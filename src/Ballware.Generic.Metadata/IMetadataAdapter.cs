namespace Ballware.Generic.Metadata;

public interface IMetadataAdapter
{
    Task<Tenant?> MetadataForTenantByIdAsync(Guid tenantId);
    Task<Entity?> MetadataForEntityByTenantAndIdentifierAsync(Guid tenantId, string identifier);
    
    Entity MetadataForEntityByTenantAndIdentifier(Guid tenant, string identifier);

    ProcessingState? SingleProcessingStateForTenantAndEntityByValue(Guid tenant, string entity, int state);
    
    Notification? MetadataForNotificationByTenantAndIdentifier(Guid tenant, string identifier);

    NotificationTrigger CreateNotificationTriggerForTenantAndNotificationBehalfOfUser(Guid tenant, Guid notification, Guid userId);
    
    void SaveNotificationTriggerBehalfOfUser(Guid tenant, Guid userId, NotificationTrigger notificationTrigger);

    Task<Guid> CreateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobCreatePayload payload);
    Task<Guid> CreateExportForTenantBehalfOfUserAsync(Guid tenant, Guid userId, ExportCreatePayload payload);
    Task<Export> FetchExportByIdForTenantAsync(Guid tenant, Guid id);
    
    Task UpdateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobUpdatePayload payload);
}