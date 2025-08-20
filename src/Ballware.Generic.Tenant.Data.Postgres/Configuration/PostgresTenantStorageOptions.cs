namespace Ballware.Generic.Tenant.Data.Postgres.Configuration;

public sealed class PostgresTenantStorageOptions
{
    public bool Enabled { get; set; } = false;
    public string? ConnectionString { get; set; }
}