using System.Data;
using Ballware.Meta.Client;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantGenericProvider
{
    Task<IEnumerable<T>> AllAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier,
        Dictionary<string, object> claims) where T : class;

    Task<IEnumerable<T>> QueryAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier,
        Dictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class;

    Task<long> CountAsync(ServiceTenant tenant, ServiceEntity entity, string identifier,
        Dictionary<string, object> claims, IDictionary<string, object> queryParams);

    Task<T?> ByIdAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier,
        Dictionary<string, object> claims, Guid id) where T : class;

    Task<T?> NewAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier,
        Dictionary<string, object> claims) where T : class;

    Task<T?> NewQueryAsync<T>(ServiceTenant tenant, ServiceEntity entity, string identifier,
        Dictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class;

    Task SaveAsync(ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier,
        Dictionary<string, object> claims, IDictionary<string, object> value);

    Task<RemoveResult> RemoveAsync(ServiceTenant tenant, ServiceEntity entity, Guid? userId,
        Dictionary<string, object> claims, Guid id);
    
    Task ImportAsync(ServiceTenant tenant, ServiceEntity entity,
        Guid? userId,
        string identifier,
        Dictionary<string, object> claims,
        Stream importStream,
        Func<IDictionary<string, object>, Task<bool>> authorized);

    Task<GenericExport> ExportAsync(ServiceTenant tenant, ServiceEntity entity, string identifier,
        Dictionary<string, object> claims, IDictionary<string, object> queryParams);
}