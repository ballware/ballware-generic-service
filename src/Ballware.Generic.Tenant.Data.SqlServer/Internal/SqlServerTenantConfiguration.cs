namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerTenantConfiguration
{
    public bool UseContainedDatabase { get; set; }
    public string TenantMasterConnectionString { get; set; }
}