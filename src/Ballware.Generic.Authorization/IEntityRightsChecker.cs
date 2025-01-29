using Ballware.Meta.Client;

namespace Ballware.Generic.Authorization;

public interface IEntityRightsChecker
{
    public Task<bool> HasRightAsync(Guid tenantId, ServiceEntity metadata, Dictionary<string, object> claims, string right, IDictionary<string, object> param, bool tenantResult);
}