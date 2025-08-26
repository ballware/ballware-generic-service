using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Ballware.Generic.Api.Public;
using Ballware.Shared.Authorization;
using Ballware.Generic.Metadata;
using Ballware.Generic.Tenant.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using MimeTypes;
using Newtonsoft.Json;
using Quartz;

namespace Ballware.Generic.Api.Endpoints;

public class CountResult
{
    [JsonPropertyName("count")]
    public long Count { get; set; }
}

public static class GenericDataEndpoint
{
    private static readonly string DefaultQuery = "primary";
    private static readonly string TenantOrEntityNotFoundError = "Unknown tenant or entity";
    
    public static IEndpointRouteBuilder MapGenericDataApi(this IEndpointRouteBuilder app, 
        string basePath,
        string apiTag = "Generic",
        string apiOperationPrefix = "Generic",
        string authorizationScope = "genericApi",
        string apiGroup = "generic")
    {
        app.MapGet(basePath + "/{application}/{entity}/all", HandleAllAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<object>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "All")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query all");
        
        app.MapGet(basePath + "/{application}/{entity}/query", HandleQueryAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<IEnumerable<object>>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "Query")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query items of generic entity by query identifier and params");
        
        app.MapGet(basePath + "/{application}/{entity}/count", HandleCountAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<CountResult>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "Count")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Count items of generic entity by query identifier and params");
        
        app.MapGet(basePath + "/{application}/{entity}/new", HandleNewAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<object>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "New")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Fetch new item template for entity by identifier");
        
        app.MapGet(basePath + "/{application}/{entity}/newquery", HandleNewQueryAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<object>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "NewQuery")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Fetch new item template for entity by identifier with params");
        
        app.MapGet(basePath + "/{application}/{entity}/byid", HandleByIdAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<object>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "ById")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query existing tenant");
        
        app.MapPost(basePath + "/{application}/{entity}/save", HandleSaveAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "Save")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Save existing or new tenant");
        
        app.MapPost(basePath + "/{application}/{entity}/savebatch", HandleSaveBatchAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "SaveBatch")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Save list of items for entity by identifier");

        
        app.MapDelete(basePath + "/{application}/{entity}/remove/{id}", HandleRemoveAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<object>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "Remove")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Query existing tenant");
        
        app.MapPost(basePath + "/{application}/{entity}/import", HandleImportAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "Import")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Import from file");
        
        app.MapGet(basePath + "/{application}/{entity}/export", HandleExportAsync)
            .RequireAuthorization(authorizationScope)
            .Produces<string>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "Export")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Export by query");
        
