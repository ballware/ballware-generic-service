using Ballware.Generic.Data.Caching;
using Ballware.Generic.Data.Ef.Configuration;
using Ballware.Generic.Data.Ef.Model;
using Ballware.Generic.Data.Ef.Repository;
using Ballware.Generic.Data.Ef.SqlServer.Internal;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Ballware.Shared.Data.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Data.Ef.SqlServer;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareTenantStorageForSqlServer(this IServiceCollection services, StorageOptions options, string connectionString)
    {
        services.AddSingleton(options);
        services.AddDbContext<TenantDbContext>(o =>
        {
            o.UseSqlServer(connectionString, o =>
            {
                o.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName);
            });

            o.ReplaceService<IModelCustomizer, TenantModelBaseCustomizer>();
        });

        services.AddScoped<ITenantDbContext, TenantDbContext>();

        services.AddScoped<TenantConnectionBaseRepository>();
        
        if (options.EnableCaching)
        {
            services.AddScoped<IRepository<TenantConnection>, CachableTenantConnectionRepository<TenantConnectionBaseRepository>>();
            services.AddScoped<ITenantConnectionRepository, CachableTenantConnectionRepository<TenantConnectionBaseRepository>>(); 
        }
        else
        {
            services.AddScoped<IRepository<TenantConnection>, TenantConnectionBaseRepository>();
            services.AddScoped<ITenantConnectionRepository, TenantConnectionBaseRepository>();    
        }
        
        services.AddScoped<ITenantableRepository<TenantEntity>, TenantEntityBaseRepository>();
        services.AddScoped<ITenantEntityRepository, TenantEntityBaseRepository>();

        services.AddHostedService<InitializationWorker>();

        return services;
    }
}