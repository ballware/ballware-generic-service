using System.Data;
using Ballware.Generic.Scripting;
using Ballware.Generic.Metadata;
using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresGenericScriptingDataAdapter : ITenantDataAdapter
{
    private PostgresGenericProvider GenericProvider { get; }

    private const string OperationNotSupportedExceptionText =
        "Operation deprecated and not supported on PosgreSQL storage layer";
    
    public PostgresGenericScriptingDataAdapter(PostgresGenericProvider genericProvider)
    {
        GenericProvider = genericProvider;
    }
    
    public IEnumerable<dynamic> RawQuery(IDbConnection db, IDbTransaction? transaction, string table, string columns, string where, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public int RawCount(IDbConnection db, IDbTransaction? transaction, string table, string where, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public void RawDelete(IDbConnection db, IDbTransaction transaction, string table, string where, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public void RawInsert(IDbConnection db, IDbTransaction transaction, string table, string columns, string values, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public void RawUpdate(IDbConnection db, IDbTransaction transaction, string table, string columns, string where, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
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
        return GenericProvider.ProcessCountAsync(new GenericProcessingContext(db, transaction, tenant, entity), queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public IEnumerable<dynamic> QueryList(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, IDictionary<string, object> claims, 
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessQueryListAsync<dynamic>(new GenericProcessingContext(db, transaction, tenant, entity), queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public dynamic? QuerySingle(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, IDictionary<string, object> claims,
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessQuerySingleAsync<dynamic>(new GenericProcessingContext(db, transaction, tenant, entity), queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public dynamic? QueryNew(IDbConnection db, IDbTransaction? transaction, Metadata.Tenant tenant, Entity entity, IDictionary<string, object> claims,
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessNewAsync<dynamic>(new GenericProcessingContext(db, transaction, tenant, entity), queryIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public void Save(IDbConnection db, IDbTransaction transaction, Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims,
        string statementIdentifier, IDictionary<string, object> p)
    {
        GenericProvider.ProcessSaveAsync(new GenericProcessingContext(db, transaction, tenant, entity), userId, statementIdentifier, claims, p).GetAwaiter().GetResult();
    }

    public (bool Result, IEnumerable<string> Messages) Remove(IDbConnection db, IDbTransaction transaction, Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims,
        IDictionary<string, object> p)
    {
        var result = GenericProvider.ProcessRemoveAsync(new GenericProcessingContext(db, transaction, tenant, entity), userId, claims, p).GetAwaiter().GetResult(); 
        
        return (result.Result, result.Messages); 
    }
}