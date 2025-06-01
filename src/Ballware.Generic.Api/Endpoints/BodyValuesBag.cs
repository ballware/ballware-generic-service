using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Ballware.Generic.Api.Endpoints;

public class BodyValuesBag
{
    public IEnumerable<Dictionary<string, object>> Values { get; private set; } = new List<Dictionary<string, object>>();

    public static async ValueTask<BodyValuesBag?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        if (context.Request.ContentLength > 0)
        {
            using var ms = new MemoryStream();
            await context.Request.Body.CopyToAsync(ms);
            
            JsonDocument doc = JsonDocument.Parse(ms.ToArray());

            var values = (IEnumerable<Dictionary<string, object>>)ReadElement(doc.RootElement);

            return new BodyValuesBag() { Values = values };            
        }

        return null;
    }
    
    private static object ReadElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(prop => prop.Name, prop => ReadElement(prop.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ReadElement).ToList(),
            JsonValueKind.String => element.GetString()!,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null!,
            _ => element.ToString()!
        };
    }
}