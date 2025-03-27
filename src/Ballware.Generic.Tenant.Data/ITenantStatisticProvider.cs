using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantStatisticProvider
{
    public Task<IEnumerable<dynamic>> FetchDataAsync(Metadata.Tenant tenant, Statistic statistic, Guid userId,
        IDictionary<string, object> claims, IDictionary<string, object> queryParams);
}