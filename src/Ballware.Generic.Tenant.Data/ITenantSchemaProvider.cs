namespace Ballware.Generic.Tenant.Data;

public interface ITenantSchemaProvider
{
    Task CreateOrUpdateEntityAsync(Guid tenant, string entity, string serializedEntityModel, Guid? userId);
    Task DropEntityAsync(Guid tenant, string entity, Guid? userId);
    
    Task CreateOrUpdateTenantAsync(Guid tenant, string serializedTenantModel, Guid? userId);
    Task DropTenantAsync(Guid tenant, Guid? userId);
}