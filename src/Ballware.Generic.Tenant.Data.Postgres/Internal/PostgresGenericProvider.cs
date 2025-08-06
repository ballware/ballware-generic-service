using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresGenericProvider : CommonGenericProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenant_id";
    
    protected override string ItemIdIdentifier { get; } = "id";
    
    public PostgresGenericProvider(ITenantStorageProvider storageProvider, IServiceProvider serviceProvider)
        : base(storageProvider, serviceProvider)
    {
    }
}