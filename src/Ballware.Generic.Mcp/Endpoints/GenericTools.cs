using System.Security.Claims;
using System.Text.Json;
using Ballware.Generic.Metadata;
using Ballware.Generic.Tenant.Data;
using Ballware.Shared.Authorization;
using Ballware.Shared.Mcp;
using Microsoft.Extensions.DependencyInjection;
using NJsonSchema;

namespace Ballware.Generic.Mcp.Endpoints;

public static class GenericToolRegistryExtensions
{
    public static IToolRegistry RegisterBallwareGenericTools(this IToolRegistry registry)
    {
        registry.RegisterDynamicToolProvider(EntityTools.GetEntityFetchToolsAsync);
        
        return registry;
    }
}

public class EntityTools
{
    public static async Task<IEnumerable<Tool>> GetEntityFetchToolsAsync(IServiceProvider serviceProvider, ClaimsPrincipal? user)
    {
        var principalUtils = serviceProvider.GetRequiredService<IPrincipalUtils>();
        var metadataProvider = serviceProvider.GetRequiredService<IMetadataAdapter>();

        if (user == null)
        {
            throw new UnauthorizedAccessException("User is not authenticated.");
        }

        var tools = new List<Tool>();
        
        var tenantId = principalUtils.GetUserTenandId(user);

        var availableEntities = await metadataProvider.SelectListForEntityAsync(tenantId);

        foreach (var entity in availableEntities)
        {
            var entityMetadata = await metadataProvider.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity.Identifier);
            
            var aiEnabledQueries = entityMetadata?.ListQuery.Where(q => q.AiEnabled).ToList() ?? []; 

            foreach (var query in aiEnabledQueries)
            {
                var capturedEntityIdentifier = entity.Identifier;
                var capturedQueryIdentifier = query.Identifier;
                var capturedRequiredParams = query.Parameters.Select(p => p.Name).ToList();

                tools.Add(new Tool()
                {
                    Name = $"generic.{entity.Application}.{entity.Identifier}.query.{query.Identifier}",
                    Description = query.Description,
                    Params = query.Parameters.Select(p => new ToolParam()
                    {
                        Name = p.Name,
                        Type = ConvertToToolParamType(p.Type),
                        Description = p.Description,
                        Required = true,
                    }),
                    OutputSchema = ConvertToOutputSchema(query.Identifier, query.Description, query.ResultColumns),
                    ExecuteAsync = async (sp, u, queryParams) =>
                    {
                        var missingParams = capturedRequiredParams
                            .Where(p => !queryParams.ContainsKey(p))
                            .ToList();

                        if (missingParams.Count > 0)
                        {
                            throw new ArgumentException(
                                $"Missing required query parameters: {string.Join(", ", missingParams)}");
                        }

                        var utils = sp.GetRequiredService<IPrincipalUtils>();
                        var metadata = sp.GetRequiredService<IMetadataAdapter>();
                        var genericProvider = sp.GetRequiredService<ITenantGenericProvider>();
                        var tenantRightsChecker = sp.GetRequiredService<ITenantRightsChecker>();
                        var entityRightsChecker = sp.GetRequiredService<IEntityRightsChecker>();

                        var tid = utils.GetUserTenandId(u);
                        var userId = utils.GetUserId(u);
                        var claims = utils.GetUserClaims(u);

                        var tenant = await metadata.MetadataForTenantByIdAsync(tid)
                            ?? throw new InvalidOperationException("Tenant not found.");
                        var entityData = await metadata.MetadataForEntityByTenantAndIdentifierAsync(tid, capturedEntityIdentifier)
                            ?? throw new InvalidOperationException($"Entity '{capturedEntityIdentifier}' not found.");

                        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, entityData.Application, entityData.Identifier, claims, "view");
                        var authorized = await entityRightsChecker.HasRightAsync(tenantId, entityData, claims, "view", new Dictionary<string, object>(), tenantAuthorized);

                        if (!authorized)
                        {
                            throw new UnauthorizedAccessException("User is not authorized to access this entity.");
                        }
                        
                        var result = await genericProvider.QueryAsync<dynamic>(tenant, entityData, capturedQueryIdentifier, userId, claims, queryParams);

                        return new ToolResult
                        {
                            StructuredContent = JsonSerializer.SerializeToElement(new { results = result }, JsonSchemaDefaults.SerializerOptions),
                        };
                    },
                });
            }
            
