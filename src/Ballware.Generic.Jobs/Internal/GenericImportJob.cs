using Ballware.Generic.Authorization;
using Ballware.Generic.Tenant.Data;
using Ballware.Generic.Metadata;
using Newtonsoft.Json;
using Quartz;

namespace Ballware.Generic.Jobs.Internal;

public class GenericImportJob : IJob
{
    public static readonly JobKey Key = new JobKey("import", "generic");
    
    private ITenantGenericProvider GenericProvider { get; }
    private ITenantRightsChecker TenantRightsChecker { get; }
    private IEntityRightsChecker EntityRightsChecker { get; }
    private IMetadataAdapter MetadataAdapter { get; }
    private IJobsFileStorageAdapter StorageAdapter { get; }
    
    public GenericImportJob(ITenantGenericProvider genericProvider, ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker, IMetadataAdapter metadataAdapter, IJobsFileStorageAdapter storageAdapter)
    {
        GenericProvider = genericProvider;
        TenantRightsChecker = tenantRightsChecker;
        EntityRightsChecker = entityRightsChecker;
        MetadataAdapter = metadataAdapter;
        StorageAdapter = storageAdapter;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        var tenantId = context.MergedJobDataMap.GetGuidValue("tenantId");
        var jobId = context.MergedJobDataMap.GetGuidValue("jobId");
        var userId = context.MergedJobDataMap.GetGuidValue("userId");
        context.MergedJobDataMap.TryGetString("application", out var application);
        context.MergedJobDataMap.TryGetString("entity", out var entity);
        context.MergedJobDataMap.TryGetString("identifier", out var identifier);
        var claims = Utils.DropNullMember(Utils.NormalizeJsonMember(JsonConvert.DeserializeObject<Dictionary<string, object?>>(context.MergedJobDataMap.GetString("claims") ?? "{}")
                     ?? new Dictionary<string, object?>()));
        context.MergedJobDataMap.TryGetString("filename", out var filename);
        
        var jobPayload = new JobUpdatePayload()
        {
            Id = jobId,
            State = JobStates.InProgress,
            Result = string.Empty,
        };
        
        try
        {
            if (identifier == null || application == null || entity == null || filename == null) 
            {
                throw new ArgumentException($"Mandatory parameter missing");
            }
            
            await MetadataAdapter.UpdateJobForTenantBehalfOfUserAsync(tenantId, userId, jobPayload);
            var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var metadata = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);
            
            if (tenant == null || metadata == null)
            {
                throw new ArgumentException($"Tenant {tenantId} or entity {entity} unknown");
            }

            var file = await StorageAdapter.FileByNameForOwnerAsync(userId.ToString(), filename);

            await GenericProvider.ImportAsync(tenant, metadata, userId, identifier, claims, file, async (item) =>
            {
                var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier);
                var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metadata, claims, identifier, item, tenantAuthorized);
                
                return authorized;
            });

            await StorageAdapter.RemoveFileForOwnerAsync(userId.ToString(), filename);

            jobPayload.State = JobStates.Finished;
            
            await MetadataAdapter.UpdateJobForTenantBehalfOfUserAsync(tenantId, userId, jobPayload);
        }
        catch (Exception ex)
        {
            jobPayload.State = JobStates.Error;
            jobPayload.Result = JsonConvert.SerializeObject(ex);
            
            await MetadataAdapter.UpdateJobForTenantBehalfOfUserAsync(tenantId, userId, jobPayload);
            
            // do you want the job to refire?
            throw new JobExecutionException(msg: "", refireImmediately: false, cause: ex);
        }
    }
}