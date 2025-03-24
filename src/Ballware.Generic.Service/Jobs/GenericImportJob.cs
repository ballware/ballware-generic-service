using Ballware.Generic.Authorization;
using Ballware.Generic.Tenant.Data;
using Ballware.Generic.Metadata;
using Ballware.Storage.Client;
using Newtonsoft.Json;
using Quartz;

namespace Ballware.Generic.Service.Jobs;

public class GenericImportJob : IJob
{
    public static readonly JobKey Key = new JobKey("import", "generic");
    
    private ITenantGenericProvider GenericProvider { get; }
    private ITenantRightsChecker TenantRightsChecker { get; }
    private IEntityRightsChecker EntityRightsChecker { get; }
    private IMetadataAdapter MetadataAdapter { get; }
    private BallwareStorageClient StorageClient { get; }
    
    public GenericImportJob(ITenantGenericProvider genericProvider, ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker, IMetadataAdapter metadataAdapter, BallwareStorageClient storageClient)
    {
        GenericProvider = genericProvider;
        TenantRightsChecker = tenantRightsChecker;
        EntityRightsChecker = entityRightsChecker;
        MetadataAdapter = metadataAdapter;
        StorageClient = storageClient;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        var tenantId = context.MergedJobDataMap.GetGuidValue("tenantId");
        var jobId = context.MergedJobDataMap.GetGuidValue("jobId");
        var userId = context.MergedJobDataMap.GetGuidValue("userId");
        var application = context.MergedJobDataMap.GetString("application");
        var entity = context.MergedJobDataMap.GetString("entity");
        var identifier = context.MergedJobDataMap.GetString("identifier");
        var claims = JsonConvert.DeserializeObject<Dictionary<string, object>>(context.MergedJobDataMap.GetString("claims") ?? "{}");
        var filename = context.MergedJobDataMap.GetString("filename");
        
        var jobPayload = new JobUpdatePayload()
        {
            Id = jobId,
            State = JobStates.InProgress,
            Result = string.Empty,
        };
        
        try
        {   
            await MetadataAdapter.UpdateJobForTenantBehalfOfUserAsync(tenantId, userId, jobPayload);
            var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var metadata = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

            var file = await StorageClient.FileByNameForOwnerAsync(userId.ToString(), filename);

            await GenericProvider.ImportAsync(tenant, metadata, userId, identifier, claims, file.Stream, async (item) =>
            {
                var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier);
                var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metadata, claims, identifier, item, tenantAuthorized);
                
                return authorized;
            });

            await StorageClient.RemoveFileForOwnerAsync(userId.ToString(), filename);

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