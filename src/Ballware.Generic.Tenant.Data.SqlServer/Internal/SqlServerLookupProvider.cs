using Ballware.Generic.Tenant.Data.Commons.Provider;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

class SqlServerLookupProvider : CommonLookupProvider
{
    protected override string TenantVariableIdentifier { get; } = "tenantId";
    
    public SqlServerLookupProvider(ITenantStorageProvider storageProvider)
        : base(storageProvider)
    {
    }
}