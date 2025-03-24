namespace Ballware.Generic.Data.Repository;

public interface ITenantEntityRepository : IRepository<Public.TenantEntity>
{
    Task<Public.TenantEntity?> ByEntityAsync(Guid tenant, string entity);
}