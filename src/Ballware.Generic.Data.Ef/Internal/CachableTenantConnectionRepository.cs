using AutoMapper;
using Ballware.Generic.Caching;
using Ballware.Generic.Data.Public;
using Ballware.Shared.Data.Repository;

namespace Ballware.Generic.Data.Ef.Internal;

class CachableTenantConnectionRepository : TenantConnectionRepository
{
    private ITenantConnectionCache Cache { get; }

    public CachableTenantConnectionRepository(IMapper mapper, TenantDbContext dbContext, ITenantConnectionCache cache)
        : base(mapper, dbContext)
    {
        Cache = cache;
    }

    public override async Task<TenantConnection?> ByIdAsync(Guid id)
    {
        if (!Cache.TryGetItem(id, out TenantConnection? result))
        {
            result = await base.ByIdAsync(id);

            if (result != null)
            {
                Cache.SetItem(id, result);
            }
        }

        return result;
    }

    public override async Task SaveAsync(Guid? userId, string identifier, IDictionary<string, object> claims,
        TenantConnection value)
    {
        await base.SaveAsync(userId, identifier, claims, value);
        
        Cache.SetItem(value.Id, value);
    }

    public override async Task<RemoveResult<TenantConnection>> RemoveAsync(Guid? userId, IDictionary<string, object> claims,
        IDictionary<string, object> removeParams)
    {
        var result = await base.RemoveAsync(userId, claims, removeParams);

        if (result.Result && removeParams.TryGetValue("Id", out var idParam) && Guid.TryParse(idParam.ToString(), out Guid id))
        {
            Cache.PurgeItem(id);
        }
        
        return result;
    }
}