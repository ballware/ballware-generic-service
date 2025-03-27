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
    
    public void RegisterLookupProvider<TProvider>(string providerName) where TProvider : ITenantLookupProvider
    {
        ProviderConfiguration.RegisterLookupProvider<TProvider>(providerName);
    }
    
    public void RegisterMlModelProvider<TProvider>(string providerName) where TProvider : ITenantMlModelProvider
    {
        ProviderConfiguration.RegisterMlModelProvider<TProvider>(providerName);
    }
    
    public void RegisterStatisticProvider<TProvider>(string providerName) where TProvider : ITenantStatisticProvider
    {
        ProviderConfiguration.RegisterStatisticProvider<TProvider>(providerName);
    }
}