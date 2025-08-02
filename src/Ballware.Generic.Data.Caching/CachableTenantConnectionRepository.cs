using Ballware.Generic.Caching;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Shared.Data.Repository;

namespace Ballware.Generic.Data.Caching;

public class CachableTenantConnectionRepository<TRepository> 
    : ITenantConnectionRepository
    where TRepository : ITenantConnectionRepository
{
    private ITenantConnectionCache Cache { get; }
    private TRepository Repository { get; }

    public CachableTenantConnectionRepository(ITenantConnectionCache cache, TRepository repository)
    {
        Cache = cache;
        Repository = repository;   
    }

    public async Task<TenantConnection?> ByIdAsync(Guid id)
    {
        if (!Cache.TryGetItem(id, out TenantConnection? result))
        {
            result = await Repository.ByIdAsync(id);

            if (result != null)
            {
                Cache.SetItem(id, result);
            }
        }

        return result;
    }

    public async Task<IEnumerable<TenantConnection>> AllAsync(string identifier, IDictionary<string, object> claims)
    {
        return await Repository.AllAsync(identifier, claims);
    }

    public async Task<IEnumerable<TenantConnection>> QueryAsync(string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        return await Repository.QueryAsync(identifier, claims, queryParams);
    }

    public async Task<long> CountAsync(string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        return await Repository.CountAsync(identifier, claims, queryParams);
    }

    public async Task<TenantConnection?> ByIdAsync(string identifier, IDictionary<string, object> claims, Guid id)
    {
        return await Repository.ByIdAsync(identifier, claims, id);
    }

    public async Task<TenantConnection> NewAsync(string identifier, IDictionary<string, object> claims)
    {
        return await Repository.NewAsync(identifier, claims);
    }

    public async Task<TenantConnection> NewQueryAsync(string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        return await Repository.NewQueryAsync(identifier, claims, queryParams);
    }

    public async Task SaveAsync(Guid? userId, string identifier, IDictionary<string, object> claims,
        TenantConnection value)
    {
        await Repository.SaveAsync(userId, identifier, claims, value);
        
        Cache.SetItem(value.Id, value);
    }

    public async Task<RemoveResult<TenantConnection>> RemoveAsync(Guid? userId, IDictionary<string, object> claims,
        IDictionary<string, object> removeParams)
    {
        var result = await Repository.RemoveAsync(userId, claims, removeParams);

        if (result.Result && removeParams.TryGetValue("Id", out var idParam) && Guid.TryParse(idParam.ToString(), out Guid id))
        {
            Cache.PurgeItem(id);
        }
        
        return result;
    }

    public async Task ImportAsync(Guid? userId, string identifier, IDictionary<string, object> claims, Stream importStream, Func<TenantConnection, Task<bool>> authorized)
    {
        await Repository.ImportAsync(userId, identifier, claims, importStream, authorized);
    }

    public async Task<ExportResult> ExportAsync(string identifier, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        return await Repository.ExportAsync(identifier, claims, queryParams);
    }
}