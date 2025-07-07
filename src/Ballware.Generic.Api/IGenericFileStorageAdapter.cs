using System.IO;
using System.Threading.Tasks;

namespace Ballware.Generic.Api;

public interface IGenericFileStorageAdapter
{
    Task<Stream> TemporaryFileByIdAsync(Guid tenantId, Guid temporaryId);
    Task UploadTemporaryFileBehalfOfUserAsync(Guid tenantId, Guid userId, Guid temporaryId, string fileName, string contentType, Stream data);
}