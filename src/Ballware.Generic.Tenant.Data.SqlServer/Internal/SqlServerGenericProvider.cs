using System.Data;
using System.Text;
using Ballware.Generic.Scripting;
using Ballware.Generic.Metadata;
using CsvHelper;
using Dapper;
using MimeTypes;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerGenericProcessingContext(
    IDbConnection connection,
    IDbTransaction? transaction,
    Metadata.Tenant tenant,
    Metadata.Entity entity)
{
    public IDbConnection Connection { get; } = connection;
    public IDbTransaction? Transaction { get; } = transaction;
    public Metadata.Tenant Tenant { get; } = tenant;
    public Metadata.Entity Entity { get; } = entity;
}

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
        
        return await ProcessQueryListAsync<T>(new SqlServerGenericProcessingContext(db, null, tenant, entity), identifier, claims, queryParams);
    }

    public async Task<long> CountAsync(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessCountAsync(new SqlServerGenericProcessingContext(db, null, tenant, entity), identifier, claims, queryParams);
    }

    public async Task<T?> ByIdAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, Guid id) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessQuerySingleAsync<T>(new SqlServerGenericProcessingContext(db, null, tenant, entity), identifier, claims, new Dictionary<string, object>() { { "id", id } });
    }

    public async Task<T?> NewAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims) where T : class
    {
        return await NewQueryAsync<T>(tenant, entity, identifier, claims, new Dictionary<string, object>());
    }

    public async Task<T?> NewQueryAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessNewAsync<T>(new SqlServerGenericProcessingContext(db, null, tenant, entity), identifier, claims, queryParams);
    }

    public async Task SaveAsync(Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> value)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        using var transaction = db.BeginTransaction();

        try
        {
            await ProcessSaveAsync(new SqlServerGenericProcessingContext(db, null, tenant, entity), userId, identifier, claims, value);
            
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
            var result = await ProcessRemoveAsync(new SqlServerGenericProcessingContext(db, transaction, tenant, entity), userId, claims, new Dictionary<string, object>() { { "id", id } });

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
        
        return await ProcessScalarValueAsync(new SqlServerGenericProcessingContext(db, null, tenant, entity), column, id, defaultValue);
    }

    public async Task<bool> StateAllowedAsync(Metadata.Tenant tenant, Entity entity, Guid id, int currentState, IDictionary<string, object> claims,
        IEnumerable<string> rights)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessStateAllowedAsync(new SqlServerGenericProcessingContext(db, null, tenant, entity), id, currentState, claims, rights);
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
            await ProcessImportAsync(new SqlServerGenericProcessingContext(db, transaction, tenant, entity), userId, identifier, claims, importStream, authorized);
            
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
        
        return await ProcessExportAsync(new SqlServerGenericProcessingContext(db, null, tenant, entity), identifier, claims, queryParams);
    }
    
    public async Task<IEnumerable<T>> ProcessQueryListAsync<T>(SqlServerGenericProcessingContext context, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p) where T : class
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        var query = context.Entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            query = context.Entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (query == null)
        {
            return Array.Empty<T>();
        }

        var queryParams = new Dictionary<string, object>(p);

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        queryParams[TenantVariableIdentifier] = context.Tenant.Id;

        return scriptingExecutor.ListScript<T>(context.Connection, context.Transaction, context.Tenant, context.Entity, identifier, claims, await context.Connection.QueryAsync<T>(
            await StorageProvider.ApplyTenantPlaceholderAsync(
                context.Tenant.Id, 
                query.Query, 
                TenantPlaceholderOptions.Create()), 
            queryParams, context.Transaction));
    }

    public async Task<long> ProcessCountAsync(SqlServerGenericProcessingContext context, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p)
    {
        var query = context.Entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            query = context.Entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (query == null)
        {
            return 0;
        }

        var queryParams = new Dictionary<string, object>(p);

        queryParams[TenantVariableIdentifier] = context.Tenant.Id;

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);

        return await context.Connection.QuerySingleAsync<long>(
            await StorageProvider.ApplyTenantPlaceholderAsync(
                context.Tenant.Id, query.Query, TenantPlaceholderOptions.Create()), 
            queryParams, context.Transaction);
    }

    public async Task<T?> ProcessQuerySingleAsync<T>(SqlServerGenericProcessingContext context, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p) where T : class
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        var query = context.Entity.ByIdQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            query = context.Entity.ByIdQuery.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (query == null)
        {
            return null;
        }

        var queryParams = new Dictionary<string, object>(p);

        queryParams[TenantVariableIdentifier] = context.Tenant.Id;

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        
        var item = (await context.Connection.QueryAsync<T>(
            await StorageProvider.ApplyTenantPlaceholderAsync(context.Tenant.Id, query.Query, TenantPlaceholderOptions.Create()), 
            queryParams, context.Transaction)).FirstOrDefault();

        if (item != null)
        {
            item = await scriptingExecutor.ByIdScriptAsync(
                context.Connection,
                context.Transaction,
                context.Tenant,
                context.Entity,
                identifier,
                claims,
                item);
        }

        return item;
    }

    public async Task<T?> ProcessNewAsync<T>(SqlServerGenericProcessingContext context, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p) where T : class
    {
        var query = context.Entity.NewQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            query = context.Entity.NewQuery.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (query == null)
        {
            return null;
        }

        var queryParams = new Dictionary<string, object>(p);

        queryParams[TenantVariableIdentifier] = context.Tenant.Id;
        
        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);

        return (await context.Connection.QueryAsync<dynamic>(
            await StorageProvider.ApplyTenantPlaceholderAsync(context.Tenant.Id, query.Query, TenantPlaceholderOptions.Create()), 
            queryParams, context.Transaction)).FirstOrDefault();
    }

    public async Task ProcessSaveAsync(SqlServerGenericProcessingContext context, Guid? userId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> value)
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        value[TenantVariableIdentifier] = context.Tenant.Id;

        value = Utils.TransferToSqlVariables(value, claims, ClaimVariablePrefix);
        
        var saveStatement = context.Entity.SaveStatement.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (saveStatement == null)
        {
            saveStatement = context.Entity.SaveStatement.FirstOrDefault(q => q.Identifier.Equals(DefaultQueryIdentifier));
        }

        if (saveStatement == null)
        {
            throw new ArgumentException($"Unsupported function identifier {identifier}");
        }

        bool insert = false;

        if (value.ContainsKey("Id") && Guid.TryParse(value["Id"].ToString(), out Guid guid))
        {
            var existingValue = await ProcessQuerySingleAsync<dynamic>(
                context, identifier, claims, new Dictionary<string, object>() { { "id", guid } });

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
            context.Connection,
            context.Transaction, 
            context.Tenant,
            context.Entity,
            userId,
            identifier,
            claims,
            insert,
            value);

        await context.Connection.ExecuteAsync(
            await StorageProvider.ApplyTenantPlaceholderAsync(context.Tenant.Id, saveStatement.Query, TenantPlaceholderOptions.Create()), 
            Utils.DropComplexMember(value), 
            context.Transaction);

        await scriptingExecutor.SaveScriptAsync(
            context.Connection,
            context.Transaction, 
            context.Tenant,
            context.Entity,
            userId,
            identifier,
            claims,
            insert,
            value);
    }

    public async Task<RemoveResult> ProcessRemoveAsync(SqlServerGenericProcessingContext context, Guid? userId, IDictionary<string, object> claims, IDictionary<string, object> p)
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        var preliminaryCheckResult = await scriptingExecutor.RemovePreliminaryCheckAsync(
            context.Connection,
            context.Transaction, 
            context.Tenant,
            context.Entity,
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
        
        queryParams[TenantVariableIdentifier] = context.Tenant.Id;

        await scriptingExecutor.RemoveScriptAsync(
            context.Connection,
            context.Transaction, 
            context.Tenant,
            context.Entity,
            userId,
            claims,
            p);

        await context.Connection.ExecuteAsync(
            await StorageProvider.ApplyTenantPlaceholderAsync(context.Tenant.Id, context.Entity.RemoveStatement, TenantPlaceholderOptions.Create()), 
            queryParams, 
            context.Transaction);

        return new RemoveResult
        {
            Result = true,
            Messages = [] 
        };
    }

    public async Task<T> ProcessScalarValueAsync<T>(
        SqlServerGenericProcessingContext context, 
        string column, 
        Guid id, 
        T defaultValue)
    {
        var query = context.Entity.ByIdQuery.FirstOrDefault(q => q.Identifier.Equals(context.Entity.ScalarValueQuery ?? "primary"));

        if (query == null)
        {
            return defaultValue;
        }
        
        var item = (await context.Connection.QueryAsync<Dictionary<string, object>>(
            await StorageProvider.ApplyTenantPlaceholderAsync(context.Tenant.Id, query.Query, TenantPlaceholderOptions.Create()), 
            new { tenantId = context.Tenant.Id, id }, 
            context.Transaction)).FirstOrDefault();

        if (item != null && item.TryGetValue(column, out object? value))
        {
            return (T)value;
        }
        
        return defaultValue;
    }
    
    public async Task<bool> ProcessStateAllowedAsync(SqlServerGenericProcessingContext context, Guid id, int currentState, IDictionary<string, object> claims,
        IEnumerable<string> rights)
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        return await scriptingExecutor.StateAllowedScriptAsync(
            context.Connection,
            context.Transaction,
            context.Tenant,
            context.Entity,
            id,
            currentState,
            claims,
            rights);
    }
    
    public async Task ProcessImportAsync(
        SqlServerGenericProcessingContext context, 
        Guid? userId,
        string identifier,
        IDictionary<string, object> claims,
        Stream importStream,
        Func<IDictionary<string, object>, Task<bool>> authorized)
    {
        var function = context.Entity.CustomFunctions.FirstOrDefault(f =>
            f.Type == CustomFunctionTypes.Import && f.Id == identifier);

        if (function == null)
        {
            throw new ArgumentException($"Unsupported function identifier {identifier}");
        }
        
        switch (function.Options?.Format)
        {
            case "csv":
                {
                    await foreach (var item in GetCsvItemsAsync(function, importStream))
                    {
                        if (await authorized(item))
                        {
                            await ProcessSaveAsync(context, userId, identifier, claims, item);
                        }
                    }
                }
                break;
            case "json":
                {
                    await foreach (var item in GetJsonItemsAsync(importStream))
                    {
                        if (await authorized(item))
                        {
                            await ProcessSaveAsync(context, userId, identifier, claims, item);
                        }
                    }
                }
                break;
            default:
                throw new ArgumentException($"Unsupported import format {function.Options.Format}");
        }
    }

    private static async IAsyncEnumerable<IDictionary<string, object?>> GetCsvItemsAsync(CustomFunctionEntry function, Stream importStream)
    {
        var config = new CsvHelper.Configuration.CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
        {
            Delimiter = function.Options?.Delimiter ?? ",",
        };

        using var reader = new StreamReader(importStream);
        using var csvReader = new CsvReader(reader, config);
                    
        while (await csvReader.ReadAsync())
        {
            var item = ((IDictionary<string, object>)csvReader.GetRecord<dynamic>())
                .ToDictionary(x => x.Key, x => string.IsNullOrEmpty(x.Value as string) ? null : x.Value);

            yield return item;
        }
    }
    
    private static async IAsyncEnumerable<IDictionary<string, object?>> GetJsonItemsAsync(Stream importStream)
    {
        using var reader = new StreamReader(importStream);

        var items = JsonConvert.DeserializeObject<IDictionary<string, object>[]>(await reader.ReadToEndAsync());
                    
        if (items != null)
        {
            foreach (var item in items)
            {
                var normalizedItem = Utils.NormalizeJsonMember(item);

                yield return normalizedItem;
            }
        }
    }
    
    private async Task<GenericExport> ProcessExportAsync(SqlServerGenericProcessingContext context, string identifier, IDictionary<string, object> claims, IDictionary<string, object> p)
    {
        var scriptingExecutor = Services.GetRequiredService<IGenericEntityScriptingExecutor>();
        
        var queryParams = Utils.TransferToSqlVariables(new Dictionary<string, object>(p), claims, "claim_");
     
        queryParams[TenantVariableIdentifier] = context.Tenant.Id;

        var query = context.Entity.ListQuery.FirstOrDefault(q => q.Identifier.Equals(identifier));

        if (query == null)
        {
            throw new ArgumentException($"Unsupported query identifier {identifier}");
        }
        
        var function = context.Entity.CustomFunctions.FirstOrDefault(f => f.Type == CustomFunctionTypes.Export && f.Id == identifier);

        if (function == null)
        {
            throw new ArgumentException($"Unsupported function identifier {identifier}");
        }
        
        var queryResult = await context.Connection.QueryAsync<dynamic>(
            await StorageProvider.ApplyTenantPlaceholderAsync(context.Tenant.Id, query.Query, TenantPlaceholderOptions.Create()), 
            queryParams, context.Transaction);

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
                        context.Connection,
                        context.Transaction, 
                        context.Tenant,
                        context.Entity,
                        identifier,
                        claims,
                        item)));

                    result.Data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(items));
                }
                break;
            default:
                throw new ArgumentException($"Unsupported export format {function.Options.Format}");
        }

        return result;
    }
}