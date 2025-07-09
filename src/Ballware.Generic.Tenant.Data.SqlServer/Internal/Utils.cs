using Newtonsoft.Json.Linq;

namespace Ballware.Generic.Tenant.Data.SqlServer.Internal;

static class Utils
{
    public static T TransferToSqlVariables<T>(T target,
        IDictionary<string, object>? source, string prefix = "") where T : IDictionary<string, object> 
    {
        if (source?.Count > 0)
        {
            foreach (var pair in source)
            {
                target[$"{prefix}{pair.Key}"] = pair.Value is string[] arrayValue ? $"|{string.Join("|", arrayValue)}|" : pair.Value;
            }
        }
        
        return target;
    }
    
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