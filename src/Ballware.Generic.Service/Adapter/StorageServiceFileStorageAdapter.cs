using Ballware.Generic.Api;
using Ballware.Generic.Jobs;
using Ballware.Storage.Client;

namespace Ballware.Generic.Service.Adapter;

public class StorageServiceFileStorageAdapter : IGenericFileStorageAdapter, IJobsFileStorageAdapter
{
    private BallwareStorageClient StorageClient { get; }
    
    public StorageServiceFileStorageAdapter(BallwareStorageClient storageClient)
    {
        StorageClient = storageClient;
    }
    
    public async Task<Stream> FileByNameForOwnerAsync(string owner, string fileName)
    {
        var result = await StorageClient.FileByNameForOwnerAsync(owner, fileName);
        
        return result.Stream;
    }

    public async Task RemoveFileForOwnerAsync(string owner, string fileName)
    {
        await StorageClient.RemoveFileForOwnerAsync(owner, fileName);
    }

    public async Task UploadFileForOwnerAsync(string owner, string fileName, string contentType, Stream data)
    {
        await StorageClient.UploadFileForOwnerAsync(owner, new []{ new FileParameter(data, fileName, contentType) });
    }
}