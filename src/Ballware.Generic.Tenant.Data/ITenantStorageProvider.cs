using System.Data;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantStorageProvider
{
    Task<string> GetConnectionStringAsync(Guid tenant);
    Task<IDbConnection> OpenConnectionAsync(Guid tenant);
    Task<string> ApplyTenantPlaceholderAsync(Guid tenant, string source, TenantPlaceholderOptions options);
}