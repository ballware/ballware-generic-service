using System.Data;
using Ballware.Meta.Client;

namespace Ballware.Generic.Tenant.Data;

public interface ITenantStorageProvider
{
    string GetConnectionString(ServiceTenant tenant);

    IDbConnection OpenConnection(ServiceTenant tenant);
    Task<IDbConnection> OpenConnectionAsync(ServiceTenant tenant);

    string ApplyTenantPlaceholder(ServiceTenant tenant, string source, TenantPlaceholderOptions options);
    Task<string> ApplyTenantPlaceholderAsync(ServiceTenant tenant, string source, TenantPlaceholderOptions options);
}