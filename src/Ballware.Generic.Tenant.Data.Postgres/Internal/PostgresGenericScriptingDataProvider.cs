using System.Data;
using Ballware.Generic.Scripting;
using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresGenericScriptingDataProvider : IScriptingTenantDataProvider
{
    private PostgresGenericProvider GenericProvider { get; }

    private const string OperationNotSupportedExceptionText =
        "Operation deprecated and not supported on PosgreSQL storage layer";
    
    public PostgresGenericScriptingDataProvider(PostgresGenericProvider genericProvider)
    {
        GenericProvider = genericProvider;
    }
    
    public IEnumerable<dynamic> RawQuery(IScriptingEntityUserContext context, string table, string columns, string where, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public int RawCount(IScriptingEntityUserContext context, string table, string where, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public void RawDelete(IScriptingEntityUserContext context, string table, string where, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public void RawInsert(IScriptingEntityUserContext context, string table, string columns, string values, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public void RawUpdate(IScriptingEntityUserContext context, string table, string columns, string where, object p)
    {
        throw new NotSupportedException(OperationNotSupportedExceptionText);
    }

    public object? QueryScalarValue(IScriptingEntityUserContext context, 
        string column, IDictionary<string, object> p)
    {
        var result = QuerySingle(context, context.Entity.ScalarValueQuery ?? "primary", p);

        if (result is IDictionary<string, object> resultDict && resultDict.TryGetValue(column, out object? value))
        {
            return value;
        }

        return null;
    }

    public long Count(IScriptingEntityUserContext context, string queryIdentifier,
        IDictionary<string, object> p)
    {
        return GenericProvider.ProcessCountAsync(new GenericProcessingContext(context.Connection, context.Transaction, context.Tenant, context.Entity), queryIdentifier, context.UserId, context.Claims, p).GetAwaiter().GetResult();
    }

    public IEnumerable<dynamic> QueryList(IScriptingEntityUserContext context, 
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessQueryListAsync<dynamic>(new GenericProcessingContext(context.Connection, context.Transaction, context.Tenant, context.Entity), queryIdentifier, context.UserId, context.Claims, p).GetAwaiter().GetResult();
    }

    public dynamic? QuerySingle(IScriptingEntityUserContext context,
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessQuerySingleAsync<dynamic>(new GenericProcessingContext(context.Connection, context.Transaction, context.Tenant, context.Entity), queryIdentifier, context.UserId, context.Claims, p).GetAwaiter().GetResult();
    }

    public dynamic? QueryNew(IScriptingEntityUserContext context,
        string queryIdentifier, IDictionary<string, object> p)
    {
        return GenericProvider.ProcessNewAsync<dynamic>(new GenericProcessingContext(context.Connection, context.Transaction, context.Tenant, context.Entity), queryIdentifier, context.UserId, context.Claims, p).GetAwaiter().GetResult();
    }

    public void Save(IScriptingEntityUserContext context,
        string statementIdentifier, IDictionary<string, object> p)
    {
        GenericProvider.ProcessSaveAsync(new GenericProcessingContext(context.Connection, context.Transaction, context.Tenant, context.Entity), statementIdentifier, context.UserId, context.Claims, p).GetAwaiter().GetResult();
    }

    public (bool Result, IEnumerable<string> Messages) Remove(IScriptingEntityUserContext context,
        IDictionary<string, object> p)
    {
        var result = GenericProvider.ProcessRemoveAsync(new GenericProcessingContext(context.Connection, context.Transaction, context.Tenant, context.Entity), context.UserId, context.Claims, p).GetAwaiter().GetResult(); 
        
        return (result.Result, result.Messages); 
    }
}