using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantLookupProvider
{
    Task<IEnumerable<T>> SelectListForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup,
        IDictionary<string, object> claims) where T : class;
    
    [Obsolete("Use overload with claims param instead")]
    Task<IEnumerable<T>> SelectListForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup,
        IEnumerable<string> rights) where T : class;
    
    Task<T> SelectByIdForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup,
        IDictionary<string, object> claims, object id) where T : class;
    
    [Obsolete("Use overload with claims param instead")]
    Task<T> SelectByIdForLookupAsync<T>(Metadata.Tenant tenant, Lookup lookup,
        IEnumerable<string> rights, object id) where T : class;
    
    Task<IEnumerable<T>> SelectListForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup,
        IDictionary<string, object> claims, string param) where T : class;
    
    [Obsolete("Use overload with claims param instead")]
    Task<IEnumerable<T>> SelectListForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup,
        IEnumerable<string> rights, string param) where T : class;
    
    Task<T> SelectByIdForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup,
        IDictionary<string, object> claims, object id, string param) where T : class;
    
    [Obsolete("Use overload with claims param instead")]
    Task<T> SelectByIdForLookupWithParamAsync<T>(Metadata.Tenant tenant, Lookup lookup,
        IEnumerable<string> rights, object id, string param) where T : class;
    
    Task<IEnumerable<string>> AutocompleteForLookupAsync(Metadata.Tenant tenant, Lookup lookup,
        IDictionary<string, object> claims);
    
    [Obsolete("Use overload with claims param instead")]
    Task<IEnumerable<string>> AutocompleteForLookupAsync(Metadata.Tenant tenant, Lookup lookup,
        IEnumerable<string> rights);
    
    Task<IEnumerable<string>> AutocompleteForLookupWithParamAsync(Metadata.Tenant tenant, Lookup lookup,
        IDictionary<string, object> claims, string param);
    
    [Obsolete("Use overload with claims param instead")]
    Task<IEnumerable<string>> AutocompleteForLookupWithParamAsync(Metadata.Tenant tenant, Lookup lookup,
        IEnumerable<string> rights, string param);
}