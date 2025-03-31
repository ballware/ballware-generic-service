using System.Data;
using Ballware.Generic.Data.Repository;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantStorageProviderProxy : ITenantStorageProvider
{
    private IProviderRegistry ProviderRegistry { get; }
    private ITenantConnectionRepository ConnectionRepository { get; }

    public TenantStorageProviderProxy(IProviderRegistry providerRegistry, ITenantConnectionRepository connectionRepository)
    {
        ProviderRegistry = providerRegistry;
        ConnectionRepository = connectionRepository;
    }

    public async Task<string> GetConnectionStringAsync(Guid tenant)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);

        if (connection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var provider = ProviderRegistry.GetStorageProvider(connection.Provider ?? "mssql");

        return await provider.GetConnectionStringAsync(tenant);
    }

    public async Task<IDbConnection> OpenConnectionAsync(Guid tenant)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);

        if (connection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var provider = ProviderRegistry.GetStorageProvider(connection.Provider ?? "mssql");

        return await provider.OpenConnectionAsync(tenant);
    }

    public async Task<string> ApplyTenantPlaceholderAsync(Guid tenant, string source, TenantPlaceholderOptions options)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);

        if (connection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var provider = ProviderRegistry.GetStorageProvider(connection.Provider ?? "mssql");

        return await provider.ApplyTenantPlaceholderAsync(tenant, source, options);
    }
}