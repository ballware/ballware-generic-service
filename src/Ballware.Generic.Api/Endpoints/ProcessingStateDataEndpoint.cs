using System.Security.Claims;
using Ballware.Generic.Tenant.Data;
using Ballware.Generic.Authorization;
using Ballware.Generic.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ballware.Generic.Api.Endpoints;

public static class ProcessingStateDataEndpoint
{
    public static IEndpointRouteBuilder MapProcessingStateDataApi(this IEndpointRouteBuilder app, 
        string basePath,
        string apiTag = "ProcessingState",
        string apiOperationPrefix = "ProcessingState",
        string authorizationScope = "metaApi",
        string apiGroup = "meta")
    {
        app.MapGet(basePath + "/selectlistallowedsuccessorsforentity/{entity}/{id}", HandleSelectListAllowedSuccessorsForEntityByIdentifierAndIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<ProcessingStateSelectListEntry>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "SelectListAllSuccessorsForEntityByIdentifierAndId")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query all possible successor processing states for entity by identifier and instance id");
        
        app.MapGet(basePath + "/selectlistallowedsuccessorsforentities/{entity}", HandleSelectListAllowedSuccessorsForEntityByIdentifierAndIdsAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<ProcessingStateSelectListEntry>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "SelectListAllSuccessorsForEntityByIdentifierAndIds")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query all possible successor processing states for entity by identifier and instance ids");

        app.MapGet(basePath + "/isstateallowedforentity/{entity}/{state}/{id}", HandleIsStateAllowedForEntityByIdentifierStateAndIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "IsStateAllowedForEntityByIdentifierStateAndId")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query specific processing state allowed for entity by object id");
        
        return app;
    }
    
    private static async Task<IResult> HandleSelectListAllowedSuccessorsForEntityByIdentifierAndIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantGenericProvider tenantGenericProvider, ClaimsPrincipal user, string entity, Guid id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        var rights = principalUtils.GetUserRights(user);

        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var entityMeta = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);
        
        if (tenant == null)
        {
            return Results.NotFound($"Tenant with ID {tenantId} not found.");
        }
        
        if (entityMeta == null || string.IsNullOrEmpty(entityMeta.StateColumn))
        {
            return Results.NotFound($"Entity with identifier {entity} not found or has no state support for tenant {tenantId}.");
        }       
        
        var currentState = await tenantGenericProvider.GetScalarValueAsync(tenant, entityMeta, entityMeta.StateColumn, id, 0);
        
        var possibleStates = await metadataAdapter.SelectListPossibleSuccessorsForEntityAsync(tenantId, entity, currentState);

        var allowedStates = possibleStates?.Where(ps => tenantGenericProvider.StateAllowedAsync(tenant, entityMeta, id, ps.State, claims, rights).Result);

        return Results.Ok(allowedStates);
    }
    
    private static async Task<IResult> HandleSelectListAllowedSuccessorsForEntityByIdentifierAndIdsAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantGenericProvider tenantGenericProvider, ClaimsPrincipal user, string entity, QueryValueBag query)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        var rights = principalUtils.GetUserRights(user);

        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var entityMeta = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null)
        {
            return Results.NotFound($"Tenant with ID {tenantId} not found.");
        }
        
        if (entityMeta == null || string.IsNullOrEmpty(entityMeta.StateColumn))
        {
            return Results.NotFound($"Entity with identifier {entity} not found or has no state support for tenant {tenantId}.");
        }   
        
        if (query.Query.TryGetValue("id", out var ids))
        {
            var listOfStates = (await Task.WhenAll(ids.Select(Guid.Parse).AsParallel().Select(async (id) =>
            {
                var currentState = await tenantGenericProvider.GetScalarValueAsync(tenant, entityMeta, entityMeta.StateColumn, id, 0);
                var possibleStates = await metadataAdapter.SelectListPossibleSuccessorsForEntityAsync(tenantId, entity, currentState);
                var allowedStates = possibleStates?.Where(ps => tenantGenericProvider.StateAllowedAsync(tenant, entityMeta, id, ps.State, claims, rights).GetAwaiter().GetResult());

                return allowedStates;
            })))?.ToList();

            if (listOfStates != null && listOfStates.Count > 1)
            {
                return Results.Ok(listOfStates.Skip(1).Aggregate(new HashSet<ProcessingStateSelectListEntry>(listOfStates[0]), (h, e) =>
                {
                    h.IntersectWith(e);
                    return h;
                }));
            }
            
            if (listOfStates != null && listOfStates.Count == 1)
            {
                return Results.Ok(listOfStates[0]);
            }
        }

        return Results.Ok();
    }
    
    private static async Task<IResult> HandleIsStateAllowedForEntityByIdentifierStateAndIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantGenericProvider tenantGenericProvider, ClaimsPrincipal user, string entity, int state, Guid id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        var rights = principalUtils.GetUserRights(user);

        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var entityMeta = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);
        
        if (tenant == null)
        {
            return Results.NotFound($"Tenant with ID {tenantId} not found.");
        }
    
        if (entityMeta == null || string.IsNullOrEmpty(entityMeta.StateColumn))
        {
            return Results.NotFound($"Entity with identifier {entity} not found or has no state support for tenant {tenantId}.");
        }   
        
        if (await tenantGenericProvider.StateAllowedAsync(tenant, entityMeta, id, state, claims, rights))
        {
            return Results.Ok();
        }
         
        return Results.Forbid();
    }
}