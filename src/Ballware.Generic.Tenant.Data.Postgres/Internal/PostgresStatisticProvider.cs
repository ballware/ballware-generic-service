using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresStatisticProvider : CommonStatisticProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenant_id";
    
    public PostgresStatisticProvider(ITenantStorageProvider storageProvider, IServiceProvider serviceProvider)
        : base(storageProvider, serviceProvider)
    {
    }
}