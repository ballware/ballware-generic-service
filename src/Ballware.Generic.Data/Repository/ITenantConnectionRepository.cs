using Ballware.Shared.Data.Repository;

namespace Ballware.Generic.Data.Repository;

public interface ITenantConnectionRepository : IRepository<Public.TenantConnection>
{
    Task<Public.TenantConnection?> ByIdAsync(Guid id);
}