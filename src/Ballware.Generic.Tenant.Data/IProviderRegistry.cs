namespace Ballware.Generic.Tenant.Data;

public interface IProviderRegistry
{
    ITenantStorageProvider GetStorageProvider(string providerName);
    
    ITenantGenericProvider GetGenericProvider(string providerName);
    
    ITenantSchemaProvider GetSchemaProvider(string providerName);
    
    ITenantLookupProvider GetLookupProvider(string providerName);
    
    ITenantMlModelProvider GetMlModelProvider(string providerName);
    
    ITenantStatisticProvider GetStatisticProvider(string providerName);
}