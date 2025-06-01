using Microsoft.Extensions.DependencyInjection;

namespace Ballware.Generic.Tenant.Data.Internal;

class DefaultProviderConfiguration
{
    private Dictionary<string, Type> StorageProviders { get; } = new();
    private Dictionary<string, Type> GenericProviders { get; } = new();
    private Dictionary<string, Type> SchemaProviders { get; } = new();
    private Dictionary<string, Type> LookupProviders { get; } = new();
    private Dictionary<string, Type> MlModelProviders { get; } = new();
    private Dictionary<string, Type> StatisticProviders { get; } = new();
    
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
                var serviceInstance = serviceProvider.GetRequiredService(StorageProviders[providerName]);

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
                var serviceInstance = serviceProvider.GetRequiredService(GenericProviders[providerName]);

                if (serviceInstance is not ITenantGenericProvider genericProvider)
                {
                    throw new InvalidOperationException($"Unable to resolve provider '{providerName}'.");
                }

                return genericProvider;
            }
        }

        throw new KeyNotFoundException($"No generic provider found for provider {providerName}");
    }
    
    public void RegisterSchemaProvider<TProvider>(string providerName) where TProvider : ITenantSchemaProvider
    {
        lock (SchemaProviders)
        {
            SchemaProviders[providerName] = typeof(TProvider);
        }
    }

    public ITenantSchemaProvider GetSchemaProvider(string providerName, IServiceProvider serviceProvider)
    {
        lock (SchemaProviders)
        {
            if (SchemaProviders.ContainsKey(providerName))
            {
                var serviceInstance = serviceProvider.GetRequiredService(SchemaProviders[providerName]);

                if (serviceInstance is not ITenantSchemaProvider genericProvider)
                {
                    throw new InvalidOperationException($"Unable to resolve provider '{providerName}'.");
                }

                return genericProvider;
            }
        }

        throw new KeyNotFoundException($"No schema provider found for provider {providerName}");
    }
    
    public void RegisterLookupProvider<TProvider>(string providerName) where TProvider : ITenantLookupProvider
    {
        lock (LookupProviders)
        {
            LookupProviders[providerName] = typeof(TProvider);
        }
    }

    public ITenantLookupProvider GetLookupProvider(string providerName, IServiceProvider serviceProvider)
    {
        lock (LookupProviders)
        {
            if (LookupProviders.ContainsKey(providerName))
            {
                var serviceInstance = serviceProvider.GetRequiredService(LookupProviders[providerName]);

                if (serviceInstance is not ITenantLookupProvider lookupProvider)
                {
                    throw new InvalidOperationException($"Unable to resolve provider '{providerName}'.");
                }

                return lookupProvider;
            }
        }

        throw new KeyNotFoundException($"No lookup provider found for provider {providerName}");
    }
    
    public void RegisterMlModelProvider<TProvider>(string providerName) where TProvider : ITenantMlModelProvider
    {
        lock (MlModelProviders)
        {
            MlModelProviders[providerName] = typeof(TProvider);
        }
    }

    public ITenantMlModelProvider GetMlModelProvider(string providerName, IServiceProvider serviceProvider)
    {
        lock (MlModelProviders)
        {
            if (MlModelProviders.ContainsKey(providerName))
            {
                var serviceInstance = serviceProvider.GetRequiredService(MlModelProviders[providerName]);

                if (serviceInstance is not ITenantMlModelProvider mlModelProvider)
                {
                    throw new InvalidOperationException($"Unable to resolve provider '{providerName}'.");
                }

                return mlModelProvider;
            }
        }

        throw new KeyNotFoundException($"No ml model provider found for provider {providerName}");
    }
    
    public void RegisterStatisticProvider<TProvider>(string providerName) where TProvider : ITenantStatisticProvider
    {
        lock (StatisticProviders)
        {
            StatisticProviders[providerName] = typeof(TProvider);
        }
    }

    public ITenantStatisticProvider GetStatisticProvider(string providerName, IServiceProvider serviceProvider)
    {
        lock (StatisticProviders)
        {
            if (StatisticProviders.ContainsKey(providerName))
            {
                var serviceInstance = serviceProvider.GetRequiredService(StatisticProviders[providerName]);

                if (serviceInstance is not ITenantStatisticProvider statisticProvider)
                {
                    throw new InvalidOperationException($"Unable to resolve provider '{providerName}'.");
                }

                return statisticProvider;
            }
        }

        throw new KeyNotFoundException($"No statistic provider found for provider {providerName}");
    }
}