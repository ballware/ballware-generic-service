namespace Ballware.Generic.Tenant.Data.Postgres.Tests.Utils;

public class TenantConnectionAttribute : Attribute
{
    public string Schema { get; }
    public string? User { get; }
    
    public TenantConnectionAttribute(string schema)
    {
        Schema = schema;
    }
    
    public TenantConnectionAttribute(string schema, string user) : this(schema)
    {
        User = user;
    }
}