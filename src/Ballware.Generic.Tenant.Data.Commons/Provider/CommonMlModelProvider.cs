using System.Data;
using Ballware.Generic.Metadata;
using Dapper;

namespace Ballware.Generic.Tenant.Data.Commons.Provider;

public abstract class CommonMlModelProvider : ITenantMlModelProvider
{
    protected abstract string TenantVariableIdentifier { get; }
    
    private ITenantStorageProvider StorageProvider { get; }
    
    public CommonMlModelProvider(ITenantStorageProvider storageProvider)
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
        var queryParams = new Dictionary<string, object>();

        queryParams[TenantVariableIdentifier] = tenant.Id;
        
        return await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, model.TrainSql,
            TenantPlaceholderOptions.Create()), queryParams, transaction);
    }
}