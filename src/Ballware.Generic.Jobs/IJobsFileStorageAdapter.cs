namespace Ballware.Generic.Jobs;

public interface IJobsFileStorageAdapter
{
    Task<Stream> TemporaryFileByIdAsync(Guid tenantId, Guid temporaryId);
    Task RemoveTemporaryFileByIdBehalfOfUserAsync(Guid tenantId, Guid userId, Guid temporaryId);
}