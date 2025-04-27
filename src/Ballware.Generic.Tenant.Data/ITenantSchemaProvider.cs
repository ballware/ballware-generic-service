namespace Ballware.Generic.Tenant.Data;

public interface ITenantSchemaProvider
{
    Task CreateOrUpdateEntityAsync(Guid tenant, string serializedEntityModel, Guid? userId);
    Task DropEntityAsync(Guid tenant, string application, string identifier, Guid? userId);
    
    Task CreateOrUpdateTenantAsync(Guid tenant, string provider, string serializedTenantModel, Guid? userId);
    Task DropTenantAsync(Guid tenant, Guid? userId);
}