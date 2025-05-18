using System.Text.Json;
using Ballware.Generic.Metadata;
using Jint;

namespace Ballware.Generic.Authorization.Jint.Internal;

class JavascriptEntityRightsChecker : IEntityRightsChecker
{
    public async Task<bool> HasRightAsync(Guid tenantId, Entity metadata, Dictionary<string, object> claims, string right, IDictionary<string, object> param,
        bool tenantResult)
    {
        var result = tenantResult;
        var rightsScript = metadata.ExtendedRightsCheckScript;

        if (!string.IsNullOrWhiteSpace(rightsScript))
        {
            var userinfo = JsonSerializer.Serialize(claims);

            result = bool.Parse(new Engine()
                .SetValue("application", metadata.Application)
                .SetValue("entity", metadata.Identifier)
                .SetValue("right", right)
                .SetValue("param", param)
                .SetValue("result", tenantResult)
                .Evaluate($"var userinfo = JSON.parse('{userinfo}'); function extendedRightsCheck() {{ {rightsScript} }} return extendedRightsCheck();")
                .ToString());
        }

        return await Task.FromResult(result);
    }
}