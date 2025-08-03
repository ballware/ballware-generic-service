using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerMlModelProvider : CommonMlModelProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenantId";
    
    public SqlServerMlModelProvider(ITenantStorageProvider storageProvider)
        : base(storageProvider)
    {
    }
}