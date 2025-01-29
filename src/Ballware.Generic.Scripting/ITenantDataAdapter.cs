using System.Data;
using Ballware.Meta.Client;

namespace Ballware.Generic.Scripting
{
    public interface ITenantDataAdapter
    {
        IEnumerable<dynamic> RawQuery(IDbConnection db, string table, string columns, string where, object p);
        int RawCount(IDbConnection db, string table, string where, object p);
        void RawDelete(IDbConnection db, string table, string where, object p);
        void RawInsert(IDbConnection db, string table, string columns, string values, object p);
        void RawUpdate(IDbConnection db, string table, string columns, string where, object p);

        object QueryScalarValue(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata, string column, IDictionary<string, object> p);
        long Count(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata, string queryIdentifier, IDictionary<string, object> p);
        IEnumerable<dynamic> QueryList(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata, string queryIdentifier, IDictionary<string, object> p);
        dynamic QuerySingle(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata, string queryIdentifier, IDictionary<string, object> p);
        dynamic QueryNew(IDbConnection db, ServiceTenant tenant, Dictionary<string, object> claims, ServiceEntity metadata, string queryIdentifier, IDictionary<string, object> p);
        void Save(IDbConnection db, ServiceTenant tenant, Guid? userId, Dictionary<string, object> claims, ServiceEntity metadata, string statementIdentifier, IDictionary<string, object> p);
        (bool Result, IEnumerable<string> Messages) Remove(IDbConnection db, ServiceTenant tenant, Guid? userId, Dictionary<string, object> claims, ServiceEntity metadata, IDictionary<string, object> p);
    }
}