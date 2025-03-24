using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using System.Net;
using Ballware.Generic.Authorization;
using Ballware.Generic.Service.ModelBinders;
using Ballware.Generic.Tenant.Data;
using Ballware.Generic.Metadata;
using Microsoft.Extensions.Primitives;
using Ballware.Storage.Client;
using MimeTypes;
using Quartz;

namespace Ballware.Generic.Service.Controllers;

public class CountResult
{
    [JsonProperty("count")]
    public long Count { get; set; }
}


[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class GenericController : ControllerBase
{
    private ISchedulerFactory SchedulerFactory { get; }
    
    private IPrincipalUtils PrincipalUtils { get; }
    private ITenantRightsChecker TenantRightsChecker { get; }
    private IEntityRightsChecker EntityRightsChecker { get; }
    private ITenantStorageProvider StorageProvider { get; }
    private ITenantGenericProvider GenericProvider { get; }
    private IMetadataAdapter MetadataAdapter { get; }
    private BallwareStorageClient StorageClient { get; }

    public GenericController(ISchedulerFactory schedulerFactory, IPrincipalUtils principalUtils, ITenantRightsChecker tenantRightsChecker, IEntityRightsChecker entityRightsChecker, ITenantStorageProvider storageProvider, ITenantGenericProvider genericProvider, IMetadataAdapter metadataAdapter, BallwareStorageClient storageClient)
    {
        SchedulerFactory = schedulerFactory;
        PrincipalUtils = principalUtils;
        TenantRightsChecker = tenantRightsChecker;
        EntityRightsChecker = entityRightsChecker;
        StorageProvider = storageProvider;
        GenericProvider = genericProvider;
        MetadataAdapter = metadataAdapter;
        StorageClient = storageClient;
    }

    [HttpGet]
    [Route("{application}/{entity}/all")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Query all items of generic entity by query identifier",
      Description = "",
      OperationId = "AllForEntityByIdentifier"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "List of items", typeof(IEnumerable<object>), new[] { MimeMapping.KnownMimeTypes.Json })]
    public virtual async Task<IActionResult> All(string application, string entity, [FromQuery] string identifier)
    {
        var tenantId = PrincipalUtils.GetUserTenandId(User);

        var claims = PrincipalUtils.GetUserClaims(User);

        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }

        
        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "view");
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, "view", new Dictionary<string, object>(), tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }
        
        try
        {
            return Ok(await GenericProvider.AllAsync<dynamic>(tenant, metaData, identifier, claims));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
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

    [HttpGet]
    [Route("{application}/{entity}/query")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Query items of generic entity by query identifier and params",
      Description = "",
      OperationId = "QueryForEntityByIdentifierWithParams"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "List of items", typeof(IEnumerable<object>), new[] { MimeMapping.KnownMimeTypes.Json })]
    public virtual async Task<IActionResult> Query(
        string application,
        string entity,
        [FromQuery] string identifier,
        [FromQuery, ModelBinder(typeof(DynamicQueryStringModelBinder))] IDictionary<string, StringValues> query)
    {
        var tenantId = PrincipalUtils.GetUserTenandId(User);

        var claims = PrincipalUtils.GetUserClaims(User);
        var queryParams = GetQueryParams(query);
        
        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }

        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "view");
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, "view", queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await GenericProvider.QueryAsync<dynamic>(tenant, metaData, identifier, claims, queryParams));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpGet]
    [Route("{application}/{entity}/count")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Count items of generic entity by query identifier and params",
      Description = "",
      OperationId = "CountForEntityByIdentifierWithParams"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Count of items", typeof(CountResult), new[] { MimeMapping.KnownMimeTypes.Json })]
    public virtual async Task<IActionResult> Count(
        string application,
        string entity,
        [FromQuery] string identifier,
        [FromQuery, ModelBinder(typeof(DynamicQueryStringModelBinder))] IDictionary<string, StringValues> query)
    {
        var tenantId = PrincipalUtils.GetUserTenandId(User);

        var claims = PrincipalUtils.GetUserClaims(User);
        var queryParams = GetQueryParams(query);

        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }
        
        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "view");
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, "view", queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(new CountResult { Count = await GenericProvider.CountAsync(tenant, metaData, identifier, claims, queryParams) });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpGet]
    [Route("{application}/{entity}/byid")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Fetch single item for entity by id",
      Description = "",
      OperationId = "SingleForEntityById"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Item", typeof(object), new[] { MimeMapping.KnownMimeTypes.Json })]
    public virtual async Task<IActionResult> ById(string application, string entity, [FromQuery] string identifier, [FromQuery] Guid id)
    {
        var tenantId = PrincipalUtils.GetUserTenandId(User);

        var claims = PrincipalUtils.GetUserClaims(User);

        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }

        try
        {
            var value = await GenericProvider.ByIdAsync<dynamic>(tenant, metaData, identifier, claims, id);

            var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "view");
            var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, "view", value, tenantAuthorized);

            if (!authorized)
            {
                return Unauthorized();
            }

            return Ok(value);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpGet]
    [Route("{application}/{entity}/new")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Fetch new item template for entity by identifier",
      Description = "",
      OperationId = "NewForEntityByIdentifier"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Item", typeof(object), new[] { MimeMapping.KnownMimeTypes.Json })]
    public virtual async Task<IActionResult> New(string application, string entity, [FromQuery] string identifier)
    {
        var tenantId = PrincipalUtils.GetUserTenandId(User);

        var claims = PrincipalUtils.GetUserClaims(User);

        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }

        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "add");
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, "add", new Dictionary<string, object>(), tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await GenericProvider.NewAsync<dynamic>(tenant, metaData, identifier, claims));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpGet]
    [Route("{application}/{entity}/newquery")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Fetch new item template for entity by identifier with params",
      Description = "",
      OperationId = "NewForEntityByIdentifierWithParams"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Item", typeof(object), new[] { MimeMapping.KnownMimeTypes.Json })]
    public virtual async Task<IActionResult> NewQuery(
        string application,
        string entity,
        [FromQuery] string identifier,
        [FromQuery, ModelBinder(typeof(DynamicQueryStringModelBinder))] IDictionary<string, StringValues> query)
    {
        var tenantId = PrincipalUtils.GetUserTenandId(User);
        
        var claims = PrincipalUtils.GetUserClaims(User);
        var queryParams = GetQueryParams(query);
        
        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }

        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "add");
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, "add", queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await GenericProvider.NewQueryAsync<dynamic>(tenant, metaData, identifier, claims, queryParams));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpPost]
    [Route("{application}/{entity}/save")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Save item for entity by identifier",
      Description = "",
      OperationId = "SaveForEntityByIdentifier"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Save succeeded")]
    [SwaggerResponse((int)HttpStatusCode.InternalServerError, "Error occured on save")]
    public virtual async Task<IActionResult> Save(
        string application,
        string entity,
        [FromQuery] string identifier,
        [FromBody] Dictionary<string, object> value
    )
    {
        var currentUserId = PrincipalUtils.GetUserId(User);
        var tenantId = PrincipalUtils.GetUserTenandId(User);

        var claims = PrincipalUtils.GetUserClaims(User);
        
        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }

        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier == "primary" ? "edit" : identifier);
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, identifier == "primary" ? "edit" : identifier, value, tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }

        try
        {
            await GenericProvider.SaveAsync(tenant, metaData, currentUserId, identifier, claims, value);

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpPost]
    [Route("{application}/{entity}/savebatch")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Save list of items for entity by identifier",
      Description = "",
      OperationId = "SaveBatchForEntityByIdentifier"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Save succeeded")]
    [SwaggerResponse((int)HttpStatusCode.InternalServerError, "Error occured on save")]
    public virtual async Task<IActionResult> SaveBatch(
        string application,
        string entity,
        [FromQuery] string identifier,
        [FromBody] IEnumerable<Dictionary<string, object>> values
    )
    {
        var currentUserId = PrincipalUtils.GetUserId(User);
        var tenantId = PrincipalUtils.GetUserTenandId(User);

        var claims = PrincipalUtils.GetUserClaims(User);

        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }
        
        foreach (var value in values)
        {
            var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier == "primary" ? "edit" : identifier);
            var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, identifier == "primary" ? "edit" : identifier, value, tenantAuthorized);

            if (!authorized)
            {
                return Unauthorized();
            }
        }

        try
        {
            foreach (var value in values)
            {
                await GenericProvider.SaveAsync(tenant, metaData, currentUserId, identifier, claims, value);
            }

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpDelete]
    [Route("{application}/{entity}/remove/{id}")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Remove item for entity by id",
      Description = "",
      OperationId = "RemoveForEntityById"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.BadRequest, "Remove failed", typeof(string), new[] { MimeMapping.KnownMimeTypes.Text })]
    [SwaggerResponse((int)HttpStatusCode.OK, "Remove succeeded")]
    [SwaggerResponse((int)HttpStatusCode.InternalServerError, "Error occured on remove")]
    public virtual async Task<IActionResult> Remove(string application, string entity, Guid id)
    {
        var currentUserId = PrincipalUtils.GetUserId(User);
        var tenantId = PrincipalUtils.GetUserTenandId(User);
        var claims = PrincipalUtils.GetUserClaims(User);

        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }

        var value = await GenericProvider.ByIdAsync<dynamic>(tenant, metaData, "primary", claims, id);

        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, "delete");
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, "delete", value, tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }

        try
        {
            var removeResult = await GenericProvider.RemoveAsync(tenant, metaData, currentUserId, claims, id);

            if (!removeResult.Result)
            {
                return BadRequest(new Exception(string.Join("\r\n", removeResult.Messages)));
            }

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpPost]
    [Route("{application}/{entity}/import")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Import items for entity by identifier",
      Description = "",
      OperationId = "ImportForEntityByIdentifier"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Import succeeded")]
    public virtual async Task<IActionResult> Import(
        string application,
        string entity,
        [FromQuery] string identifier
    )
    {
        var currentUserId = PrincipalUtils.GetUserId(User);
        var tenantId = PrincipalUtils.GetUserTenandId(User);
        var claims = PrincipalUtils.GetUserClaims(User);

        try
        {
            var files = HttpContext.Request.Form.Files;

            foreach (var file in files)
            {
                if (file != null)
                {
                    var jobData = new JobDataMap();

                    jobData["tenantId"] = tenantId;
                    jobData["userId"] = currentUserId;
                    jobData["application"] = application;
                    jobData["entity"] = entity;
                    jobData["identifier"] = identifier;
                    jobData["claims"] = JsonConvert.SerializeObject(claims);
                    jobData["filename"] = file.FileName;

                    await StorageClient.UploadFileForOwnerAsync(currentUserId.ToString(),
                        new[]
                        {
                            new FileParameter(file.OpenReadStream(), file.FileName, file.ContentType)
                        });

                    var jobPayload = new JobCreatePayload()
                    {
                        Identifier = "import",
                        Scheduler = "generic",
                        Options = JsonConvert.SerializeObject(jobData)
                    };
                    
                    var job = await MetadataAdapter.CreateJobForTenantBehalfOfUserAsync(tenantId, currentUserId, jobPayload);

                    jobData["jobId"] = job;

                    await (await SchedulerFactory.GetScheduler()).TriggerJob(JobKey.Create("import", "generic"), jobData);
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpGet]
    [Route("{application}/{entity}/export")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Export items for entity by identifier with params",
      Description = "",
      OperationId = "ExportForEntityByIdentifierWithParams"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Exported items", typeof(string), new[] { MimeMapping.KnownMimeTypes.Text, MimeMapping.KnownMimeTypes.Json, MimeMapping.KnownMimeTypes.Xml, MimeMapping.KnownMimeTypes.Csv })]
    public virtual async Task<IActionResult> Export(
        string application,
        string entity,
        [FromQuery] string identifier,
        [FromQuery, ModelBinder(typeof(DynamicQueryStringModelBinder))] IDictionary<string, StringValues> query
    )
    {

        var tenantId = PrincipalUtils.GetUserTenandId(User);
        var claims = PrincipalUtils.GetUserClaims(User);
        var queryParams = GetQueryParams(query);

        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }
        
        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier);
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, identifier, queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }

        try
        {
            var export = await GenericProvider.ExportAsync(tenant, metaData, identifier, claims, queryParams);

            return Content(Encoding.UTF8.GetString(export.Data), export.MediaType);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    [Route("{application}/{entity}/exporturl")]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Download itemsfor entity by identifier with params",
      Description = "",
      OperationId = "DownloadForEntityByIdentifierWithParams"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Entity not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Identifier of export file", typeof(string), new[] { MimeMapping.KnownMimeTypes.Text })]
    public virtual async Task<IActionResult> ExportUrl(
        string application,
        string entity,
        [FromQuery] string identifier,
        [FromForm] IFormCollection query
    )
    {

        var currentUserId = PrincipalUtils.GetUserId(User);
        var tenantId = PrincipalUtils.GetUserTenandId(User);
        var claims = PrincipalUtils.GetUserClaims(User);

        var queryParams = new Dictionary<string, object>();

        foreach (var queryEntry in query)
        {
            queryParams.Add(queryEntry.Key, queryEntry.Value);
        }
        
        var tenant = await MetadataAdapter.MetadataForTenantByIdAsync(tenantId);
        var metaData = await MetadataAdapter.MetadataForEntityByTenantAndIdentifierAsync(tenantId, entity);

        if (tenant == null || metaData == null)
        {
            return NotFound("Unknown tenant or entity");
        }

        var tenantAuthorized = await TenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier);
        var authorized = await EntityRightsChecker.HasRightAsync(tenantId, metaData, claims, identifier, queryParams, tenantAuthorized);

        if (!authorized)
        {
            return Unauthorized();
        }

        try
        {
            var export = await GenericProvider.ExportAsync(tenant, metaData, identifier, claims, queryParams);
            
            var exportPayload = new ExportCreatePayload()
            {
                Application = application,
                Entity = entity,
                Query = identifier,
                MediaType = export.MediaType,
                ExpirationStamp = DateTime.Now.AddDays(1)
            };
            
            var exportId = await MetadataAdapter.CreateExportForTenantBehalfOfUserAsync(tenantId, currentUserId, exportPayload);
            
            await StorageClient.UploadFileForOwnerAsync("export", new[] { new FileParameter(new MemoryStream(export.Data), $"{exportId}{MimeTypeMap.GetExtension(export.MediaType)}", export.MediaType) });
            
            return Ok(exportId.ToString());
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, ex);
        }
    }

    [HttpGet]
    [Route("{application}/{entity}/download")]
    [AllowAnonymous]
    [ApiExplorerSettings(GroupName = "generic")]
    [SwaggerOperation(
      Summary = "Download export by id",
      Description = "",
      OperationId = "DownloadExportById"
    )]
    [SwaggerResponse((int)HttpStatusCode.NotFound, "Export not found")]
    [SwaggerResponse((int)HttpStatusCode.OK, "Exported file", typeof(FileContentResult), new[] { MimeMapping.KnownMimeTypes.Text, MimeMapping.KnownMimeTypes.Json, MimeMapping.KnownMimeTypes.Xml, MimeMapping.KnownMimeTypes.Csv })]
    public virtual async Task<IActionResult> Download(
        [FromQuery] Guid id
    )
    {
        var tenantId = PrincipalUtils.GetUserId(User);
        
        var export = await MetadataAdapter.FetchExportByIdForTenantAsync(tenantId, id);

        if (export != null && export.ExpirationStamp > DateTime.Now)
        {
            //await _fileStorage.OpenFileAsync("export", export.Uuid.ToString());
        }
        else
        {
            return NotFound();
        }

        var fileContent = await StorageClient.FileByNameForOwnerAsync("export", $"{export.Id}{MimeTypeMap.GetExtension(export.MediaType)}");
        
        return File(fileContent.Stream, export.MediaType, $"{export.Query}_{DateTime.Now:yyyyMMdd_HHmmss}{MimeTypeMap.GetExtension(export.MediaType)}");
    }
}