using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantGenericProvider
{
    Task<IEnumerable<T>> AllAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier,
        IDictionary<string, object> claims) where T : class;

    Task<IEnumerable<T>> QueryAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier,
        IDictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class;

    Task<long> CountAsync(Metadata.Tenant tenant, Entity entity, string identifier,
        IDictionary<string, object> claims, IDictionary<string, object> queryParams);

    Task<T?> ByIdAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier,
        IDictionary<string, object> claims, Guid id) where T : class;

    Task<T?> NewAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier,
        IDictionary<string, object> claims) where T : class;

    Task<T?> NewQueryAsync<T>(Metadata.Tenant tenant, Entity entity, string identifier,
        IDictionary<string, object> claims, IDictionary<string, object> queryParams) where T : class;

    Task SaveAsync(Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier,
        IDictionary<string, object> claims, IDictionary<string, object> value);

    Task<RemoveResult> RemoveAsync(Metadata.Tenant tenant, Entity entity, Guid? userId,
        IDictionary<string, object> claims, Guid id);

    Task<T> GetScalarValueAsync<T>(Metadata.Tenant tenant, Entity entity, string column, Guid id, T defaultValue);
    
    Task<bool> StateAllowedAsync(Metadata.Tenant tenant, Entity entity, Guid id, int currentState, IDictionary<string, object> claims, IEnumerable<string> rights);
    
    Task ImportAsync(Metadata.Tenant tenant, Entity entity,
        Guid? userId,
        string identifier,
        IDictionary<string, object> claims,
        Stream importStream,
        Func<IDictionary<string, object>, Task<bool>> authorized);

    Task<GenericExport> ExportAsync(Metadata.Tenant tenant, Entity entity, string identifier,
        IDictionary<string, object> claims, IDictionary<string, object> queryParams);
}