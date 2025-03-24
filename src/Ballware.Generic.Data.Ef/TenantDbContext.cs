using Ballware.Generic.Data.Persistables;
using Microsoft.EntityFrameworkCore;

namespace Ballware.Generic.Data.Ef;

public class TenantDbContext : DbContext
{
    public TenantDbContext(DbContextOptions<TenantDbContext> options) : base(options)
    {
    }

    public DbSet<TenantConnection> TenantConnections { get; set; }
    public DbSet<TenantEntity> TenantEntities { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantConnection>().HasKey(d => d.Id);
        modelBuilder.Entity<TenantConnection>().HasIndex(d => d.Uuid).IsUnique();
        
        modelBuilder.Entity<TenantEntity>().HasKey(d => d.Id);
        modelBuilder.Entity<TenantEntity>().HasIndex(d => new { d.TenantId, d.Uuid }).IsUnique();
        modelBuilder.Entity<TenantEntity>().HasIndex(d => new { d.TenantId, d.Entity }).IsUnique();
    }
}