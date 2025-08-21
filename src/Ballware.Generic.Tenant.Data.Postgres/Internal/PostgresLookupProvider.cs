using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresLookupProvider : CommonLookupProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenant_id";
    
    public PostgresLookupProvider(ITenantStorageProvider storageProvider)
        : base(storageProvider)
    {
    }
}