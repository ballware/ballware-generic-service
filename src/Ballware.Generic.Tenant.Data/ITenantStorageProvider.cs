using System.Data;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantStorageProvider
{
    Task<IDbConnection> OpenConnectionAsync(Guid tenant);

    Task<string> ApplyTenantPlaceholderAsync(Guid tenant, string source, TenantPlaceholderOptions options);
}