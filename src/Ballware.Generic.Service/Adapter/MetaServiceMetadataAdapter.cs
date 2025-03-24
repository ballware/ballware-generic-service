using AutoMapper;
using Ballware.Generic.Metadata;
using Ballware.Meta.Client;
using JobCreatePayload = Ballware.Generic.Metadata.JobCreatePayload;
using JobUpdatePayload = Ballware.Generic.Metadata.JobUpdatePayload;
using NotificationTrigger = Ballware.Generic.Metadata.NotificationTrigger;

namespace Ballware.Generic.Service.Adapter;

public class MetaServiceMetadataAdapter : IMetadataAdapter
{
    private IMapper Mapper { get; }
    private BallwareMetaClient MetaClient { get; }
    
    public MetaServiceMetadataAdapter(IMapper mapper, BallwareMetaClient metaClient)
    {
        Mapper = mapper;
        MetaClient = metaClient;
    }

    public async Task<Metadata.Tenant?> MetadataForTenantByIdAsync(Guid tenantId)
    {
        return Mapper.Map<Metadata.Tenant>(await MetaClient.ServiceMetadataForTenantByIdAsync(tenantId));
    }

    public async Task<Entity?> MetadataForEntityByTenantAndIdentifierAsync(Guid tenantId, string identifier)
    {
        return Mapper.Map<Entity?>(await MetaClient.MetadataForEntityByTenantdAndIdentifierAsync(tenantId, identifier));
    }

    public Entity MetadataForEntityByTenantAndIdentifier(Guid tenant, string identifier)
    {
        return Mapper.Map<Entity>(MetaClient.MetadataForEntityByTenantdAndIdentifier(tenant, identifier));
    }

    public ProcessingState? SingleProcessingStateForTenantAndEntityByValue(Guid tenant, string entity, int state)
    {
        return Mapper.Map<ProcessingState>(MetaClient.SingleProcessingStateForTenantAndEntityByValue(tenant, entity, state));
    }

    public Notification? MetadataForNotificationByTenantAndIdentifier(Guid tenant, string identifier)
    {
        return Mapper.Map<Notification>(MetaClient.MetadataForNotificationByTenantAndIdentifier(tenant, identifier));
    }

    public NotificationTrigger CreateNotificationTriggerForTenantAndNotificationBehalfOfUser(Guid tenant, Guid notification,
        Guid userId)
    {
        return Mapper.Map<NotificationTrigger>(MetaClient.CreateNotificationTriggerForTenantAndNotificationBehalfOfUser(tenant, notification, userId));
    }

    public void SaveNotificationTriggerBehalfOfUser(Guid tenant, Guid userId, NotificationTrigger notificationTrigger)
    {
        MetaClient.SaveNotificationTriggerBehalfOfUser(tenant, userId, Mapper.Map<Ballware.Meta.Client.NotificationTrigger>(notificationTrigger));
    }

    public async Task<Guid> CreateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobCreatePayload payload)
    {
        var metaJob = await MetaClient.CreateJobForTenantBehalfOfUserAsync(tenant, userId, Mapper.Map<Ballware.Meta.Client.JobCreatePayload>(payload));
        
        return metaJob.Id;
    }

    public async Task<Guid> CreateExportForTenantBehalfOfUserAsync(Guid tenant, Guid userId, ExportCreatePayload payload)
    {
        var metaExport = await MetaClient.CreateExportForTenantBehalfOfUserAsync(tenant, userId);

        metaExport.Application = payload.Application;
        metaExport.Entity = payload.Entity;
        metaExport.Query = payload.Query;
        metaExport.ExpirationStamp = payload.ExpirationStamp;
        metaExport.MediaType = payload.MediaType;
        
        await MetaClient.SaveExportBehalfOfUserAsync(tenant, userId, metaExport);
        
        return metaExport.Id;
    }

    public async Task<Export> FetchExportByIdForTenantAsync(Guid tenant, Guid id)
    {
        return Mapper.Map<Export>(await MetaClient.FetchExportByIdForTenantAsync(tenant, id));
    }

    public async Task UpdateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobUpdatePayload payload)
    {
        await MetaClient.UpdateJobForTenantBehalfOfUserAsync(tenant, userId, Mapper.Map<Ballware.Meta.Client.JobUpdatePayload>(payload));
    }
}