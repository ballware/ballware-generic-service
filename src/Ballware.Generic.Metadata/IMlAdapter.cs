namespace Ballware.Generic.Metadata;

public interface IMlAdapter
{
    public Task<object> ConsumeByIdentifierBehalfOfUserAsync(Guid tenant, Guid user, string model,
        IDictionary<string, IEnumerable<string>> query);
}