        app.MapPost(basePath + "/{application}/{entity}/exporturl", HandleExportUrlAsync)
            .RequireAuthorization(authorizationScope)
            .DisableAntiforgery()
            .Accepts<IFormCollection>("application/x-www-form-urlencoded")
            .Produces<ExportUrlResult>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "ExportUrl")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Export to file by query");
        
        app.MapGet(basePath + "/{application}/{entity}/download/{tenantId}/{id}", HandleDownloadExportAsync)
            .AllowAnonymous()
            .Produces<FileStream>(StatusCodes.Status200OK, contentType: "application/json")
            .Produces(StatusCodes.Status404NotFound)
            .WithName(apiOperationPrefix + "Download")
            .WithGroupName(apiGroup)
            .WithTags(apiTag)
            .WithSummary("Download exported");
        
        return app;
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleAllAsync(IPrincipalUtils principalUtils, ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }

    
        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "view");
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, "view", new Dictionary<string, object>(), tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        return Results.Ok(await genericProvider.AllAsync<dynamic>(tenant, metaData, identifier, claims));
    }
    
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleQueryAsync(IPrincipalUtils principalUtils, ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier, QueryValueBag query)
    {
        var tenantId = principalUtils.GetUserTenandId(user);

        var claims = principalUtils.GetUserClaims(user);
        var queryParams = GetQueryParams(query.Query);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }

        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "view");
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, "view", queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        return Results.Ok(await genericProvider.QueryAsync<dynamic>(tenant, metaData, identifier, claims, queryParams));
    }
    
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleCountAsync(IPrincipalUtils principalUtils, ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier, QueryValueBag query)
    {
        var tenantId = principalUtils.GetUserTenandId(user);

        var claims = principalUtils.GetUserClaims(user);
        var queryParams = GetQueryParams(query.Query);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }
    
        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "view");
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, "view", queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        return Results.Ok(new CountResult { Count = await genericProvider.CountAsync(tenant, metaData, identifier, claims, queryParams) });
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleNewAsync(IPrincipalUtils principalUtils,
        ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier)
    {
        var tenantId = principalUtils.GetUserTenandId(user);

        var claims = principalUtils.GetUserClaims(user);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }

        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "add");
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, "add", new Dictionary<string, object>(), tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        return Results.Ok(await genericProvider.NewAsync<dynamic>(tenant, metaData, identifier, claims));
    }
    
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleNewQueryAsync(IPrincipalUtils principalUtils,
        ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier, QueryValueBag query)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        
        var claims = principalUtils.GetUserClaims(user);
        var queryParams = GetQueryParams(query.Query);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }

        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "add");
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, "add", queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        return Results.Ok(await genericProvider.NewQueryAsync<dynamic>(tenant, metaData, identifier, claims, queryParams));
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleByIdAsync(IPrincipalUtils principalUtils,
        ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier, Guid id)
    {
        var tenantId = principalUtils.GetUserTenandId(user);

        var claims = principalUtils.GetUserClaims(user);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }
        
        var value = await genericProvider.ByIdAsync<dynamic>(tenant, metaData, identifier, claims, id);
        
        if (value == null)
        {
            return Results.NotFound("Item not found");
        }

        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "view");
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, "view", value, tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(value);
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleSaveAsync(IPrincipalUtils principalUtils,
        ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier, BodyValueBag? value)
    {
        var currentUserId = principalUtils.GetUserId(user);
        var tenantId = principalUtils.GetUserTenandId(user);

        var claims = principalUtils.GetUserClaims(user);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }

        if (value == null)
        {
            return Results.BadRequest("No value provided");
        }
        
        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier == DefaultQuery ? "edit" : identifier);
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, identifier == DefaultQuery ? "edit" : identifier, value.Value, tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        await genericProvider.SaveAsync(tenant, metaData, currentUserId, identifier, claims, value.Value);

        return Results.Ok();
    }
    
    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleSaveBatchAsync(IPrincipalUtils principalUtils,
        ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier, BodyValuesBag? values)
    {
        var currentUserId = principalUtils.GetUserId(user);
        var tenantId = principalUtils.GetUserTenandId(user);

        var claims = principalUtils.GetUserClaims(user);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }
        
        if (values == null)
        {
            return Results.BadRequest("No values provided");
        }
    
        foreach (var value in values.Values)
        {
            var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier == DefaultQuery ? "edit" : identifier);
            var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, identifier == DefaultQuery ? "edit" : identifier, value, tenantAuthorized);

            if (!authorized)
            {
                return Results.Unauthorized();
            }
        }
        
        foreach (var value in values.Values)
        {
            await genericProvider.SaveAsync(tenant, metaData, currentUserId, identifier, claims, value);
        }

        return Results.Ok();
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleRemoveAsync(IPrincipalUtils principalUtils,
        ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        Guid id)
    {
        var currentUserId = principalUtils.GetUserId(user);
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }

        var value = await genericProvider.ByIdAsync<dynamic>(tenant, metaData, DefaultQuery, claims, id);

        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "delete");
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, "delete", value, tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        var removeResult = await genericProvider.RemoveAsync(tenant, metaData, currentUserId, claims, id);

        if (!removeResult.Result)
        {
            return Results.BadRequest(new Exception(string.Join("\r\n", removeResult.Messages)));
        }

        return Results.Ok();
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleImportAsync(ISchedulerFactory schedulerFactory,
        IPrincipalUtils principalUtils, ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ClaimsPrincipal user, IGenericFileStorageAdapter storageAdapter,
        string application, string entity,
        string identifier,
        IFormFileCollection files)
    {
        var currentUserId = principalUtils.GetUserId(user);
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        
        foreach (var file in files)
        {
            var temporaryId = Guid.NewGuid();
            
            var jobData = new JobDataMap();

            jobData["tenantId"] = tenantId;
            jobData["userId"] = currentUserId;
            jobData["application"] = application;
            jobData["entity"] = entity;
            jobData["identifier"] = identifier;
            jobData["claims"] = JsonConvert.SerializeObject(claims);
            jobData["file"] = temporaryId;

            await storageAdapter.UploadTemporaryFileBehalfOfUserAsync(tenantId, currentUserId, temporaryId, file.FileName, file.ContentType, file.OpenReadStream());

            var jobPayload = new JobCreatePayload()
            {
                Identifier = "import",
                Scheduler = "generic",
                Options = JsonConvert.SerializeObject(jobData)
            };
            
            var job = await metadataAdapter.CreateJobForTenantBehalfOfUserAsync(tenantId, currentUserId, jobPayload);

            jobData["jobId"] = job;

            await (await schedulerFactory.GetScheduler()).TriggerJob(JobKey.Create("import", "generic"), jobData);
        }

        return Results.Created();
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleExportAsync(
        IPrincipalUtils principalUtils, ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, 
        ITenantGenericProvider genericProvider, ClaimsPrincipal user,
        string application, string entity,
        string identifier, HttpRequest request)
    {
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);
        
        var query = request.Query;
            
        var queryParams = new Dictionary<string, object>();

        foreach (var queryEntry in query)
        {
            queryParams.Add(queryEntry.Key, queryEntry.Value);
        }
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }
    
        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier);
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, identifier, queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        var export = await genericProvider.ExportAsync(tenant, metaData, identifier, claims, queryParams);

        return Results.Content(Encoding.UTF8.GetString(export.Data), export.MediaType);
    }

    [SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "DI injection needed")]
    public static async Task<IResult> HandleExportUrlAsync(IPrincipalUtils principalUtils,
        ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker,
        IMetadataAdapter metadataAdapter, ITenantGenericProvider genericProvider,
        IGenericFileStorageAdapter storageAdapter,
        ClaimsPrincipal user,
        string application, string entity,
        string identifier, HttpRequest request)
    {
        var query = await request.ReadFormAsync();
        var currentUserId = principalUtils.GetUserId(user);
        var tenantId = principalUtils.GetUserTenandId(user);
        var claims = principalUtils.GetUserClaims(user);

        var queryParams = new Dictionary<string, object>();

        foreach (var queryEntry in query)
        {
            queryParams.Add(queryEntry.Key, queryEntry.Value);
        }
        
        var tenant = await metadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await metadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return Results.NotFound(TenantOrEntityNotFoundError);
        }

        var tenantAuthorized = await tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier);
        var authorized = await entityRightsChecker.HasRightAsync(tenantId, metaData, claims, identifier, queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Results.Unauthorized();
        }
        
        var export = await genericProvider.ExportAsync(tenant, metaData, identifier, claims, queryParams);
        
        var exportPayload = new ExportCreatePayload()
        {
            Application = application,
            Entity = entity,
            Query = identifier,
            MediaType = export.MediaType,
            ExpirationStamp = DateTime.Now.AddDays(1)
        };
        
        var exportId = await metadataAdapter.CreateExportForTenantBehalfOfUserAsync(tenantId, currentUserId, exportPayload);
        
        await storageAdapter.UploadTemporaryFileBehalfOfUserAsync(tenantId, currentUserId, exportId, $"{exportId}{MimeTypeMap.GetExtension(export.MediaType)}", export.MediaType, new MemoryStream(export.Data));

        return Results.Ok(new ExportUrlResult()
        {
            TenantId = tenantId,
            Id = exportId 
        });
    }

    public static async Task<IResult> HandleDownloadExportAsync(
        IMetadataAdapter metadataAdapter,
        IGenericFileStorageAdapter storageAdapter, 
        string application, string entity,
        Guid tenantId,
        Guid id)
    {
        var export = await metadataAdapter.FetchExportForTenantByIdAsync(tenantId, id);

        if (export == null || export.ExpirationStamp <= DateTime.Now)
        {
            return Results.NotFound();
        }

        var fileContent = await storageAdapter.TemporaryFileByIdAsync(tenantId, export.Id);

        return Results.File(fileContent, export.MediaType, $"{export.Query}_{DateTime.Now:yyyyMMdd_HHmmss}{MimeTypeMap.GetExtension(export.MediaType)}");
    }
    
    private static Dictionary<string, object> GetQueryParams(IDictionary<string, StringValues> query)
    {
        var queryParams = new Dictionary<string, object>();

        foreach (var queryEntry in query)
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

        return queryParams;
    }
}