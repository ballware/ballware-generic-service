using System.Security.Claims;
using Ballware.Generic.Tenant.Data;
using Ballware.Generic.Authorization;
using Ballware.Generic.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ballware.Generic.Api.Endpoints;

public static class StatisticDataEndpoint
{
    public static IEndpointRouteBuilder MapStatisticDataApi(this IEndpointRouteBuilder app, 
        string basePath,
        string apiTag = "Statistic",
        string apiOperationPrefix = "Statistic",
        string authorizationScope = "metaApi",
        string apiGroup = "meta")
    {
        app.MapGet(basePath + "/dataforidentifier/{identifier}", HandleDataByIdentifierAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<object>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "PayloadByIdentifier")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query data payload for statistic by identifier");
        
        return app;
    }
    
    private static async Task<IResult> HandleDataByIdentifierAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantStatisticProvider statisticProvider, ClaimsPrincipal user, string identifier, QueryValueBag query)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var currentUserId = principalUtils.GetUserId(user);
        var claims = principalUtils.GetUserClaims(user);
        var queryParams = new Dictionary<string, object>();

        foreach (var queryEntry in query.Query)
        {
            if (queryEntry.Value.Count > 1)
            {
                queryParams.Add(queryEntry.Key, $"|{string.Join('|', queryEntry.Value)}|");
            }
            else
            {
                queryParams.Add(queryEntry.Key, queryEntry.Value);
            }
        }

        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var statistic = await metadataAdapter.MetadataForStatisticByTenantAndIdentifierAsync(tenantId, identifier);
        
        if (tenant == null)
        {
            return Results.NotFound($"Tenant with ID {tenantId} not found.");
        }
        
        if (statistic == null)
        {
            return Results.NotFound($"Statistic with identifier {identifier} not found for tenant {tenantId}.");
        }        
        
        return Results.Ok(await statisticProvider.FetchDataAsync(tenant, statistic, currentUserId, claims, queryParams));
    }
}