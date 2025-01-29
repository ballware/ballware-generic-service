using System.Data;
using System.Text;
using Ballware.Generic.Scripting;
using Ballware.Meta.Client;
using CsvHelper;
using MimeTypes;
using Newtonsoft.Json;
using Dapper;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

public class SqlServerGenericProvider : ITenantGenericProvider
{
    private const string DefaultQueryIdentifier = "primary";
    private const string TenantVariableIdentifier = "tenantId";
    private const string ClaimVariablePrefix = "claim_";
    
    private ITenantStorageProvider StorageProvider { get; }
    private IGenericEntityScriptingExecutor ScriptingExecutor { get; }
    
    public SqlServerGenericProvider(ITenantStorageProvider storageProvider, IGenericEntityScriptingExecutor scriptingExecutor)
    {
        StorageProvider = storageProvider;
        ScriptingExecutor = scriptingExecutor;
    }
    
    public async Task<IEnumerable<T>> AllAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims) where T : class
    {
        return await QueryAsync<T>(tenant, entity, identifier, claims, new Dictionary<string, object>());
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant);
        
        return await ProcessQueryListAsync<T>(db, tenant, entity, identifier, claims, queryParams);
    }

    public async Task<long> CountAsync(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant);
        
        return await ProcessCountAsync(db, tenant, entity, identifier, claims, queryParams);
    }

    public async Task<T?> ByIdAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, Guid id) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant);
        
        return await ProcessQuerySingleAsync<T>(db, tenant, entity, identifier, claims, new Dictionary<string, object>() { { "id", id } });
    }

    public async Task<T?> NewAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims) where T : class
    {
        return await NewQueryAsync<T>(tenant, entity, identifier, claims, new Dictionary<string, object>());
    }

    public async Task<T?> NewQueryAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant);
        
        return await ProcessNewAsync<T>(db, tenant, entity, identifier, claims, queryParams);
    }

    public async Task SaveAsync(ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier, Dictionary<string, object> claims, IDictionary<string, object> value)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant);
        using var transaction = db.BeginTransaction();

        try
        {
            await ProcessSaveAsync(db, tenant, entity, userId, identifier, claims, value);
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }

    public async Task<RemoveResult> RemoveAsync(ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims, Guid id)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant);
        using var transaction = db.BeginTransaction();

        try
        {
            var result = await ProcessRemoveAsync(db, tenant, entity, userId, claims, new Dictionary<string, object>() { { "id", id } });

            transaction.Commit();

            return result;
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }

    public async Task ImportAsync(ServiceTenant tenant, ServiceEntity entity,
      Guid? userId,
      string identifier,
      Dictionary<string, object> claims,
      Stream importStream,
      Func<IDictionary<string, object>, Task<bool>> authorized)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant);
        using var transaction = db.BeginTransaction();

        try
        {
            await ProcessImportAsync(db, tenant, entity, userId, identifier, claims, importStream, authorized);
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }

    public async Task<GenericExport> ExportAsync(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant);
        
        return await ProcessExportAsync(db, tenant, entity, identifier, claims, queryParams);
    }
    
    public async Task<IEnumerable<T>> ProcessQueryListAsync<T>(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> p) where T : class
    {
        var query = entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            query = entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (query == null)
        {
            return Array.Empty<T>();
        }

        var queryParams = new Dictionary<string, object>(p);

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        queryParams[TenantVariableIdentifier] = tenant.Id;

        return ScriptingExecutor.ListScript<T>(db, tenant, entity, identifier, claims, await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant, query.Query, TenantPlaceholderOptions.Create()), queryParams));
    }

    public async Task<long> ProcessCountAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> p)
    {
        var query = entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            query = entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (query == null)
        {
            return 0;
        }

        var queryParams = new Dictionary<string, object>(p);

        queryParams[TenantVariableIdentifier] = tenant.Id;

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);

        return await db.QuerySingleAsync<long>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant, query.Query, TenantPlaceholderOptions.Create()), queryParams);
    }

    public async Task<T?> ProcessQuerySingleAsync<T>(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> p) where T : class
    {
        var query = entity.ByIdQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            query = entity.ByIdQuery.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (query == null)
        {
            return null;
        }

        var queryParams = new Dictionary<string, object>(p);

        queryParams[TenantVariableIdentifier] = tenant.Id;

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        
        var item = (await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant, query.Query, TenantPlaceholderOptions.Create()), queryParams)).FirstOrDefault();

        if (item != null)
        {
            item = await ScriptingExecutor.ByIdScriptAsync(
                db,
                tenant,
                entity,
                identifier,
                claims,
                item);
        }

        return item;
    }

    public async Task<T?> ProcessNewAsync<T>(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> p) where T : class
    {
        var query = entity.NewQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            query = entity.NewQuery.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (query == null)
        {
            return null;
        }

        var queryParams = new Dictionary<string, object>(p);

        queryParams[TenantVariableIdentifier] = tenant.Id;
        
        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);

        return (await db.QueryAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant, query.Query, TenantPlaceholderOptions.Create()), queryParams)).FirstOrDefault();
    }

    public async Task ProcessSaveAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier, Dictionary<string, object> claims, IDictionary<string, object> value)
    {
        value[TenantVariableIdentifier] = tenant.Id;

        value = Utils.TransferToSqlVariables(value, claims, ClaimVariablePrefix);
        
        var saveStatement = entity.SaveStatement.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (saveStatement == null)
        {
            saveStatement = entity.SaveStatement.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (saveStatement == null)
        {
            throw new ArgumentException($"Unsupported function identifier {identifier}");
        }

        bool insert = false;

        if (value.ContainsKey("Id") && Guid.TryParse(value["Id"].ToString(), out Guid guid))
        {
            var existingValue = await ProcessQuerySingleAsync<dynamic>(db, tenant, entity, identifier, claims, new Dictionary<string, object>() { { "id", guid } });

            if (existingValue != null)
            {
                foreach (KeyValuePair<string, object> prop in existingValue)
                {
                    value[$"original_{prop.Key}"] = prop.Value;
                }
            }
            else
            {
                insert = true;
            }
        }
        else
        {
            insert = true;
        }

        await ScriptingExecutor.BeforeSaveScriptAsync(
            db,
            tenant,
            entity,
            userId,
            identifier,
            claims,
            insert,
            value);

        await db.ExecuteAsync(await StorageProvider.ApplyTenantPlaceholderAsync(tenant, saveStatement.Query, TenantPlaceholderOptions.Create()), value);

        await ScriptingExecutor.SaveScriptAsync(
            db,
            tenant,
            entity,
            userId,
            identifier,
            claims,
            insert,
            value);
    }

    public async Task<RemoveResult> ProcessRemoveAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims, IDictionary<string, object> p)
    {
        var preliminaryCheckResult = await ScriptingExecutor.RemovePreliminaryCheckAsync(
            db,
            tenant,
            entity,
            userId,
            claims,
            p);

        if (!preliminaryCheckResult.Result)
        {
            return new RemoveResult
            {
                Result = preliminaryCheckResult.Result,
                Messages = preliminaryCheckResult.Messages
            };
        }
        
        var queryParams = Utils.TransferToSqlVariables(new Dictionary<string, object>(p), claims, "claim_");
        
        queryParams[TenantVariableIdentifier] = tenant.Id;

        await ScriptingExecutor.RemoveScriptAsync(
            db,
            tenant,
            entity,
            userId,
            claims,
            p);

        await db.ExecuteAsync(await StorageProvider.ApplyTenantPlaceholderAsync(tenant, entity.RemoveStatement, TenantPlaceholderOptions.Create()), queryParams);

        return new RemoveResult
        {
            Result = true,
            Messages = Array.Empty<string>() 
        };
    }

    public async Task ProcessImportAsync(
        IDbConnection db,
        ServiceTenant tenant,
        ServiceEntity entity, 
        Guid? userId,
        string identifier,
        Dictionary<string, object> claims,
        Stream importStream,
        Func<IDictionary<string, object>, Task<bool>> authorized)
    {
        var function = entity.CustomFunctions.FirstOrDefault(f =>
            f.Type == ServiceEntityCustomFunctionTypes.Import && f.Id == identifier);

        if (function == null)
        {
            throw new ArgumentException($"Unsupported function identifier {identifier}");
        }
        
        switch (function.Options.Format)
        {
            case "csv":
                {
                    var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
                    {
                        Delimiter = function.Options.Delimiter ?? ",",
                    };

                    using var reader = new StreamReader(importStream);
                    using var csvReader = new CsvReader(reader, config);
                    
                    while (await csvReader.ReadAsync())
                    {
                        var item = ((IDictionary<string, object>)csvReader.GetRecord<dynamic>())
                            .ToDictionary(x => x.Key, x => string.IsNullOrEmpty(x.Value as string) ? null : x.Value);

                        if (await authorized(item))
                        {
                            await ProcessSaveAsync(db, tenant, entity, userId, identifier, claims, item);
                        }
                    }
                }
                break;
            case "json":
                {
                    using var reader = new StreamReader(importStream);

                    var items = JsonConvert.DeserializeObject<IDictionary<string, object>[]>(await reader.ReadToEndAsync());

                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            if (await authorized(item))
                            {
                                await ProcessSaveAsync(db, tenant, entity, userId, identifier, claims, item);
                            }
                        }
                    }
                }
                break;
            default:
                throw new ArgumentException($"Unsupported import format {function.Options.Format}");
        }
    }

    public async Task<GenericExport> ProcessExportAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IDictionary<string, object> p)
    {
        var queryParams = Utils.TransferToSqlVariables(new Dictionary<string, object>(p), claims, "claim_");
     
        queryParams[TenantVariableIdentifier] = tenant.Id;

        var query = entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            throw new ArgumentException($"Unsupported query identifier {identifier}");
        }
        
        var function = entity.CustomFunctions.FirstOrDefault(f => f.Type == ServiceEntityCustomFunctionTypes.Export && f.Id == identifier);

        if (function == null)
        {
            throw new ArgumentException($"Unsupported function identifier {identifier}");
        }
        
        var queryResult = await db.QueryAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant, query.Query, TenantPlaceholderOptions.Create()), queryParams);

        var result = new GenericExport
        {
            FileName = $"{identifier}.{function.Options.Format}",
            MediaType = MimeTypeMap.GetMimeType(function.Options.Format)
        };

        switch (function.Options.Format)
        {
            case "csv":
                {
                    var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
                    {
                        Delimiter = function.Options.Delimiter ?? ","
                    };

                    using var stream = new MemoryStream();
                    await using var writer = new StreamWriter(stream);
                    await using var csvWriter = new CsvWriter(writer, config);
                    
                    await csvWriter.WriteRecordsAsync(queryResult);
                    await csvWriter.FlushAsync();
                    await writer.FlushAsync();
                    await stream.FlushAsync();

                    result.Data = stream.ToArray();
                }
                break;
            case "json":
                {
                    var items = await Task.WhenAll(queryResult.Select(async item => await ScriptingExecutor.ByIdScriptAsync(
                        db,
                        tenant,
                        entity,
                        identifier,
                        claims,
                        item)));

                    result.Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(items));
                }
                break;
            default:
                throw new ArgumentException($"Unsupported export format {function.Options.Format}");
        }

        return result;
    }
}