using Ballware.Generic.Metadata;

namespace Ballware.Generic.Authorization;

public interface ITenantRightsChecker
{
    public Task<bool> HasRightAsync(Tenant tenant, string application, string entity, IDictionary<string, object> claims, string right);

}