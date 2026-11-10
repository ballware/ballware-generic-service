using MapsterMapper;
using Ballware.Generic.Metadata;
using JobCreatePayload = Ballware.Generic.Metadata.JobCreatePayload;
using JobUpdatePayload = Ballware.Generic.Metadata.JobUpdatePayload;
using ProcessingStateSelectListEntry = Ballware.Generic.Metadata.ProcessingStateSelectListEntry;

namespace Ballware.Generic.Service.Adapter;

public class MetaServiceMetadataAdapter : IMetadataAdapter
{
    private IMapper Mapper { get; }
    private Ballware.Meta.Service.Client.MetaServiceClient MetaClient { get; }
    
    public MetaServiceMetadataAdapter(IMapper mapper, Ballware.Meta.Service.Client.MetaServiceClient metaClient)
    {
        Mapper = mapper;
        MetaClient = metaClient;
    }

    public async Task<Metadata.Tenant?> MetadataForTenantByIdAsync(Guid tenantId)
    {
        return Mapper.Map<Metadata.Tenant>(await MetaClient.TenantServiceMetadataAsync(tenantId));
    }

    public async Task<IEnumerable<EntitySelectListEntry>> SelectListForEntityAsync(Guid tenantId)
    {
        return Mapper.Map<IEnumerable<Metadata.EntitySelectListEntry>>(await MetaClient.EntitySelectListForTenantAsync(tenantId));
    }

    public async Task<Entity?> MetadataForEntityByTenantAndIdentifierAsync(Guid tenantId, string identifier)
    {
        var rawEntity = await MetaClient.EntityServiceMetadataForTenantByIdentifierAsync(tenantId, identifier);
        
        return Mapper.Map<Entity?>(rawEntity);
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

    public async Task<Guid?> CreateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobCreatePayload payload)
    {
        var metaJob = await MetaClient.JobCreateForTenantBehalfOfUserAsync(tenant, userId, Mapper.Map<Ballware.Meta.Service.Client.JobCreatePayload>(payload));
        
        return metaJob.Id;
    }

    public async Task<Guid> CreateExportForTenantBehalfOfUserAsync(Guid tenant, Guid userId, ExportCreatePayload payload)
    {
        var exportId = await MetaClient.ExportCreateForTenantBehalfOfUserAsync(tenant, userId, Mapper.Map<Ballware.Meta.Service.Client.ExportCreatePayload>(payload));
        
        return exportId;
    }

    public async Task<Export> FetchExportForTenantByIdAsync(Guid tenant, Guid id)
    {
        return Mapper.Map<Export>(await MetaClient.ExportFetchForTenantByIdAsync(tenant, id));
    }

    public async Task UpdateJobForTenantBehalfOfUserAsync(Guid tenant, Guid userId, JobUpdatePayload payload)
    {
        await MetaClient.JobUpdateForTenantBehalfOfUserAsync(tenant, userId, Mapper.Map<Ballware.Meta.Service.Client.JobUpdatePayload>(payload));
    }
}