using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerGenericProvider : CommonGenericProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenantId";
    
    public SqlServerGenericProvider(ITenantStorageProvider storageProvider, IServiceProvider serviceProvider)
        : base(storageProvider, serviceProvider)
    {
    }
}