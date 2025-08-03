using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantStatisticProvider
{
    public Task<IEnumerable<T>> FetchDataAsync<T>(Metadata.Tenant tenant, Statistic statistic, Guid userId,
        IDictionary<string, object> claims, IDictionary<string, object> queryParams);
}