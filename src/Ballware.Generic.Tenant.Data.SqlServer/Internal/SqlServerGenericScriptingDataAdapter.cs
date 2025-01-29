using System.Data;
using Ballware.Generic.Scripting;
using Ballware.Meta.Client;
using Dapper;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

public class SqlServerGenericScriptingDataAdapter : ITenantDataAdapter
{
    private SqlServerGenericProvider GenericProvider { get; }
    
    public SqlServerGenericScriptingDataAdapter(SqlServerGenericProvider genericProvider)
    {
        GenericProvider = genericProvider;
    }
    
    public IEnumerable<dynamic> RawQuery(IDbConnection db, string table, string columns, string where, object p)
    {
        return db.Query<dynamic>($"SELECT {columns} FROM {table} WHERE {where}", p);
    }

    public int RawCount(IDbConnection db, string table, string where, object p)
    {
        return db.ExecuteScalar<int>($"SELECT COUNT(*) FROM {table} WHERE {where}", p);
    }

    public void RawDelete(IDbConnection db, string table, string where, object p)
    {
        db.Execute($"DELETE FROM {table} WHERE {where}", p);
    }

    public void RawInsert(IDbConnection db, string table, string columns, string values, object p)
    {
        db.Execute($"INSERT INTO {table} ({columns}) VALUES ({values})", p);
    }

    public void RawUpdate(IDbConnection db, string table, string columns, string where, object p)
    {
        db.Execute($"UPDATE {table} SET {columns} WHERE {where}", p);
    }

    public object? QueryScalarValue(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Dictionary<string, object> claims,
        string column, IDictionary<string, object> p)
    {
        var result = QuerySingle(db, tenant, entity, claims, entity.ScalarValueQuery ?? "primary", p);

        object? value = null;
        
        if (result is IDictionary<string, object> && result.TryGetValue(column, out value))
        {
            return value;
        }

        return null;
    }

    public long Count(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Dictionary<string, object> claims, string queryIdentifier,
        IDictionary<string, object> p)
    {
        return GenericProvider.ProcessCountAsync(db, tenant, entity, queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public IEnumerable<dynamic> QueryList(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Dictionary<string, object> claims, 
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessQueryListAsync<dynamic>(db, tenant, entity, queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public dynamic? QuerySingle(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Dictionary<string, object> claims,
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessQuerySingleAsync<dynamic>(db, tenant, entity, queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public dynamic? QueryNew(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Dictionary<string, object> claims,
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessNewAsync<dynamic>(db, tenant, entity, queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public void Save(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims,
        string statementIdentifier, IDictionary<string, object> p)
    {
        GenericProvider.ProcessSaveAsync(db, tenant, entity, userId, "primary", claims, p).GetAwaiter().GetResult();
    }

    public (bool Result, IEnumerable<string> Messages) Remove(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims,
        IDictionary<string, object> p)
    {
        var result = GenericProvider.ProcessRemoveAsync(db, tenant, entity, userId, claims, p).GetAwaiter().GetResult(); 
        
        return (result.Result, result.Messages); 
    }
}