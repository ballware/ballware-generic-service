using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantLookupProviderProxy : ITenantLookupProvider
{
    private IProviderRegistry ProviderRegistry { get; }

    public TenantLookupProviderProxy(IProviderRegistry providerRegistry)
    {
        ProviderRegistry = providerRegistry;
    }

    public async Task<IEnumerable<T>> SelectListForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims) where T : class
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);

        return await provider.SelectListForLookupAsync<T>(tenant, lookup, claims);
    }
    
    [Obsolete("Use overload with claims param instead")]
    public async Task<IEnumerable<T>> SelectListForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights) where T : class
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);

        return await provider.SelectListForLookupAsync<T>(tenant, lookup, rights);
    }

    public async Task<T> SelectByIdForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, object id) where T : class
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.SelectByIdForLookupAsync<T>(tenant, lookup, claims, id);
    }
    
    [Obsolete("Use overload with claims param instead")]
    public async Task<T> SelectByIdForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, object id) where T : class
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.SelectByIdForLookupAsync<T>(tenant, lookup, rights, id);
    }

    public async Task<IEnumerable<T>> SelectListForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, string param) where T : class
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.SelectListForLookupWithParamAsync<T>(tenant, lookup, claims, param);
    }
    
    [Obsolete("Use overload with claims param instead")]
    public async Task<IEnumerable<T>> SelectListForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, string param) where T : class
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.SelectListForLookupWithParamAsync<T>(tenant, lookup, rights, param);
    }

    public async Task<T> SelectByIdForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, object id, string param) where T : class
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.SelectByIdForLookupWithParamAsync<T>(tenant, lookup, claims, id, param);
    }
    
    [Obsolete("Use overload with claims param instead")]
    public async Task<T> SelectByIdForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, object id, string param) where T : class
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.SelectByIdForLookupWithParamAsync<T>(tenant, lookup, rights, id, param);
    }
    
    public async Task<IEnumerable<string>> AutocompleteForLookupAsync(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims)
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.AutocompleteForLookupAsync(tenant, lookup, claims);
    }
    
    [Obsolete("Use overload with claims param instead")]
    public async Task<IEnumerable<string>> AutocompleteForLookupAsync(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights)
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.AutocompleteForLookupAsync(tenant, lookup, rights);
    }

    public async Task<IEnumerable<string>> AutocompleteForLookupWithParamAsync(Metadata.Tenant tenant, Lookup lookup, IDictionary<string, object> claims, string param)
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.AutocompleteForLookupWithParamAsync(tenant, lookup, claims, param);
    }
    
    [Obsolete("Use overload with claims param instead")]
    public async Task<IEnumerable<string>> AutocompleteForLookupWithParamAsync(Metadata.Tenant tenant, Lookup lookup, IEnumerable<string> rights, string param)
    {
        var provider = ProviderRegistry.GetLookupProvider(tenant.Provider);
        
        return await provider.AutocompleteForLookupWithParamAsync(tenant, lookup, rights, param);
    }
}