using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantGenericProviderProxy : ITenantGenericProvider
{
    private IProviderRegistry ProviderRegistry { get; }

    public TenantGenericProviderProxy(IProviderRegistry providerRegistry)
    {
        ProviderRegistry = providerRegistry;
    }

    public async Task<IEnumerable<T>> AllAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.AllAsync<T>(tenant, entity, identifier, claims);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims,
        IDictionary<string, object> queryParams) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.QueryAsync<T>(tenant, entity, identifier, claims, queryParams);
    }

    public async Task<long> CountAsync(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims,
        IDictionary<string, object> queryParams)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.CountAsync(tenant, entity, identifier, claims, queryParams);
    }

    public async Task<T?> ByIdAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, Guid id) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.ByIdAsync<T>(tenant, entity, identifier, claims, id);
    }

    public async Task<T?> NewAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.NewAsync<T>(tenant, entity, identifier, claims);
    }

    public async Task<T?> NewQueryAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims,
        IDictionary<string, object> queryParams) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.NewQueryAsync<T>(tenant, entity, identifier, claims, queryParams);
    }

    public async Task SaveAsync(Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims,
        IDictionary<string, object> value)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        await provider.SaveAsync(tenant, entity, userId, identifier, claims, value);
    }

    public async Task<RemoveResult> RemoveAsync(Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, Guid id)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.RemoveAsync(tenant, entity, userId, claims, id);
    }

    public async Task<T> GetScalarValueAsync<T>(Metadata.Tenant tenant, Entity entity, string column, Guid id, T defaultValue)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.GetScalarValueAsync<T>(tenant, entity, column, id, defaultValue);
    }

    public async Task<bool> StateAllowedAsync(Metadata.Tenant tenant, Entity entity, Guid id, int currentState, IDictionary<string, object> claims,
        IEnumerable<string> rights)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.StateAllowedAsync(tenant, entity, id, currentState, claims, rights);
    }

    public async Task ImportAsync(Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims,
        Stream importStream, Func<IDictionary<string, object>, Task<bool>> authorized)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        await provider.ImportAsync(tenant, entity, userId, identifier, claims, importStream, authorized);
    }

    public async Task<GenericExport> ExportAsync(Metadata.Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims,
        IDictionary<string, object> queryParams)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider);
        
        return await provider.ExportAsync(tenant, entity, identifier, claims, queryParams);
    }
}