using System.Data;
using Ballware.Generic.Metadata;

namespace Ballware.Generic.Scripting
{
    public interface ITenantDataAdapter
    {
        IEnumerable<dynamic> RawQuery(IDbConnection db, IDbTransaction? transaction, string table, string columns, string where, object p);
        int RawCount(IDbConnection db, IDbTransaction? transaction, string table, string where, object p);
        void RawDelete(IDbConnection db, IDbTransaction transaction, string table, string where, object p);
        void RawInsert(IDbConnection db, IDbTransaction transaction, string table, string columns, string values, object p);
        void RawUpdate(IDbConnection db, IDbTransaction transaction, string table, string columns, string where, object p);

        object? QueryScalarValue(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Entity entity, IDictionary<string, object> claims, string column, IDictionary<string, object> p);
        long Count(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Entity entity, IDictionary<string, object> claims, string queryIdentifier, IDictionary<string, object> p);
        IEnumerable<dynamic> QueryList(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Entity entity, IDictionary<string, object> claims, string queryIdentifier, IDictionary<string, object> p);
        dynamic? QuerySingle(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Entity entity, IDictionary<string, object> claims, string queryIdentifier, IDictionary<string, object> p);
        dynamic? QueryNew(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Entity entity, IDictionary<string, object> claims, string queryIdentifier, IDictionary<string, object> p);
        void Save(IDbConnection db, IDbTransaction transaction, Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, string statementIdentifier, IDictionary<string, object> p);
        (bool Result, IEnumerable<string> Messages) Remove(IDbConnection db, IDbTransaction transaction, Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, IDictionary<string, object> p);
    }
}