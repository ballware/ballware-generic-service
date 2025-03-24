using System.Net;
using Ballware.Generic.Service.Dtos;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Ballware.Generic.Service.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class SchemaController : ControllerBase
{
    public SchemaController()
    {
        
    }

    [HttpPost]
    [Route("createorupdateentityschemafortenant/{tenant}")]
    [ApiExplorerSettings(GroupName = "service")]
    [Authorize("serviceApi")]
    [SwaggerOperation(
        Summary = "Create or update entity schema in tenant database",
        Description = "",
        OperationId = "CreateOrUpdateEntitySchemaForTenant"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Create or update successful")]
    public async Task<IActionResult> CreateOrUpdateEntitySchemaForTenantAndEntityAsync(Guid tenant, [FromBody] ServiceCreateEntitySchemaDto payload)
    {
        return Ok();
    }
    
    [HttpPost]
    [Route("dropentityschemafortenantbyidentifier/{tenant}")]
    [ApiExplorerSettings(GroupName = "service")]
    [Authorize("serviceApi")]
    [SwaggerOperation(
        Summary = "Drop entity schema in tenant database by identifier",
        Description = "",
        OperationId = "DropEntitySchemaForTenantByIdentifier"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Drop successful")]
    public async Task<IActionResult> DropEntitySchemaForTenantByIdentifierAsync(Guid tenant, [FromBody] ServiceDropEntitySchemaDto payload)
    {
        return Ok();
    }
    
    [HttpPost]
    [Route("createorupdatedatabaseobjectfortenant/{tenant}")]
    [ApiExplorerSettings(GroupName = "service")]
    [Authorize("serviceApi")]
    [SwaggerOperation(
        Summary = "Create or update database object in tenant database",
        Description = "",
        OperationId = "CreateOrUpdateDatabaseObjectForTenant"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Create or update successful")]
    public async Task<IActionResult> CreateOrUpdateDatabaseObjectForTenantAsync(Guid tenant, [FromBody] ServiceCreateDatabaseObjectDto payload)
    {
        return Ok();
    }
    
    [HttpPost]
    [Route("dropdatabaseobjectfortenant/{tenant}")]
    [ApiExplorerSettings(GroupName = "service")]
    [Authorize("serviceApi")]
    [SwaggerOperation(
        Summary = "Drop database object in tenant database",
        Description = "",
        OperationId = "DropDatabaseObjectForTenant"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Drop successful")]
    public async Task<IActionResult> DropDatabaseObjectForTenantAsync(Guid tenant, [FromBody] ServiceDropDatabaseObjectDto payload)
    {
        return Ok();
    }

    [HttpPost]
    [Route("createtenantschema/{tenant}")]
    [ApiExplorerSettings(GroupName = "service")]
    [Authorize("serviceApi")]
    [SwaggerOperation(
        Summary = "Create tenant schema",
        Description = "",
        OperationId = "CreateTenantSchema"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.Created, "Create successful")]
    public async Task<IActionResult> CreateTenantSchemaAsync(Guid tenant, [FromBody] ServiceCreateTenantSchemaDto payload)
    {
        return Created();
    }

    [HttpPost]
    [Route("droptenantschema/{tenant}")]
    [ApiExplorerSettings(GroupName = "service")]
    [Authorize("serviceApi")]
    [SwaggerOperation(
        Summary = "Drop tenant schema",
        Description = "",
        OperationId = "DropTenantSchema"
    )]
    [SwaggerResponse((int)HttpStatusCode.Unauthorized)]
    [SwaggerResponse((int)HttpStatusCode.OK, "Drop successful")]
    public async Task<IActionResult> DropTenantSchemaAsync(Guid tenant, [FromBody] ServiceDropTenantSchemaDto payload)
    {
        return Ok();
    }
    
    
}