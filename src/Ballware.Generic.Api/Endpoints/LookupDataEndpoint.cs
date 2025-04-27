using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Claims;
using Ballware.Generic.Authorization;
using Ballware.Generic.Metadata;
using Ballware.Generic.Tenant.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Ballware.Generic.Api.Endpoints;

public static class LookupDataEndpoint
{
    public static IEndpointRouteBuilder MapLookupUserDataApi(this IEndpointRouteBuilder app, 
        string basePath,
        string apiTag = "Lookup",
        string apiOperationPrefix = "Lookup",
        string authorizationScope = "metaApi",
        string apiGroup = "meta")
    {
        app.MapGet(basePath + "/selectlistforlookup/{lookupId}", HandleSelectListForLookupIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<object>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "SelectListForLookupId")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query entries for lookup by id");
        
        app.MapGet(basePath + "/selectbyidforlookup/{lookupId}/{id}", HandleSelectByIdForLookupIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<object>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "SelectByIdForLookupId")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query single entry for lookup by id");
        
        app.MapGet(basePath + "/selectlistforlookupidentifier/{identifier}", HandleSelectListForLookupIdentifierAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<object>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "SelectListForLookupIdentifier")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query entries for lookup by identifier");
        
        app.MapGet(basePath + "/selectbyidforlookupidentifier/{identifier}/{id}", HandleSelectByIdForLookupIdentifierAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<object>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "SelectByIdForLookupIdentifier")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query single entry for lookup by identifier");
        
        app.MapGet(basePath + "/selectlistforlookupwithparam/{lookupId}/{param}", HandleSelectListForLookupIdAndParamAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<object>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "SelectListForLookupIdAndParam")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query entries for lookup with param by id");
        
        app.MapGet(basePath + "/selectbyidforlookupwithparam/{lookupId}/{param}/{id}", HandleSelectByIdForLookupIdAndParamAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<object>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "SelectByIdForLookupIdAndParam")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query single entry for lookup with param by id");
        
        app.MapGet(basePath + "/autocompleteforlookup/{lookupId}", HandleAutocompleteForLookupIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<string>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "AutocompleteForLookupId")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query autocomplete entries for lookup by id");
        
        app.MapGet(basePath + "/autocompleteforlookupwithparam/{lookupId}/{param}", HandleAutocompleteForLookupIdWithParamAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<string>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .WithName(apiOperationPrefix + "AutocompleteForLookupIdWithParam")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query autocomplete entries for lookup by id with param");
        
        return app;
    }
    
    public static IEndpointRouteBuilder MapLookupServiceDataApi(this IEndpointRouteBuilder app, 
            string basePath,
            string apiTag = "Lookup",
            string apiOperationPrefix = "Lookup",
            string authorizationScope = "serviceApi",
            string apiGroup = "service")
        {
            app.MapGet(basePath + "/columnvaluebytenantandidforlookup/{tenantId}/{lookupId}/{id}/{column}", HandleSelectColumnValueByIdForTenantAndLookupIdAsync)
                .RequireAuthorization(authorizationScope)
                .Produces<object>()
                .Produces(StatusCodes.Status401Unauthorized)
                .WithName(apiOperationPrefix + "SelectColumnValueByIdForTenantAndLookupId")
                .WithGroupName(apiGroup)
                .WithTags(apiTag)
                .WithSummary("Query single column value for lookup by tenant and id");
            
            return app;
        }
    
    public static async Task<IResult> HandleSelectListForLookupIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, ClaimsPrincipal user, Guid lookupId)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var rights = principalUtils.GetUserRights(user);
        
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdAsync(tenantId, lookupId);
                
            return Results.Ok(await lookupProvider.SelectListForLookupAsync<dynamic>(
                tenant, 
                lookup, rights));
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleSelectByIdForLookupIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, ClaimsPrincipal user, Guid lookupId, string id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var rights = principalUtils.GetUserRights(user);
        
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdAsync(tenantId, lookupId);
                
            return Results.Ok(await lookupProvider.SelectByIdForLookupAsync<dynamic>(
                tenant, 
                lookup, rights, id));
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleSelectListForLookupIdentifierAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, ClaimsPrincipal user, string identifier)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var rights = principalUtils.GetUserRights(user);
        
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdentifierAsync(tenantId, identifier);
                
            return Results.Ok(await lookupProvider.SelectListForLookupAsync<dynamic>(
                tenant, 
                lookup, rights));
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleSelectByIdForLookupIdentifierAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, ClaimsPrincipal user, string identifier, string id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var rights = principalUtils.GetUserRights(user);
        
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdentifierAsync(tenantId, identifier);
                
            return Results.Ok(await lookupProvider.SelectByIdForLookupAsync<dynamic>(
                tenant, 
                lookup, rights, id));
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleSelectListForLookupIdAndParamAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, ClaimsPrincipal user, Guid lookupId, string param)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var rights = principalUtils.GetUserRights(user);
        
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdAsync(tenantId, lookupId);
                
            return Results.Ok(await lookupProvider.SelectListForLookupWithParamAsync<dynamic>(
                tenant, 
                lookup, rights, param));
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleSelectByIdForLookupIdAndParamAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, ClaimsPrincipal user, Guid lookupId, string id, string param)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var rights = principalUtils.GetUserRights(user);
        
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdAsync(tenantId, lookupId);
                
            return Results.Ok(await lookupProvider.SelectByIdForLookupWithParamAsync<dynamic>(
                tenant, 
                lookup, rights, id, param));
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleAutocompleteForLookupIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, ClaimsPrincipal user, Guid lookupId)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var rights = principalUtils.GetUserRights(user);
        
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdAsync(tenantId, lookupId);
                
            return Results.Ok(await lookupProvider.AutocompleteForLookupAsync(
                tenant, 
                lookup, rights));
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleAutocompleteForLookupIdWithParamAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, ClaimsPrincipal user, Guid lookupId, string param)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var rights = principalUtils.GetUserRights(user);
        
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdAsync(tenantId, lookupId);
                
            return Results.Ok(await lookupProvider.AutocompleteForLookupWithParamAsync(
                tenant, 
                lookup, rights, param));
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
    
    public static async Task<IResult> HandleSelectColumnValueByIdForTenantAndLookupIdAsync(IPrincipalUtils principalUtils, IMetadataAdapter metadataAdapter, ITenantLookupProvider lookupProvider, Guid tenantId, Guid lookupId, string id, string column)
    {
        try
        {
            var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
            var lookup = await metadataAdapter.MetadataForLookupByTenantAndIdAsync(tenantId, lookupId);
                
            var result = await lookupProvider.SelectByIdForLookupAsync<dynamic>(
                tenant, 
                lookup, ImmutableArray<string>.Empty, id);
            
            if (result is IDictionary<string, object> resultDict && resultDict.TryGetValue(column, out object? value))
            {
                return Results.Ok(value);
            }
            
            return Results.NotFound($"Column '{column}' not found in the result.");
        }
        catch (Exception ex)
        {
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: ex.Message, detail: ex.StackTrace);
        }
    }
}