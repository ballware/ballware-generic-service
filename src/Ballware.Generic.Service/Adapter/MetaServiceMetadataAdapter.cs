using AutoMapper;
using Ballware.Generic.Metadata;
using JobCreatePayload = Ballware.Generic.Metadata.JobCreatePayload;
using JobUpdatePayload = Ballware.Generic.Metadata.JobUpdatePayload;
using MlModel = Ballware.Generic.Metadata.MlModel;
using NotificationTrigger = Ballware.Generic.Metadata.NotificationTrigger;
using ProcessingStateSelectListEntry = Ballware.Generic.Metadata.ProcessingStateSelectListEntry;

namespace Ballware.Generic.Service.Adapter;

public class MetaServiceMetadataAdapter : IMetadataAdapter
{
    private IMapper Mapper { get; }
    private Ballware.Meta.Client.BallwareMetaClient MetaClient { get; }
    
    public MetaServiceMetadataAdapter(IMapper mapper, Ballware.Meta.Client.BallwareMetaClient metaClient)
    {
        Mapper = mapper;
        MetaClient = metaClient;
    }

    public async Task<Metadata.Tenant?> MetadataForTenantByIdAsync(Guid tenantId)
    {
        return Mapper.Map<Metadata.Tenant>(await MetaClient.TenantServiceMetadataAsync(tenantId));
    }

    public async Task<Entity?> MetadataForEntityByTenantAndIdentifierAsync(Guid tenantId, string identifier)
    {
        return Mapper.Map<Entity?>(await MetaClient.EntityServiceMetadataForTenantByIdentifierAsync(tenantId, identifier));
    }

    public async Task<Lookup?> MetadataForLookupByTenantAndIdAsync(Guid tenantId, Guid id)
    {
        return Mapper.Map<Lookup?>(await MetaClient.LookupMetadataForTenantAndIdAsync(tenantId, id));
    }

    public async Task<Lookup?> MetadataForLookupByTenantAndIdentifierAsync(Guid tenantId, string identifier)
    {
        return Mapper.Map<Lookup?>(await MetaClient.LookupMetadataForTenantAndIdentifierAsync(tenantId, identifier));
    }

    public async Task<IEnumerable<Lookup>> MetadataForLookupsByTenantAsync(Guid tenantId)
    {
        return Mapper.Map<IEnumerable<Lookup>>(await MetaClient.LookupMetadataForTenantAsync(tenantId));
    }

    public async Task<MlModel?> MetadataForMlModelByTenantAndIdAsync(Guid tenantId, Guid id)
    {
        return Mapper.Map<MlModel?>(await MetaClient.MlModelMetadataByTenantAndIdAsync(tenantId, id));
    }

    public async Task<Statistic?> MetadataForStatisticByTenantAndIdentifierAsync(Guid tenantId, string identifier)
    {
        return Mapper.Map<Statistic?>(await MetaClient.StatisticMetadataByTenantAndIdentifierAsync(tenantId, identifier));
    }

    public Entity MetadataForEntityByTenantAndIdentifier(Guid tenant, string identifier)
    {
        return Mapper.Map<Entity>(MetaClient.EntityServiceMetadataForTenantByIdentifier(tenant, identifier));
    }

    public async Task<IEnumerable<ProcessingStateSelectListEntry>> SelectListPossibleSuccessorsForEntityAsync(Guid tenantId, string entity, int state)
    {
        return Mapper.Map<IEnumerable<ProcessingStateSelectListEntry>>(await MetaClient.ProcessingStateSelectListAllSuccessorsForTenantAndEntityByIdentifierAsync(tenantId, entity, state));
    }

    public ProcessingState? SingleProcessingStateForTenantAndEntityByValue(Guid tenant, string entity, int state)
    {
        return Mapper.Map<ProcessingState>(MetaClient.ProcessingStateSelectByStateForTenantAndEntityByIdentifier(tenant, entity, state));
    }

    public Notification? MetadataForNotificationByTenantAndIdentifier(Guid tenant, string identifier)
    {
        return Mapper.Map<Notification>(MetaClient.NotificationMetadataByTenantAndIdentifier(tenant, identifier));
    }

    public NotificationTrigger CreateNotificationTriggerForTenantAndNotificationBehalfOfUser(Guid tenant, Guid notification,
        Guid userId)
    {
        return Mapper.Map<NotificationTrigger>(MetaClient.NotificationTriggerCreateForTenantAndNotificationBehalfOfUser(tenant, notification, userId));
    }

    public void SaveNotificationTriggerBehalfOfUser(Guid tenant, Guid userId, NotificationTrigger notificationTrigger)
    {
        MetaClient.NotificationTriggerSaveForTenantBehalfOfUser(tenant, userId, Mapper.Map<Ballware.Meta.Client.NotificationTrigger>(notificationTrigger));
    }

    public async Task<Guid?> CreateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobCreatePayload payload)
    {
        var metaJob = await MetaClient.JobCreateForTenantBehalfOfUserAsync(tenant, userId, Mapper.Map<Ballware.Meta.Client.JobCreatePayload>(payload));
        
        return metaJob.Id;
    }

    public async Task<Guid> CreateExportForTenantBehalfOfUserAsync(Guid tenant, Guid userId, ExportCreatePayload payload)
    {
        var exportId = await MetaClient.ExportCreateForTenantBehalfOfUserAsync(tenant, userId, Mapper.Map<Ballware.Meta.Client.ExportCreatePayload>(payload));
        
        return exportId;
    }

    public async Task<Export> FetchExportByIdForTenantAsync(Guid tenant, Guid id)
    {
        return Mapper.Map<Export>(await MetaClient.ExportFetchForTenantByIdAsync(tenant, id));
    }

    public async Task UpdateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobUpdatePayload payload)
    {
        await MetaClient.JobUpdateForTenantBehalfOfUserAsync(tenant, userId, Mapper.Map<Ballware.Meta.Client.JobUpdatePayload>(payload));
    }
}