            var aiEnabledByIdQueries = entityMetadata?.ByIdQuery.Where(q => q.AiEnabled).ToList() ?? []; 

            foreach (var query in aiEnabledByIdQueries)
            {
                var capturedEntityIdentifier = entity.Identifier;
                var capturedQueryIdentifier = query.Identifier;
                var capturedRequiredParams = query.Parameters.Select(p => p.Name).ToList();

                tools.Add(new Tool()
                {
                    Name = $"generic.{entity.Application}.{entity.Identifier}.byid.{query.Identifier}",
                    Description = query.Description,
                    Params = query.Parameters.Select(p => new ToolParam()
                    {
                        Name = p.Name,
                        Type = ConvertToToolParamType(p.Type),
                        Description = p.Description,
                        Required = true,
                    }),
                    OutputSchema = ConvertToOutputSchema(query.Identifier, query.Description, query.ResultColumns),
                    ExecuteAsync = async (sp, u, queryParams) =>
                    {
                        var missingParams = capturedRequiredParams
                            .Where(p => !queryParams.ContainsKey(p))
                            .ToList();

                        if (missingParams.Count > 0)
                        {
                            throw new ArgumentException(
                                $"Missing required query parameters: {string.Join(", ", missingParams)}");
                        }

                        var utils = sp.GetRequiredService<IPrincipalUtils>();
                        var metadata = sp.GetRequiredService<IMetadataAdapter>();
                        var genericProvider = sp.GetRequiredService<ITenantGenericProvider>();
                        var tenantRightsChecker = sp.GetRequiredService<ITenantRightsChecker>();
                        var entityRightsChecker = sp.GetRequiredService<IEntityRightsChecker>();

                        var tid = utils.GetUserTenandId(u);
                        var userId = utils.GetUserId(u);
                        var claims = utils.GetUserClaims(u);

                        var tenant = await metadata.MetadataForTenantByIdAsync(tid)
                            ?? throw new InvalidOperationException("Tenant not found.");
                        var entityData = await metadata.MetadataForEntityByTenantAndIdentifierAsync(tid, capturedEntityIdentifier)
                            ?? throw new InvalidOperationException($"Entity '{capturedEntityIdentifier}' not found.");

                        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, entityData.Application, entityData.Identifier, claims, "view");
                        var authorized = await entityRightsChecker.HasRightAsync(tenantId, entityData, claims, "view", new Dictionary<string, object>(), tenantAuthorized);

                        if (!authorized)
                        {
                            throw new UnauthorizedAccessException("User is not authorized to access this entity.");
                        }

                        if (!queryParams.ContainsKey("id") || !Guid.TryParse(queryParams["id"] as string, out Guid id))
                        {
                            throw new ArgumentException("Missing or invalid id parameter.");
                        }
                        
                        var result = await genericProvider.ByIdAsync<dynamic>(tenant, entityData, capturedQueryIdentifier, userId, claims, id);

                        return new ToolResult
                        {
                            StructuredContent = JsonSerializer.SerializeToElement(new { results = result }, JsonSchemaDefaults.SerializerOptions),
                        };
                    },
                });
            }
        }
        
        return tools;
    }
    
    private static ToolParamType ConvertToToolParamType(string type)
    {
        return type.ToLower() switch
        {
            "string" => ToolParamType.String,
            "number" => ToolParamType.Number,
            "boolean" => ToolParamType.Boolean,
            _ => ToolParamType.String
        };
    }

    private static JsonObjectType ConvertToJsonObjectType(string type)
    {
        return type.ToLower() switch
        {
            "string" => JsonObjectType.String,
            "number" => JsonObjectType.Number,
            "boolean" => JsonObjectType.Boolean,
            _ => JsonObjectType.String
        };   
    }
    
    private static string ConvertToOutputSchema(string title, string? description, IEnumerable<QueryResultParameter> resultColumns)
    {
        var itemSchema = new JsonSchema
        {
            Type = JsonObjectType.Object,
        };

        foreach (var column in resultColumns)
        {
            itemSchema.Properties[column.Name] = new JsonSchemaProperty
            {
                Description = column.Description,
                Type = ConvertToJsonObjectType(column.Type),
            };
        }

        var schema = new JsonSchema
        {
            Title = title,
            Description = description,
            Type = JsonObjectType.Object,
        };

        schema.Properties["results"] = new JsonSchemaProperty
        {
            Type = JsonObjectType.Array,
            Item = itemSchema,
        };

        return schema.ToJson();
    }
}