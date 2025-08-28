using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBallwareTenantGenericStorage(this IServiceCollection services,
        Action<TenantStorageBuilder>? configureOptions = null)
    {
        services.AddScoped<IProviderRegistry, DefaultProviderRegistry>();
        services.AddScoped<ITenantStorageProvider, TenantStorageProviderProxy>();
        services.AddScoped<ITenantGenericProvider, TenantGenericProviderProxy>();
        services.AddScoped<ITenantLookupProvider, TenantLookupProviderProxy>();
        services.AddScoped<ITenantMlModelProvider, TenantMlModelProviderProxy>();
        services.AddScoped<ITenantStatisticProvider, TenantStatisticProviderProxy>();
        services.AddScoped<ITenantSchemaProvider, TenantSchemaProviderProxy>();
        services.AddScoped<IScriptingTenantDataProvider, TenantScriptingDataProviderProxy>();

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