using Ballware.Generic.Metadata;

namespace Ballware.Generic.Authorization;

public interface IEntityRightsChecker
{
    public Task<bool> HasRightAsync(Guid tenantId, Entity metadata, IDictionary<string, object> claims, string right, object param, bool tenantResult);
}