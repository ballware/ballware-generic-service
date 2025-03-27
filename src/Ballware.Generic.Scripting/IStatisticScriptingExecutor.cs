using System.Data;
using Ballware.Generic.Metadata;

namespace Ballware.Generic.Scripting;

public interface IStatisticScriptingExecutor
{
    public IEnumerable<dynamic> FetchScript(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Statistic statistic, Guid userId, IDictionary<string, object> claims, IEnumerable<dynamic> results);

}