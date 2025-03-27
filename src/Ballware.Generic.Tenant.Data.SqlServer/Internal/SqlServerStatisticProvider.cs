using System.Data;
using Ballware.Generic.Metadata;
using Ballware.Generic.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Dapper;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerStatisticProvider : ITenantStatisticProvider
{
    private ITenantStorageProvider StorageProvider { get; }
    private IStatisticScriptingExecutor ScriptingExecutor { get; }
    
    public SqlServerStatisticProvider(ITenantStorageProvider storageProvider, IServiceProvider serviceProvider)
    {
        StorageProvider = storageProvider;
        ScriptingExecutor = serviceProvider.GetRequiredService<IStatisticScriptingExecutor>();
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

    public async Task<IEnumerable<dynamic>> FetchDataAsync(Metadata.Tenant tenant, Statistic statistic, Guid userId, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);

        return await ProcessFetchDataAsync(db, null, tenant, statistic, userId, claims, queryParams);
    }
    
    public async Task<IEnumerable<dynamic>> ProcessFetchDataAsync(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Statistic statistic, Guid userId, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        var data = await db.QueryAsync<dynamic>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, statistic.FetchSql,
                TenantPlaceholderOptions.Create()),
            queryParams);

        return ScriptingExecutor.FetchScript(db, transaction, tenant, statistic, userId, claims, data);
    }
}