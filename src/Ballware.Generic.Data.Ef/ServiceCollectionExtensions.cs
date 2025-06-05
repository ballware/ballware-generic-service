using Ballware.Generic.Data.Ef.Configuration;
using Ballware.Generic.Data.Ef.Internal;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Data.Repository;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Data.Ef;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareTenantStorage(this IServiceCollection services, StorageOptions options, string connectionString)
    {
        services.AddSingleton(options);
        services.AddDbContext<TenantDbContext>(o =>
        {
            o.UseSqlServer(connectionString, o =>
            {
                o.MigrationsAssembly(typeof(TenantDbContext).Assembly.FullName);
            });

        });

        if (options.EnableCaching)
        {
            services.AddScoped<IRepository<TenantConnection>, CachableTenantConnectionRepository>();
            services.AddScoped<ITenantConnectionRepository, CachableTenantConnectionRepository>(); 
        }
        else
        {
            services.AddScoped<IRepository<TenantConnection>, TenantConnectionRepository>();
            services.AddScoped<ITenantConnectionRepository, TenantConnectionRepository>();    
        }
        
        services.AddScoped<IRepository<TenantEntity>, TenantEntityRepository>();
        services.AddScoped<ITenantEntityRepository, TenantEntityRepository>();

        services.AddHostedService<InitializationWorker>();

        return services;
    }
}