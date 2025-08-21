using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerGenericProvider : CommonGenericProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenantId";
    protected override string ItemIdIdentifier { get; } = "Id";
    
    public SqlServerGenericProvider(ITenantStorageProvider storageProvider, IServiceProvider serviceProvider)
        : base(storageProvider, serviceProvider)
    {
    }
}