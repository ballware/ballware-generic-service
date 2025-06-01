using System.Data;
using Ballware.Generic.Metadata;
using Dapper;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerMlModelProvider : ITenantMlModelProvider
{
    private ITenantStorageProvider StorageProvider { get; }
    
    public SqlServerMlModelProvider(ITenantStorageProvider storageProvider)
    {
        StorageProvider = storageProvider;
    }
    
    public async Task<IEnumerable<T>> TrainDataByModelAsync<T>(Metadata.Tenant tenant, MlModel model)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);

        return await ProcessTrainDataByModelAsync<T>(db, null, tenant, model);
    }
    
    public async Task<IEnumerable<T>> ProcessTrainDataByModelAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, MlModel model)
    {
        return await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, model.TrainSql,
            TenantPlaceholderOptions.Create()), transaction);
    }
}