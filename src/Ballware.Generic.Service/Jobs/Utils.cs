using Newtonsoft.Json.Linq;

namespace Ballware.Generic.Service.Jobs;

public static class Utils
{
    private static object? NormalizeJsonValue(object? value)
    {
        return value switch
        {
            JValue jv => jv.Value,
            JObject jo => jo.ToObject<Dictionary<string, object>>()?
                .ToDictionary(kv => kv.Key, kv => NormalizeJsonValue(kv.Value)),
            JArray ja => ja.Select(NormalizeJsonValue).ToList(),
            Dictionary<string, object> dict => dict.ToDictionary(kv => kv.Key, kv => NormalizeJsonValue(kv.Value)),
            _ => value
        };
    }

    public static IDictionary<string, object?> NormalizeJsonMember(IDictionary<string, object?> input)
    {
        return input.ToDictionary(kv => kv.Key, kv => NormalizeJsonValue(kv.Value));
    }
}