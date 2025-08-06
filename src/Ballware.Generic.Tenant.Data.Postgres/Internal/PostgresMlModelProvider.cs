using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.Postgres.Internal;

class PostgresMlModelProvider : CommonMlModelProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenant_id";
    
    public PostgresMlModelProvider(ITenantStorageProvider storageProvider)
        : base(storageProvider)
    {
    }
}