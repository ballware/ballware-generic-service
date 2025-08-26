namespace Ballware.Generic.Metadata;

public interface IMetadataAdapter
{
    Task<Tenant?> MetadataForTenantByIdAsync(Guid tenantId);
    Task<Entity?> MetadataForEntityByTenantAndIdentifierAsync(Guid tenantId, string identifier);
    
    Task<Lookup?> MetadataForLookupByTenantAndIdAsync(Guid tenantId, Guid id);
    Task<Lookup?> MetadataForLookupByTenantAndIdentifierAsync(Guid tenantId, string identifier);
    
    Task<IEnumerable<Lookup>> MetadataForLookupsByTenantAsync(Guid tenantId);
    
    Task<MlModel?> MetadataForMlModelByTenantAndIdAsync(Guid tenantId, Guid id);
    Task<Statistic?> MetadataForStatisticByTenantAndIdentifierAsync(Guid tenantId, string identifier);
    
    Entity MetadataForEntityByTenantAndIdentifier(Guid tenant, string identifier);

    Task<IEnumerable<ProcessingStateSelectListEntry>> SelectListPossibleSuccessorsForEntityAsync(Guid tenantId,
        string entity, int state);
    
    ProcessingState? SingleProcessingStateForTenantAndEntityByValue(Guid tenant, string entity, int state);
    
    Notification? MetadataForNotificationByTenantAndIdentifier(Guid tenant, string identifier);
    
    void CreateNotificationTriggerForTenantBehalfOfUser(Guid tenant, Guid userId, NotificationTriggerCreatePayload payload);

    Task<Guid?> CreateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobCreatePayload payload);
    Task<Guid> CreateExportForTenantBehalfOfUserAsync(Guid tenant, Guid userId, ExportCreatePayload payload);
    Task<Export> FetchExportForTenantByIdAsync(Guid tenant, Guid id);
    
    Task UpdateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobUpdatePayload payload);
}