using Ballware.Generic.Data.Persistables;
using Ballware.Shared.Data.Ef;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ballware.Generic.Data.Ef.Postgres;

public class TenantDbContext : DbContext, ITenantDbContext
{
    private ILoggerFactory LoggerFactory { get; }
    
    public TenantDbContext(DbContextOptions<TenantDbContext> options, ILoggerFactory loggerFactory) : base(options)
    {
        LoggerFactory = loggerFactory;
    }

    public DbSet<TenantConnection> TenantConnections { get; set; }
    public DbSet<TenantEntity> TenantEntities { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseLoggerFactory(LoggerFactory);
        
        base.OnConfiguring(optionsBuilder);
    }
}