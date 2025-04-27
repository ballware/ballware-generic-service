using System;
using System.Collections.Generic;
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
    
    public static async Task<IResult> HandleSelectListAllowedSuccessorsForEntityByIdentifierAndIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantGenericProvider tenantGenericProvider, ClaimsPrincipal user, string entity, Guid id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        var rights = principalUtils.GetUserRights(user);

        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var entityMeta = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);
            
            var currentState = await tenantGenericProvider.GetScalarValueAsync(tenant, entityMeta, entityMeta.StateColumn, id, 0);
            
            var possibleStates = await metadataAdapter.SelectListPossibleSuccessorsForEntityAsync(tenantId, entity, currentState);

            var allowedStates = possibleStates?.Where(ps => tenantGenericProvider.StateAllowedAsync(tenant, entityMeta, id, ps.State, claims, rights).Result);

            return Results.Ok(allowedStates);
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleSelectListAllowedSuccessorsForEntityByIdentifierAndIdsAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantGenericProvider tenantGenericProvider, ClaimsPrincipal user, string entity, QueryValueBag query)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        var rights = principalUtils.GetUserRights(user);

        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var entityMeta = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

            if (query.Query.TryGetValue("id", out var ids))
            {
                var listOfStates = await Task.WhenAll(ids.Select(Guid.Parse).AsParallel().Select(async (id) =>
                {
                    var currentState = await tenantGenericProvider.GetScalarValueAsync(tenant, entityMeta, entityMeta.StateColumn, id, 0);
                    var possibleStates = metadataAdapter.SelectListPossibleSuccessorsForEntityAsync(tenantId, entity, currentState).Result;
                    var allowedStates = possibleStates?.Where(ps => tenantGenericProvider.StateAllowedAsync(tenant, entityMeta, id, ps.State, claims, rights).GetAwaiter().GetResult());

                    return allowedStates;
                }));

                if (listOfStates.Count() > 1)
                {
                    return Results.Ok(listOfStates.Skip(1).Aggregate(new HashSet<ProcessingStateSelectListEntry>(listOfStates.First()), (h, e) =>
                    {
                        h.IntersectWith(e);
                        return h;
                    }));
                }
                else if (listOfStates.Count() == 1)
                {
                    return Results.Ok(listOfStates.First());
                }
            }

            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleIsStateAllowedForEntityByIdentifierStateAndIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantGenericProvider tenantGenericProvider, ClaimsPrincipal user, string entity, int state, Guid id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        var rights = principalUtils.GetUserRights(user);

        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var entityMeta = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);
            
            if (await tenantGenericProvider.StateAllowedAsync(tenant, entityMeta, id, state, claims, rights))
            {
                return Results.Ok();
            }
            else
            {
                return Results.Forbid();
            }
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
}