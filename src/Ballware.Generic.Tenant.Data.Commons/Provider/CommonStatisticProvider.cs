using System.Data;
using Ballware.Generic.Metadata;
using Ballware.Generic.Scripting;
using Microsoft.Extensions.DependencyInjection;
using Dapper;

namespace Ballware.Generic.Tenant.Data.Commons.Provider;

public abstract class CommonStatisticProvider : ITenantStatisticProvider
{
    protected abstract string TenantVariableIdentifier { get; }
    
    private ITenantStorageProvider StorageProvider { get; }
    private IStatisticScriptingExecutor ScriptingExecutor { get; }
    
    public CommonStatisticProvider(ITenantStorageProvider storageProvider, IServiceProvider serviceProvider)
    {
        StorageProvider = storageProvider;
        ScriptingExecutor = serviceProvider.GetRequiredService<IStatisticScriptingExecutor>();
    }
    
    public async Task<IEnumerable<T>> FetchDataAsync<T>(Metadata.Tenant tenant, Statistic statistic, Guid userId, IDictionary<string, object> claims, IDictionary<string, object> queryParams)
    {
        using var db = await StorageProvider.OpenConnectionAsync(tenant.Id);

        return await ProcessFetchDataAsync<T>(db, null, tenant, statistic, userId, claims, queryParams);
    }
    
    public async Task<IEnumerable<T>> ProcessFetchDataAsync<T>(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Statistic statistic, Guid userId, IDictionary<string, object> claims, IDictionary<string, object> p)
    {
        var queryParams = new Dictionary<string, object>(p);

        queryParams[TenantVariableIdentifier] = tenant.Id;
        
        var data = await db.QueryAsync<T>(await StorageProvider.ApplyTenantPlaceholderAsync(tenant.Id, statistic.FetchSql,
                TenantPlaceholderOptions.Create()),
            queryParams);

        return ScriptingExecutor.FetchScript(db, transaction, tenant, statistic, userId, claims, data);
    }
}