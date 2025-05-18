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

    public static IDictionary<string, object> FilterNestedDictionaries(IDictionary<string, object> input)
    {
        var filtered = input
            .Where(kv => kv.Value is not Dictionary<string, object> && kv.Value is not List<object>)
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return filtered;
    }
}