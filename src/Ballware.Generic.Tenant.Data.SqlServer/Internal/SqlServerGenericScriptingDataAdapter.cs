using System.Data;
using Ballware.Generic.Scripting;
using Ballware.Generic.Metadata;
using Dapper;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerGenericScriptingDataAdapter : ITenantDataAdapter
{
    private SqlServerGenericProvider GenericProvider { get; }
    
    public SqlServerGenericScriptingDataAdapter(SqlServerGenericProvider genericProvider)
    {
        GenericProvider = genericProvider;
    }
    
    public IEnumerable<dynamic> RawQuery(IDbConnection db, IDbTransaction? transaction, string table, string columns, string where, object p)
    {
        return db.Query<dynamic>($"SELECT {columns} FROM {table} WHERE {where}", p, transaction);
    }

    public int RawCount(IDbConnection db, IDbTransaction? transaction, string table, string where, object p)
    {
        return db.ExecuteScalar<int>($"SELECT COUNT(*) FROM {table} WHERE {where}", p, transaction);
    }

    public void RawDelete(IDbConnection db, IDbTransaction transaction, string table, string where, object p)
    {
        db.Execute($"DELETE FROM {table} WHERE {where}", p, transaction);
    }

    public void RawInsert(IDbConnection db, IDbTransaction transaction, string table, string columns, string values, object p)
    {
        db.Execute($"INSERT INTO {table} ({columns}) VALUES ({values})", p, transaction);
    }

    public void RawUpdate(IDbConnection db, IDbTransaction transaction, string table, string columns, string where, object p)
    {
        db.Execute($"UPDATE {table} SET {columns} WHERE {where}", p, transaction);
    }

    public object? QueryScalarValue(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, IDictionary<string, object> claims,
        string column, IDictionary<string, object> p)
    {
        var result = QuerySingle(db, transaction, tenant, entity, claims, entity.ScalarValueQuery ?? "primary", p);

        if (result is IDictionary<string, object> resultDict && resultDict.TryGetValue(column, out object? value))
        {
            return value;
        }

        return null;
    }

    public long Count(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, IDictionary<string, object> claims, string queryIdentifier,
        IDictionary<string, object> p)
    {
        return GenericProvider.ProcessCountAsync(new SqlServerGenericProcessingContext(db, transaction, tenant, entity), queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public IEnumerable<dynamic> QueryList(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, IDictionary<string, object> claims, 
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessQueryListAsync<dynamic>(new SqlServerGenericProcessingContext(db, transaction, tenant, entity), queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public dynamic? QuerySingle(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, IDictionary<string, object> claims,
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessQuerySingleAsync<dynamic>(new SqlServerGenericProcessingContext(db, transaction, tenant, entity), queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public dynamic? QueryNew(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, IDictionary<string, object> claims,
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessNewAsync<dynamic>(new SqlServerGenericProcessingContext(db, transaction, tenant, entity), queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public void Save(IDbConnection db, IDbTransaction transaction, Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims,
        string statementIdentifier, IDictionary<string, object> p)
    {
        GenericProvider.ProcessSaveAsync(new SqlServerGenericProcessingContext(db, transaction, tenant, entity), userId, statementIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public (bool Result, IEnumerable<string> Messages) Remove(IDbConnection db, IDbTransaction transaction, Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims,
        IDictionary<string, object> p)
    {
        var result = GenericProvider.ProcessRemoveAsync(new SqlServerGenericProcessingContext(db, transaction, tenant, entity), userId, claims, p).GetAwaiter().GetResult(); 
        
        return (result.Result, result.Messages); 
    }
}