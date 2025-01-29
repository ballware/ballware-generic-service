using System.Data;
using Ballware.Meta.Client;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantStorageProviderProxy : ITenantStorageProvider
{
    private IProviderRegistry ProviderRegistry { get; }

    public TenantStorageProviderProxy(IProviderRegistry providerRegistry)
    {
        ProviderRegistry = providerRegistry;
    }

    public string GetConnectionString(ServiceTenant tenant)
    {
        var provider = ProviderRegistry.GetStorageProvider(tenant.Provider ?? "mssql");

        return provider.GetConnectionString(tenant);
    }

    public IDbConnection OpenConnection(ServiceTenant tenant)
    {
        var provider = ProviderRegistry.GetStorageProvider(tenant.Provider ?? "mssql");

        return provider.OpenConnection(tenant);
    }

    public async Task<IDbConnection> OpenConnectionAsync(ServiceTenant tenant)
    {
        var provider = ProviderRegistry.GetStorageProvider(tenant.Provider ?? "mssql");

        return await provider.OpenConnectionAsync(tenant);
    }

    public string ApplyTenantPlaceholder(ServiceTenant tenant, string source, TenantPlaceholderOptions options)
    {
        var provider = ProviderRegistry.GetStorageProvider(tenant.Provider ?? "mssql");

        return provider.ApplyTenantPlaceholder(tenant, source, options);
    }

    public async Task<string> ApplyTenantPlaceholderAsync(ServiceTenant tenant, string source, TenantPlaceholderOptions options)
    {
        var provider = ProviderRegistry.GetStorageProvider(tenant.Provider ?? "mssql");

        return await provider.ApplyTenantPlaceholderAsync(tenant, source, options);
    }
}