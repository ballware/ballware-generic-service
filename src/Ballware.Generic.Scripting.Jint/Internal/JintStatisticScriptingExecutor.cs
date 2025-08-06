using System.Data;
using Ballware.Generic.Metadata;
using Jint;

namespace Ballware.Generic.Scripting.Jint.Internal;

class JintStatisticScriptingExecutor : IStatisticScriptingExecutor
{
    private IMlAdapter MlAdapter { get; }

    public JintStatisticScriptingExecutor(IMlAdapter mlAdapter)
    {
        MlAdapter = mlAdapter;
    }
    
    public IEnumerable<T> FetchScript<T>(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Statistic statistic, Guid userId, IDictionary<string, object> claims,
        IEnumerable<T> results)
    {
        if (!string.IsNullOrEmpty(statistic.FetchScript))
        {
            return results.Select(item =>
            {
                new Engine()
                    .SetValue("identifier", statistic.Identifier)
                    .SetJsonFunctions()
                    .SetClaimFunctions(claims)
                    .SetMlFunctions(tenant.Id, userId, MlAdapter)
                    .SetValue("item", item)
                    .SetValue("addProperty",
                        new Action<string, object>((prop, value) =>
                        {
                            (item as IDictionary<string, object>).Add(prop, value);
                        }))
                    .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" +
                              statistic.FetchScript);
                return item;
            });
        }

        return results;
    }
}