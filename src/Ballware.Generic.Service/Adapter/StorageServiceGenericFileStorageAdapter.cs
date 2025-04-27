using System.IO;
using System.Threading.Tasks;
using Ballware.Generic.Api;
using Ballware.Storage.Client;

namespace Ballware.Meta.Service.Adapter;

public class StorageServiceGenericFileStorageAdapter : IGenericFileStorageAdapter
{
    private BallwareStorageClient StorageClient { get; }
    
    public StorageServiceGenericFileStorageAdapter(BallwareStorageClient storageClient)
    {
        StorageClient = storageClient;
    }
    
    public async Task<Stream> FileByNameForOwnerAsync(string owner, string fileName)
    {
        var result = await StorageClient.FileByNameForOwnerAsync(owner, fileName);
        
        return result.Stream;
    }

    public async Task UploadFileForOwnerAsync(string owner, string fileName, string contentType, Stream data)
    {
        await StorageClient.UploadFileForOwnerAsync(owner, new []{ new FileParameter(data, fileName, contentType) });
    }
}