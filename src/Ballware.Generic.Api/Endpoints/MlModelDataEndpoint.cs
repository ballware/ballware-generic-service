using System;
using System.Collections.Generic;
using System.Security.Claims;
using Ballware.Generic.Authorization;
using Ballware.Generic.Metadata;
using Ballware.Generic.Tenant.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ballware.Generic.Api.Endpoints;

public static class MlModelDataEndpoint
{
    public static IEndpointRouteBuilder MapMlModelDataApi(this IEndpointRouteBuilder app, 
        string basePath,
        string apiTag = "MlModel",
        string apiOperationPrefix = "MlModel",
        string authorizationScope = "serviceApi",
        string apiGroup = "service")
    {
        app.MapGet(basePath + "/trainingdatabytenantandid/{tenantId}/{id}", HandleTrainingDataByTenantAndIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<object>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "TrainingDataByTenantAndId")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query model training data by tenant and id");
        
        return app;
    }
    
    public static async Task<IResult> HandleTrainingDataByTenantAndIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantMlModelProvider modelProvider, ClaimsPrincipal user, Guid tenantId, Guid id)
    {
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var model = await metadataAdapter.MetadataForMlModelByTenantAndIdAsync(tenantId, id);
            
            var data = await modelProvider.TrainDataByModelAsync<dynamic>(tenant, model);
            
            return Results.Ok(data);
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
}