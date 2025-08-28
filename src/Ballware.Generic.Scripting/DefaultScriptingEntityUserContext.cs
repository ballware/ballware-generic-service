using System.Data;
using Ballware.Generic.Metadata;

namespace Ballware.Generic.Scripting;

public class DefaultScriptingEntityUserContext : IScriptingEntityUserContext
{
    public static DefaultScriptingEntityUserContext Create(IDbConnection connection, Tenant tenant, Entity entity, Guid userId, IDictionary<string, object> claims)
    {
        return new DefaultScriptingEntityUserContext(connection, null, tenant, entity, userId, claims);
    }
    
    public static DefaultScriptingEntityUserContext CreateWithTransaction(IDbConnection connection, IDbTransaction? transaction, Tenant tenant, Entity entity, Guid userId, IDictionary<string, object> claims)
    {
        return new DefaultScriptingEntityUserContext(connection, transaction, tenant, entity, userId, claims);
    }
    
    public static DefaultScriptingEntityUserContext DuplicateForEntity(IScriptingEntityUserContext source, Entity entity)
    {
        return new DefaultScriptingEntityUserContext(source.Connection, source.Transaction, source.Tenant, entity, source.UserId, source.Claims);
    }
    
    private DefaultScriptingEntityUserContext(IDbConnection connection, IDbTransaction? transaction, Tenant tenant, Entity entity, Guid userId, IDictionary<string, object> claims) 
    {
        Connection = connection;
        Transaction = transaction;
        Tenant = tenant;
        Entity = entity;
        UserId = userId;
        Claims = claims;
    }

    public IDbConnection Connection { get; }
    public IDbTransaction? Transaction { get; }
    public Tenant Tenant { get; }
    public Entity Entity { get; }
    public Guid UserId { get; }
    public IDictionary<string, object> Claims { get; }
}