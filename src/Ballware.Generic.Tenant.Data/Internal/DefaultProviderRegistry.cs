namespace Ballware.Generic.Tenant.Data.Internal;

class DefaultProviderRegistry : IProviderRegistry
{
    private DefaultProviderConfiguration Configuration { get; }
    private IServiceProvider ServiceProvider { get; }
    
    public DefaultProviderRegistry(IServiceProvider serviceProvider, DefaultProviderConfiguration configuration)
    {
        ServiceProvider = serviceProvider;
        Configuration = configuration;
    }
    
    public ITenantStorageProvider GetStorageProvider(string providerName)
    {
        return Configuration.GetStorageProvider(providerName, ServiceProvider);
    }
    
    public ITenantGenericProvider GetGenericProvider(string providerName)
    {
        return Configuration.GetGenericProvider(providerName, ServiceProvider);
    }

    public ITenantSchemaProvider GetSchemaProvider(string providerName)
    {
        return Configuration.GetSchemaProvider(providerName, ServiceProvider);
    }

    public ITenantLookupProvider GetLookupProvider(string providerName)
    {
        return Configuration.GetLookupProvider(providerName, ServiceProvider);
    }

    public ITenantMlModelProvider GetMlModelProvider(string providerName)
    {
        return Configuration.GetMlModelProvider(providerName, ServiceProvider);
    }
    
    public ITenantStatisticProvider GetStatisticProvider(string providerName)
    {
        return Configuration.GetStatisticProvider(providerName, ServiceProvider);
    }
}