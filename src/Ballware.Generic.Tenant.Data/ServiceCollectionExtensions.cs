using Ballware.Generic.Tenant.Data.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareTenantStorage(this IServiceCollection services,
        Action<TenantStorageBuilder>? configureOptions = null)
    {
        services.AddSingleton<IProviderRegistry, DefaultProviderRegistry>();
        services.AddSingleton<ITenantStorageProvider, TenantStorageProviderProxy>();
        services.AddSingleton<ITenantGenericProvider, TenantGenericProviderProxy>();

        var defaultProviderConfiguration = new DefaultProviderConfiguration();
        var tenantStorageBuilder = new TenantStorageBuilder(services, defaultProviderConfiguration);
        
        if (configureOptions != null)
        {
            configureOptions(tenantStorageBuilder);
        }
        
        services.AddSingleton<DefaultProviderConfiguration>(defaultProviderConfiguration);

        return services;
    }
}