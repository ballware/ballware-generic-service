using Ballware.Generic.Data.Repository;

namespace Ballware.Generic.Tenant.Data.Internal;

class TenantSchemaProviderProxy : ITenantSchemaProvider
{
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
        
        var provider = ProviderRegistry.GetSchemaProvider(connection.Provider ?? "mssql");

        await provider.CreateOrUpdateEntityAsync(tenant, serializedEntityModel, userId);
    }

    public async Task DropEntityAsync(Guid tenant, string application, string identifier, Guid? userId)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);
        
        if (connection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var provider = ProviderRegistry.GetSchemaProvider(connection.Provider ?? "mssql");

        await provider.DropEntityAsync(tenant, application, identifier, userId);
    }

    public async Task CreateOrUpdateTenantAsync(Guid tenant, string provider, string serializedTenantModel, Guid? userId)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);
        
        var impl = ProviderRegistry.GetSchemaProvider(connection?.Provider ?? provider ?? "mssql");

        await impl.CreateOrUpdateTenantAsync(tenant, connection?.Provider ?? provider ?? "mssql", serializedTenantModel, userId);
    }

    public async Task DropTenantAsync(Guid tenant, Guid? userId)
    {
        var connection = await ConnectionRepository.ByIdAsync(tenant);
        
        if (connection == null)
        {
            throw new ArgumentException($"Tenant {tenant} does not exist");
        }
        
        var provider = ProviderRegistry.GetSchemaProvider(connection.Provider ?? "mssql");

        await provider.DropTenantAsync(tenant, userId);
    }
}