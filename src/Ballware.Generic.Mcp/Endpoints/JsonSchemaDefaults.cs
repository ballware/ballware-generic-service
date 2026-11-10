using System.Text.Json;
using NJsonSchema.Generation;

namespace Ballware.Generic.Mcp.Endpoints;

internal static class JsonSchemaDefaults
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static readonly JsonSchemaGeneratorSettings SchemaSettings = new SystemTextJsonSchemaGeneratorSettings
    {
        SerializerOptions = SerializerOptions
    };
}