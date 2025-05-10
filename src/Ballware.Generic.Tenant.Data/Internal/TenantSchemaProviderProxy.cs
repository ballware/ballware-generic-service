using Ballware.Generic.Data.Repository;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantSchemaProviderProxy : ITenantSchemaProvider
{
    private const string DefaultProvider = "mssql";
    private IProviderRegistry ProviderRegistry { get; }
    private ITenantConnectionRepository ConnectionRepository { get; }

    public TenantSchemaProviderProxy(IProviderRegistry providerRegistry, ITenantConnectionRepository connectionRepository)
    {
        ProviderRegistry = providerRegistry;
        ConnectionRepository = connectionRepository;
    }

    public async Task CreateOrUpdateEntityAsync(Guid tenant, string serializedEntityModel, Guid? userId)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);
        
        if (connection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var provider = ProviderRegistry.GetSchemaProvider(connection.Provider ?? DefaultProvider);

        await provider.CreateOrUpdateEntityAsync(tenant, serializedEntityModel, userId);
    }

    public async Task DropEntityAsync(Guid tenant, string identifier, Guid? userId)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);
        
        if (connection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var provider = ProviderRegistry.GetSchemaProvider(connection.Provider ?? DefaultProvider);

        await provider.DropEntityAsync(tenant, identifier, userId);
    }

    public async Task CreateOrUpdateTenantAsync(Guid tenant, string provider, string serializedTenantModel, Guid? userId)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);
        
        var impl = ProviderRegistry.GetSchemaProvider(connection?.Provider ?? provider ?? DefaultProvider);

        await impl.CreateOrUpdateTenantAsync(tenant, connection?.Provider ?? provider ?? DefaultProvider, serializedTenantModel, userId);
    }

    public async Task DropTenantAsync(Guid tenant, Guid? userId)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);
        
        if (connection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var provider = ProviderRegistry.GetSchemaProvider(connection.Provider ?? DefaultProvider);

        await provider.DropTenantAsync(tenant, userId);
    }
}