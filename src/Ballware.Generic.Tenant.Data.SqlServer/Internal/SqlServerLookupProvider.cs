using System.Data;
using Ballware.Generic.Metadata;
using Dapper;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerLookupProvider : ITenantLookupProvider
{
    private const string TenantVariableIdentifier = "tenantId";
    private const string ClaimVariablePrefix = "claim_";
    
    private ITenantStorageProvider StorageProvider { get; }
    
    public SqlServerLookupProvider(ITenantStorageProvider storageProvider)
    {
        StorageProvider = storageProvider;
    }

    public async Task<IEnumerable<T>> SelectListForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessSelectListForLookupAsync<T>(db, null, tenant, lookup, claims);
    }

    public async Task<IEnumerable<T>> SelectListForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessSelectListForLookupAsync<T>(db, null, tenant, lookup, rights);
    }

    public async Task<T> SelectByIdForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, object id) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessSelectByIdForLookupAsync<T>(db, null, tenant, lookup, claims, id);
    }

    public async Task<T> SelectByIdForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, object id) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessSelectByIdForLookupAsync<T>(db, null, tenant, lookup, rights, id);
    }

    public async Task<IEnumerable<T>> SelectListForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, string param) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessSelectListForLookupWithParamAsync<T>(db, null, tenant, lookup, claims, param);
    }

    public async Task<IEnumerable<T>> SelectListForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, string param) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessSelectListForLookupWithParamAsync<T>(db, null, tenant, lookup, rights, param);
    }

    public async Task<T> SelectByIdForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, object id, string param) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessSelectByIdForLookupWithParamAsync<T>(db, null, tenant, lookup, claims, id, param);
    }

    public async Task<T> SelectByIdForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, object id, string param) where T : class
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessSelectByIdForLookupWithParamAsync<T>(db, null, tenant, lookup, rights, id, param);
    }

    public async Task<IEnumerable<string>> AutocompleteForLookupAsync(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessAutocompleteForLookupAsync(db, null, tenant, lookup, claims);
    }

    public async Task<IEnumerable<string>> AutocompleteForLookupAsync(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessAutocompleteForLookupAsync(db, null, tenant, lookup, rights);
    }

    public async Task<IEnumerable<string>> AutocompleteForLookupWithParamAsync(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, string param)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessAutocompleteForLookupWithParamAsync(db, null, tenant, lookup, claims, param);
    }

    public async Task<IEnumerable<string>> AutocompleteForLookupWithParamAsync(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, string param)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);
        
        return await ProcessAutocompleteForLookupWithParamAsync(db, null, tenant, lookup, rights, param);
    }

    public async Task<IEnumerable<T>> ProcessSelectListForLookupAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims) where T : class
    {
        var queryParams = new Dictionary<string, object>();

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        queryParams[TenantVariableIdentifier] = tenant.Id;
        
        return await db.QueryAsync<T>(
            await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ListQuery,
                TenantPlaceholderOptions.Create()), queryParams, transaction);
    }
    
    public async Task<IEnumerable<T>> ProcessSelectListForLookupAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights) where T : class
    {
        return await db.QueryAsync<T>(
            await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ListQuery,
                TenantPlaceholderOptions.Create()), new { tenantId = tenant.Id, claims = string.Join(",", rights) }, transaction);
    }

    public async Task<T> ProcessSelectByIdForLookupAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, object id) where T : class
    {
        if (string.IsNullOrEmpty(lookup.ByIdQuery))
        {
            throw new ArgumentException(nameof(lookup.ByIdQuery));
        }
        
        var queryParams = new Dictionary<string, object>();

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        queryParams[TenantVariableIdentifier] = tenant.Id;
        queryParams["id"] = id;
        
        return (await db.QuerySingleAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ByIdQuery,
            TenantPlaceholderOptions.Create()), queryParams, transaction));
    }

    public async Task<T> ProcessSelectByIdForLookupAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, object id) where T : class
    {
        if (string.IsNullOrEmpty(lookup.ByIdQuery))
        {
            throw new ArgumentException(nameof(lookup.ByIdQuery));
        }
        
        return (await db.QuerySingleAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ByIdQuery,
            TenantPlaceholderOptions.Create()), new { id, tenantId = tenant.Id, claims = string.Join(",", rights) }, transaction));
    }
    
    public async Task<IEnumerable<T>> ProcessSelectListForLookupWithParamAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, string param) where T : class
    {
        var queryParams = new Dictionary<string, object>();

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        queryParams[TenantVariableIdentifier] = tenant.Id;
        queryParams["param"] = param;
        
        return await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ListQuery,
            TenantPlaceholderOptions.Create()), queryParams, transaction);
    }
    
    public async Task<IEnumerable<T>> ProcessSelectListForLookupWithParamAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, string param) where T : class
    {
        return await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ListQuery,
            TenantPlaceholderOptions.Create()), new { tenantId = tenant.Id, claims = string.Join(",", rights), param }, transaction);  
    }

    public async Task<T> ProcessSelectByIdForLookupWithParamAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, object id, string param) where T : class
    {
        if (string.IsNullOrEmpty(lookup.ByIdQuery))
        {
            throw new ArgumentException(nameof(lookup.ByIdQuery));
        }
        
        var queryParams = new Dictionary<string, object>();

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        queryParams[TenantVariableIdentifier] = tenant.Id;
        queryParams["param"] = param;
        queryParams["id"] = id;
        
        return (await db.QuerySingleAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id,
                lookup.ByIdQuery,
                TenantPlaceholderOptions.Create()),
            queryParams, transaction));     
    }
    
    public async Task<T> ProcessSelectByIdForLookupWithParamAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, object id, string param) where T : class
    {
        if (string.IsNullOrEmpty(lookup.ByIdQuery))
        {
            throw new ArgumentException(nameof(lookup.ByIdQuery));
        }
        
        return (await db.QuerySingleAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id,
                lookup.ByIdQuery,
                TenantPlaceholderOptions.Create()),
            new { id, tenantId = tenant.Id, claims = string.Join(",", rights), param }, transaction));
    }

    public async Task<IEnumerable<string>> ProcessAutocompleteForLookupAsync(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims)
    {
        var queryParams = new Dictionary<string, object>();

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        queryParams[TenantVariableIdentifier] = tenant.Id;
        
        return await db.QueryAsync<string>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ListQuery,
            TenantPlaceholderOptions.Create()), queryParams, transaction);
    }
    
    public async Task<IEnumerable<string>> ProcessAutocompleteForLookupAsync(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights)
    {
        return await db.QueryAsync<string>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ListQuery,
            TenantPlaceholderOptions.Create()), new { tenantId = tenant.Id, claims = string.Join(",", rights) }, transaction); 
    }

    public async Task<IEnumerable<string>> ProcessAutocompleteForLookupWithParamAsync(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, string param)
    {
        var queryParams = new Dictionary<string, object>();

        queryParams = Utils.TransferToSqlVariables(queryParams, claims, ClaimVariablePrefix);
        queryParams[TenantVariableIdentifier] = tenant.Id;
        queryParams["param"] = param;
        
        return await db.QueryAsync<string>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ListQuery,
            TenantPlaceholderOptions.Create()), queryParams, transaction);
    }
    
    public async Task<IEnumerable<string>> ProcessAutocompleteForLookupWithParamAsync(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, string param)
    {
        return await db.QueryAsync<string>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, lookup.ListQuery,
            TenantPlaceholderOptions.Create()), new { tenantId = tenant.Id, claims = string.Join(",", rights), param }, transaction); 
    }
}