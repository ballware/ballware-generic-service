using System.Reflection.Metadata;
using Ballware.Generic.Data.Ef.Model;
using Ballware.Generic.Data.Persistables;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ballware.Generic.Data.Ef.Postgres.Model;

public class PostgresTenantModelCustomizer : TenantModelBaseCustomizer
{
    public PostgresTenantModelCustomizer(ModelCustomizerDependencies dependencies) 
        : base(dependencies)
    {
    }
    
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        
        modelBuilder.Entity<TenantConnection>().ToTable("tenant_connection");
        modelBuilder.Entity<TenantEntity>().ToTable("tenant_entity");
    }
}