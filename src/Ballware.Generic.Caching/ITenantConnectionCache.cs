using Ballware.Generic.Data.Public;

namespace Ballware.Generic.Caching;

public interface ITenantConnectionCache
{
    TenantConnection? GetItem(Guid tenantId);
    bool TryGetItem(Guid tenantId, out TenantConnection? item);
    void SetItem(Guid tenantId, TenantConnection value);
    void PurgeItem(Guid tenantId);
}