using Ballware.Generic.Metadata;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantMlModelProvider
{
    Task<IEnumerable<T>> TrainDataByModelAsync<T>(Metadata.Tenant tenant, MlModel model);
    Task<IEnumerable<T>> TrainDataByPlainQueryAsync<T>(Metadata.Tenant tenant, string query);
}