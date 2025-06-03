namespace Ballware.Generic.Jobs;

public interface IJobsFileStorageAdapter
{
    Task<Stream> FileByNameForOwnerAsync(string owner, string fileName);
    Task RemoveFileForOwnerAsync(string owner, string fileName);
}