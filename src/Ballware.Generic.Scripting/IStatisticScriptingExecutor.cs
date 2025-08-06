using System.Data;
using Ballware.Generic.Metadata;

namespace Ballware.Generic.Scripting;

public interface IStatisticScriptingExecutor
{
    public IEnumerable<T> FetchScript<T>(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Statistic statistic, Guid userId, IDictionary<string, object> claims, IEnumerable<T> results);

}