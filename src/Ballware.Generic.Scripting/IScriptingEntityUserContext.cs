using System.Data;
using Ballware.Generic.Metadata;

namespace Ballware.Generic.Scripting;

public interface IScriptingEntityUserContext
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; } 
    Tenant Tenant { get; }
    Entity Entity { get; } 
    Guid UserId { get; }
    IDictionary<string, object> Claims { get; }
}