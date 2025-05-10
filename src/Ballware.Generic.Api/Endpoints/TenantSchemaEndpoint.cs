using System;
using System.Collections.Generic;
using System.Linq;
using Ballware.Generic.Api.Public;
using Ballware.Generic.Tenant.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ballware.Generic.Api.Endpoints;

public static class TenantSchemaEndpoint
{
    public static IEndpointRouteBuilder MapTenantServiceSchemaApi(this IEndpointRouteBuilder app,
        string basePath,
        string apiTag = "Tenant",
        string apiOperationPrefix = "Tenant",
        string authorizationScope = "schemaApi",
        string apiGroup = "schema")
    {
        app.MapPost(basePath + "/createorupdateentityschemafortenant/{tenantId}", HandleCreateOrUpdateEntitySchemaForTenantBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "CreateOrUpdateEntitySchemaForTenant")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Create or update entity schema in tenant database");
        
        app.MapDelete(basePath + "/dropentityschemafortenant/{tenantId}/{identifier}", HandleDropEntitySchemaForTenantBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropEntitySchemaForTenant")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop entity schema in tenant database by identifier");
        
        app.MapPost(basePath + "/createorupdateschemafortenant/{tenantId}", HandleCreateOrUpdateSchemaForTenantBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "CreateOrUpdateSchemaForTenant")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Create or update schema in tenant database");
        
        app.MapDelete(basePath + "/dropschemafortenant/{tenantId}", HandleDropSchemaForTenantBehalfOfUserAsync)
            .RequireAuthorization(authorizationScope)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "DropSchemaForTenant")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Drop schema in tenant database");

        return app;
    }

    public static async Task<IResult> HandleCreateOrUpdateEntitySchemaForTenantBehalfOfUserAsync(ITenantSchemaProvider tenantSchemaProvider, Guid tenantId, EntitySchema payload)
    {
        try
        {
            await tenantSchemaProvider.CreateOrUpdateEntityAsync(tenantId, payload.SerializedEntityModel, payload.UserId);
        
            return Results.Ok();   
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleDropEntitySchemaForTenantBehalfOfUserAsync(ITenantSchemaProvider tenantSchemaProvider, Guid tenantId, string identifier, Guid? userId = null)
    {
        try
        {
            await tenantSchemaProvider.DropEntityAsync(tenantId, identifier, userId);
        
            return Results.Ok();   
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleCreateOrUpdateSchemaForTenantBehalfOfUserAsync(ITenantSchemaProvider tenantSchemaProvider, Guid tenantId, TenantSchema payload)
    {
        try
        {
            await tenantSchemaProvider.CreateOrUpdateTenantAsync(tenantId, payload.Provider, payload.SerializedTenantModel, payload.UserId);
        
            return Results.Ok();  
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleDropSchemaForTenantBehalfOfUserAsync(ITenantSchemaProvider tenantSchemaProvider, Guid tenantId, Guid? userId = null)
    {
        try
        {
            await tenantSchemaProvider.DropTenantAsync(tenantId, userId);
        
            return Results.Ok();
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
}