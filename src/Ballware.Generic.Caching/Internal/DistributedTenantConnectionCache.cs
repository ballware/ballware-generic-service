using Ballware.Generic.Caching.Configuration;
using Ballware.Generic.Data.Public;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Ballware.Generic.Caching.Internal;

class DistributedTenantConnectionCache : ITenantConnectionCache
{
    private ILogger<DistributedTenantConnectionCache> Logger { get; }
    private IDistributedCache Cache { get; }
    private CacheOptions Options { get; }
    
    public DistributedTenantConnectionCache(ILogger<DistributedTenantConnectionCache> logger, IDistributedCache cache, IOptions<CacheOptions> options)
    {
        Logger = logger;
        Cache = cache;
        Options = options.Value;
    }
    
    public TenantConnection? GetItem(Guid tenantId)
    {
        var cachedSerializedItem = Cache.GetString(tenantId.ToString());
        
        if (cachedSerializedItem != null)
        {
            Logger.LogTrace("Cache hit for {TenantId}", tenantId);
            return JsonConvert.DeserializeObject<TenantConnection>(cachedSerializedItem);
        }
        
        Logger.LogTrace("Cache fail for {TenantId}", tenantId);
        
        return null;
    }

    public bool TryGetItem(Guid tenantId, out TenantConnection? item)
    {
        item = GetItem(tenantId);

        return item != null;
    }

    public void SetItem(Guid tenantId, TenantConnection value)
    {
        Cache.SetString(tenantId.ToString(), JsonConvert.SerializeObject(value),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(Options.CacheExpirationHours)
            });
        
        Logger.LogTrace("Cache update for {TenantId}", tenantId);
    }

    public void PurgeItem(Guid tenantId)
    {
        Cache.Remove(tenantId.ToString());

        Logger.LogTrace("Cache purge for {TenantId}", tenantId);
    }
}