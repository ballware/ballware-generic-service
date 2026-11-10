using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantMlModelProviderProxy : ITenantMlModelProvider
{
    private IProviderRegistry ProviderRegistry { get; }

    public TenantMlModelProviderProxy(IProviderRegistry providerRegistry)
    {
        ProviderRegistry = providerRegistry;
    }

    public async Task<IEnumerable<T>> TrainDataByPlainQueryAsync<T>(Metadata.Tenant tenant, string query)
    {
        var provider = ProviderRegistry.GetMlModelProvider(tenant.Provider);
        
        return await provider.TrainDataByPlainQueryAsync<T>(tenant, query);
    }
}