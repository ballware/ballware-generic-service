using System.Security.Claims;
using Ballware.Generic.Metadata;
using Ballware.Generic.Tenant.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        
        app.MapPost(basePath + "/trainingdatabytenantandplainquery/{tenantId}", HandleTrainingDataByTenantAndPlainQueryAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<object>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "TrainingDataByTenantAndPlainQuery")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query model training data by tenant and sql query");
        
        return app;
    }
    
    private static async Task<IResult> HandleTrainingDataByTenantAndIdAsync(IMetadataAdapter metadataAdapter, ITenantMlModelProvider modelProvider, Guid tenantId, Guid id)
    {
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var model = await metadataAdapter.MetadataForMlModelByTenantAndIdAsync(tenantId, id);
        
        if (tenant == null)
        {
            return Results.NotFound($"Tenant with ID {tenantId} not found.");
        }
        
        if (model == null)
        {
            return Results.NotFound($"Model with ID {id} not found for tenant {tenantId}.");
        }        
        
        return Results.Ok(await modelProvider.TrainDataByModelAsync<dynamic>(tenant, model));
    }
    
    private static async Task<IResult> HandleTrainingDataByTenantAndPlainQueryAsync(IMetadataAdapter metadataAdapter, ITenantMlModelProvider modelProvider, Guid tenantId, [FromBody] string query)
    {
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        
        if (tenant == null)
        {
            return Results.NotFound($"Tenant with ID {tenantId} not found.");
        }

        if (string.IsNullOrEmpty(query))
        {
            return Results.BadRequest("Query is empty.");
        }
        
        return Results.Ok(await modelProvider.TrainDataByPlainQueryAsync<dynamic>(tenant, query));
    }
}