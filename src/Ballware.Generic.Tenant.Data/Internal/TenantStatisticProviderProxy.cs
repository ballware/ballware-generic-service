using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantStatisticProviderProxy : ITenantStatisticProvider
{
    private IProviderRegistry ProviderRegistry { get; }

    public TenantStatisticProviderProxy(IProviderRegistry providerRegistry)
    {
        ProviderRegistry = providerRegistry;
    }

    public async Task<IEnumerable<dynamic>> FetchDataAsync(Metadata.Tenant tenant, Statistic statistic, Guid userId, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        var provider = ProviderRegistry.GetStatisticProvider(tenant.Provider);
        
        return await provider.FetchDataAsync(tenant, statistic, userId, claims, queryParams);
    }
}