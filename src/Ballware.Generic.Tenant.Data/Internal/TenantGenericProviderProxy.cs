using System.Data;
using Ballware.Meta.Client;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantGenericProviderProxy : ITenantGenericProvider
{
    private IProviderRegistry ProviderRegistry { get; }

    public TenantGenericProviderProxy(IProviderRegistry providerRegistry)
    {
        ProviderRegistry = providerRegistry;
    }

    public async Task<IEnumerable<T>> AllAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        return await provider.AllAsync<T>(tenant, entity, identifier, claims);
    }

    public async Task<IEnumerable<T>> QueryAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims,
        IDictionary<string, object> queryParams) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        return await provider.QueryAsync<T>(tenant, entity, identifier, claims, queryParams);
    }

    public async Task<long> CountAsync(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims,
        IDictionary<string, object> queryParams)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        return await provider.CountAsync(tenant, entity, identifier, claims, queryParams);
    }

    public async Task<T?> ByIdAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, Guid id) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        return await provider.ByIdAsync<T>(tenant, entity, identifier, claims, id);
    }

    public async Task<T?> NewAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        return await provider.NewAsync<T>(tenant, entity, identifier, claims);
    }

    public async Task<T?> NewQueryAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims,
        IDictionary<string, object> queryParams) where T : class
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        return await provider.NewQueryAsync<T>(tenant, entity, identifier, claims, queryParams);
    }

    public async Task SaveAsync(ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier, Dictionary<string, object> claims,
        IDictionary<string, object> value)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        await provider.SaveAsync(tenant, entity, userId, identifier, claims, value);
    }

    public async Task<RemoveResult> RemoveAsync(ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims, Guid id)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        return await provider.RemoveAsync(tenant, entity, userId, claims, id);
    }

    public async Task ImportAsync(ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier, Dictionary<string, object> claims,
        Stream importStream, Func<IDictionary<string, object>, Task<bool>> authorized)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        await provider.ImportAsync(tenant, entity, userId, identifier, claims, importStream, authorized);
    }

    public async Task<GenericExport> ExportAsync(ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims,
        IDictionary<string, object> queryParams)
    {
        var provider = ProviderRegistry.GetGenericProvider(tenant.Provider ?? "mssql");
        
        return await provider.ExportAsync(tenant, entity, identifier, claims, queryParams);
    }
}