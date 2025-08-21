using Ballware.Generic.Data.Persistables;
using Ballware.Shared.Data.Ef;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Generic.Data.Ef;

public interface ITenantDbContext : IDbContext
{
    Task MigrateDatabaseAsync(CancellationToken cancellationToken);
    
    DbSet<TenantConnection> TenantConnections { get; }
    DbSet<TenantEntity> TenantEntities { get; }
}