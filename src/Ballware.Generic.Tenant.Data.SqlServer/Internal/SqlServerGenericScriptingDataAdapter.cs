using System.Data;
using Ballware.Generic.Scripting;
using Ballware.Meta.Client;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

public class SqlServerGenericScriptingDataAdapter : ITenantDataAdapter
{
    public IEnumerable<dynamic> RawQuery(IDbConnection db, string table, string columns, string where, object p)
    {
        throw new NotImplementedException();
    }

    public int RawCount(IDbConnection db, string table, string where, object p)
    {
        throw new NotImplementedException();
    }

    public void RawDelete(IDbConnection db, string table, string where, object p)
    {
        throw new NotImplementedException();
    }

    public void RawInsert(IDbConnection db, string table, string columns, string values, object p)
    {
        throw new NotImplementedException();
    }

    public void RawUpdate(IDbConnection db, string table, string columns, string where, object p)
    {
        throw new NotImplementedException();
    }

    public object QueryScalarValue(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata,
        string column, IDictionary<string, object> p)
    {
        throw new NotImplementedException();
    }

    public long Count(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata, string queryIdentifier,
        IDictionary<string, object> p)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<dynamic> QueryList(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata,
        string queryIdentifier, IDictionary<string, object> p)
    {
        throw new NotImplementedException();
    }

    public dynamic QuerySingle(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata,
        string queryIdentifier, IDictionary<string, object> p)
    {
        throw new NotImplementedException();
    }

    public dynamic QueryNew(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata,
        string queryIdentifier, IDictionary<string, object> p)
    {
        throw new NotImplementedException();
    }

    public void Save(IDbConnection db, ServiceTenant tenant, Guid? userId, Dictionary<string, object> claims, ServiceEntity metadata,
        string statementIdentifier, IDictionary<string, object> p)
    {
        throw new NotImplementedException();
    }

    public (bool Result, IEnumerable<string> Messages) Remove(IDbConnection db, ServiceTenant tenant, Guid? userId, Dictionary<string, object> claims,
        ServiceEntity metadata, IDictionary<string, object> p)
    {
        throw new NotImplementedException();
    }
}