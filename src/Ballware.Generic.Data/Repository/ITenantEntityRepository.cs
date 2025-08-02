using Ballware.Shared.Data.Repository;

namespace Ballware.Generic.Data.Repository;

public interface ITenantEntityRepository : ITenantableRepository<Public.TenantEntity>
{
    Task<Public.TenantEntity?> ByEntityAsync(Guid tenantId, string entity);
}