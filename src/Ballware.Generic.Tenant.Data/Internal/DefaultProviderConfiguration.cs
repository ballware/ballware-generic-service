namespace Ballware.Generic.Tenant.Data.Internal;

class DefaultProviderConfiguration
{
    private Dictionary<string, Type> StorageProviders { get; } = new();
    private Dictionary<string, Type> GenericProviders { get; } = new();
    
    public void RegisterStorageProvider<TProvider>(string providerName) where TProvider : ITenantStorageProvider
    {
        lock (StorageProviders)
        {
            StorageProviders[providerName] = typeof(TProvider);
        }
    }

    public ITenantStorageProvider GetStorageProvider(string providerName, IServiceProvider serviceProvider)
    {
        lock (StorageProviders)
        {
            if (StorageProviders.ContainsKey(providerName))
            {
                var serviceInstance = serviceProvider.GetService(GenericProviders[providerName]);

                if (serviceInstance is not ITenantStorageProvider storageProvider)
                {
                    throw new InvalidOperationException($"Unable to resolve provider '{providerName}'.");
                }

                return storageProvider;
            }
        }

        throw new KeyNotFoundException($"No storage provider found for provider {providerName}");
    }
    
    public void RegisterGenericProvider<TProvider>(string providerName) where TProvider : ITenantGenericProvider
    {
        lock (GenericProviders)
        {
            GenericProviders[providerName] = typeof(TProvider);
        }
    }

    public ITenantGenericProvider GetGenericProvider(string providerName, IServiceProvider serviceProvider)
    {
        lock (GenericProviders)
        {
            if (GenericProviders.ContainsKey(providerName))
            {
                var serviceInstance = serviceProvider.GetService(GenericProviders[providerName]);

                if (serviceInstance is not ITenantGenericProvider genericProvider)
                {
                    throw new InvalidOperationException($"Unable to resolve provider '{providerName}'.");
                }

                return genericProvider;
            }
        }

        throw new KeyNotFoundException($"No generic provider found for provider {providerName}");
    }
}