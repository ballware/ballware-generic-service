using Newtonsoft.Json.Linq;

namespace Ballware.Generic.Tenant.Data.Commons.Utils;

public static class ComplexDataUtils
{
    public static IDictionary<string, object> DropComplexMember(IDictionary<string, object> input)
    {   
        var filtered = input
            .Where(kv => kv.Value is not Dictionary<string, object> && kv.Value is not List<object> && kv.Value is not object[])
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return filtered;
    }
    
    private static object NormalizeJsonValue(object value)
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

    public static IDictionary<string, object> NormalizeJsonMember(IDictionary<string, object> input)
    {
        return input.ToDictionary(kv => kv.Key, kv => NormalizeJsonValue(kv.Value));
    }
}