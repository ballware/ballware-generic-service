using System.Data;
using System.Text;
using Ballware.Generic.Scripting;
using Ballware.Generic.Metadata;
using CsvHelper;
using MimeTypes;
using Newtonsoft.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerGenericProvider : ITenantGenericProvider
{
    private const string DefaultQueryIdentifier = "primary";
    private const string TenantVariableIdentifier = "tenantId";
    private const string ClaimVariablePrefix = "claim_";
    
    private ITenantStorageProvider StorageProvider { get; }
    private IServiceProvider Services { get; }
    
    public SqlServerGenericProvider(ITenantStorageProvider storageProvider, IServiceProvider serviceProvider)
    {
        StorageProvider = storageProvider;
        Services = serviceProvider;
    }
    
    public async Task<IEnumerable<T>> AllAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims) where T : class
    {
        return await QueryAsync<T>(tenant, entity, identifier, claims, new Dictionary<string, object>());
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessQueryListAsync<T>(db, null, tenant, entity, identifier, claims, queryParams);
    }

    public async Task<long> CountAsync(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessCountAsync(db, null, tenant, entity, identifier, claims, queryParams);
    }

    public async Task<T?> ByIdAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, Guid id) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessQuerySingleAsync<T>(db, null, tenant, entity, identifier, claims, new Dictionary<string, object>() { { "id", id } });
    }

    public async Task<T?> NewAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims) where T : class
    {
        return await NewQueryAsync<T>(tenant, entity, identifier, claims, new Dictionary<string, object>());
    }

    public async Task<T?> NewQueryAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessNewAsync<T>(db, null, tenant, entity, identifier, claims, queryParams);
    }

    public async Task SaveAsync(Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> value)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        using var transaction = db.BeginTransaction();

        try
        {
            await ProcessSaveAsync(db, transaction, tenant, entity, userId, identifier, claims, value);
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }

    public async Task<RemoveResult> RemoveAsync(Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, Guid id)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        using var transaction = db.BeginTransaction();

        try
        {
            var result = await ProcessRemoveAsync(db, transaction, tenant, entity, userId, claims, new Dictionary<string, object>() { { "id", id } });

            transaction.Commit();

            return result;
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }

    public async Task<T> GetScalarValueAsync<T>(Metadata.Tenant tenant, Entity entity, string column, Guid id, T defaultValue)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessScalarValueAsync(db, null, tenant, entity, column, id, defaultValue);
    }

    public async Task<bool> StateAllowedAsync(Metadata.Tenant tenant, Entity entity, Guid id, int currentState, IDictionary<string, object> claims,
        IEnumerable<string> rights)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessStateAllowedAsync(db, null, tenant, entity, id, currentState, claims, rights);
    }

    public async Task ImportAsync(Metadata.Tenant tenant, Entity entity,
      Guid? userId,
      string identifier,
      IDictionary<string, object> claims,
      Stream importStream,
      Func<IDictionary<string, object>, Task<bool>> authorized)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        using var transaction = db.BeginTransaction();
        
        try
        {
            await ProcessImportAsync(db, transaction, tenant, entity, userId, identifier, claims, importStream, authorized);
            
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();

            throw;
        }
    }

    public async Task<GenericExport> ExportAsync(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessExportAsync(db, null, tenant, entity, identifier, claims, queryParams);
    }
    
    public async Task<IEnumerable<T>> ProcessQueryListAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p) where T : class
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
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

        return scriptingExecutor.ListScript<T>(db, transaction, tenant, entity, identifier, claims, await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, query.Query, TenantPlaceholderOptions.Create()), queryParams, transaction));
    }

    public async Task<long> ProcessCountAsync(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p)
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

        return (await db.QueryAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, query.Query, TenantPlaceholderOptions.Create()), queryParams, transaction)).LongCount();
    }

    public async Task<T?> ProcessQuerySingleAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p) where T : class
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
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
        
        var item = (await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, query.Query, TenantPlaceholderOptions.Create()), queryParams, transaction)).FirstOrDefault();

        if (item != null)
        {
            item = await scriptingExecutor.ByIdScriptAsync(
                db,
                transaction,
                tenant,
                entity,
                identifier,
                claims,
                item);
        }

        return item;
    }

    public async Task<T?> ProcessNewAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p) where T : class
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

        return (await db.QueryAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, query.Query, TenantPlaceholderOptions.Create()), queryParams, transaction)).FirstOrDefault();
    }

    public async Task ProcessSaveAsync(IDbConnection db, IDbTransaction transaction, Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> value)
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
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
            var existingValue = await ProcessQuerySingleAsync<dynamic>(db, transaction, tenant, entity, identifier, claims, new Dictionary<string, object>() { { "id", guid } });

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

        await scriptingExecutor.BeforeSaveScriptAsync(
            db,
            transaction, 
            tenant,
            entity,
            userId,
            identifier,
            claims,
            insert,
            value);

        await db.ExecuteAsync(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, saveStatement.Query, TenantPlaceholderOptions.Create()), value, transaction);

        await scriptingExecutor.SaveScriptAsync(
            db,
            transaction, 
            tenant,
            entity,
            userId,
            identifier,
            claims,
            insert,
            value);
    }

    public async Task<RemoveResult> ProcessRemoveAsync(IDbConnection db, IDbTransaction transaction, Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, IDictionary<string, object> p)
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        var preliminaryCheckResult = await ScriptingExecutor.RemovePreliminaryCheckAsync(
            db,
            transaction, 
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

        await scriptingExecutor.RemoveScriptAsync(
            db,
            transaction, 
            tenant,
            entity,
            userId,
            claims,
            p);

        await db.ExecuteAsync(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, entity.RemoveStatement, TenantPlaceholderOptions.Create()), queryParams, transaction);

        return new RemoveResult
        {
            Result = true,
            Messages = Array.Empty<string>() 
        };
    }

    public async Task<T> ProcessScalarValueAsync<T>(IDbConnection db,
        IDbTransaction? transaction, 
        Metadata.Tenant tenant, 
        Entity entity, 
        string column, 
        Guid id, 
        T defaultValue)
    {
        var query = entity.ByIdQuery.FirstOrDefault(q => q.Identifier.Equals(entity.ScalarValueQuery ?? "primary"));

        if (query == null)
        {
            return defaultValue;
        }
        
        var item = (await db.QueryAsync<Dictionary<string, object>>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, query.Query, TenantPlaceholderOptions.Create()), new { tenantId = tenant.Id, id }, transaction)).FirstOrDefault();

        if (item != null && item.TryGetValue(column, out object? value))
        {
            return (T)value;
        }
        
        return defaultValue;
    }
    
    public async Task<bool> ProcessStateAllowedAsync(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, Guid id, int currentState, IDictionary<string, object> claims,
        IEnumerable<string> rights)
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        return await scriptingExecutor.StateAllowedScriptAsync(
            db,
            transaction,
            tenant,
            entity,
            id,
            currentState,
            claims,
            rights);
    }
    
    public async Task ProcessImportAsync(
        IDbConnection db,
        IDbTransaction transaction,
        Metadata.Tenant tenant,
        Entity entity, 
        Guid? userId,
        string identifier,
        IDictionary<string, object> claims,
        Stream importStream,
        Func<IDictionary<string, object>, Task<bool>> authorized)
    {
        var function = entity.CustomFunctions.FirstOrDefault(f =>
            f.Type == CustomFunctionTypes.Import && f.Id == identifier);

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
                            await ProcessSaveAsync(db, transaction, tenant, entity, userId, identifier, claims, item);
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
                                await ProcessSaveAsync(db, transaction, tenant, entity, userId, identifier, claims, item);
                            }
                        }
                    }
                }
                break;
            default:
                throw new ArgumentException($"Unsupported import format {function.Options.Format}");
        }
    }

    public async Task<GenericExport> ProcessExportAsync(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p)
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        var queryParams = Utils.TransferToSqlVariables(new Dictionary<string, object>(p), claims, "claim_");
     
        queryParams[TenantVariableIdentifier] = tenant.Id;

        var query = entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            throw new ArgumentException($"Unsupported query identifier {identifier}");
        }
        
        var function = entity.CustomFunctions.FirstOrDefault(f => f.Type == CustomFunctionTypes.Export && f.Id == identifier);

        if (function == null)
        {
            throw new ArgumentException($"Unsupported function identifier {identifier}");
        }
        
        var queryResult = await db.QueryAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, query.Query, TenantPlaceholderOptions.Create()), queryParams, transaction);

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
                    var items = await Task.WhenAll(queryResult.Select(async item => await scriptingExecutor.ByIdScriptAsync(
                        db,
                        transaction, 
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