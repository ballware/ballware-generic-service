using System.IO;
using System.Threading.Tasks;

namespace Ballware.Generic.Api;

public interface IGenericFileStorageAdapter
{
    Task<Stream> FileByNameForOwnerAsync(string owner, string fileName);
    Task UploadFileForOwnerAsync(string owner, string fileName, string contentType, Stream data);
}