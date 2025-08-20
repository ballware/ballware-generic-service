namespace Ballware.Generic.Tenant.Data.SqlServer.Configuration;

public sealed class SqlServerTenantStorageOptions
{
    public bool Enabled { get; set; } = false;
    public bool UseContainedDatabase { get; set; } = false;
    public string? ConnectionString { get; set; }
}