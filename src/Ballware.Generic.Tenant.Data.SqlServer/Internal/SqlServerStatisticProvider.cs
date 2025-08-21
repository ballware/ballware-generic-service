using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerStatisticProvider : CommonStatisticProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenantId";
    
    public SqlServerStatisticProvider(ITenantStorageProvider storageProvider, IServiceProvider serviceProvider)
        : base(storageProvider, serviceProvider)
    {
    }
}