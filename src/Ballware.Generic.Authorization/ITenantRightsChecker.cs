using Ballware.Meta.Client;

namespace Ballware.Generic.Authorization;

public interface ITenantRightsChecker
{
    public Task<bool> HasRightAsync(ServiceTenant tenant, string application, string entity, Dictionary<string, object> claims, string right);

}