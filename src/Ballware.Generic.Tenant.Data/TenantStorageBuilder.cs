using Ballware.Generic.Tenant.Data.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data;

public class TenantStorageBuilder
{
    internal DefaultProviderConfiguration ProviderConfiguration { get; }
    
    public IServiceCollection Services { get; }
    
    internal TenantStorageBuilder(IServiceCollection services, DefaultProviderConfiguration configuration)
    {
        ProviderConfiguration = configuration;
        Services = services;
    }
    
    public void RegisterStorageProvider<TProvider>(string providerName) where TProvider : ITenantStorageProvider
    {
        ProviderConfiguration.RegisterStorageProvider<TProvider>(providerName);
    }
    
    public void RegisterGenericProvider<TProvider>(string providerName) where TProvider : ITenantGenericProvider
    {
        ProviderConfiguration.RegisterGenericProvider<TProvider>(providerName);
    }
}