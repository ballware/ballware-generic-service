# MCP endpoint

`/generic/mcp` exposes entity **read** queries as MCP tools so an AI agent can query tenant data with the
same rights as the user. It is **not part of any OpenAPI document** and has no OpenAPI schema.

`GenericTools.cs` means `src/Ballware.Generic.Mcp/Endpoints/GenericTools.cs`.

## Availability and mount

```csharp
if (mcpEndpointOptions != null && mcpEndpointOptions.Enabled)
{
    logger.LogInformation("Enabling MCP endpoint at /generic/mcp");
    app.MapBallwareUserMcpEndpoint("/generic/mcp", mcpEndpointOptions);
}
```

`src/Ballware.Generic.Service/Startup.cs:505-511` (options resolved at `:500`). Tools are registered
separately and unconditionally:

```csharp
Services.AddBallwareMcpTools((sp, registry) => registry.RegisterBallwareGenericTools());   // Startup.cs:400-403
Services.AddBallwareMcpEndpoint(mcpOptions);                                               // Startup.cs:405
```

`RegisterBallwareGenericTools` registers a **dynamic tool provider**
(`GenericTools.cs:14-19` → `registry.RegisterDynamicToolProvider(EntityTools.GetEntityFetchToolsAsync)`), so
the tool list is computed **per user and per request**.

The endpoint type itself (`McpEndpointOptions`, `IToolRegistry`, `Tool`, `ToolParam`, `ToolResult`,
`MapBallwareUserMcpEndpoint`) comes from the external package `Ballware.Shared.Mcp.Endpoints` 1.2.0
(`src/Ballware.Generic.Service/Ballware.Generic.Service.csproj:15`), as does the protocol implementation —
MCP transport internals are not inspectable from this repository.

## Options

`McpEndpointOptions` is bound from the `Mcp` section with `ValidateOnStart`
(`Startup.cs:89-91`); the DataAnnotations live in the external package.

| Property | Used for | Default when the `Mcp` section is absent |
| --- | --- | --- |
| `Enabled` | mount gate (`Startup.cs:505`) | not mounted (`mcpOptions == null` → the fallback object has no `Enabled`, so the gate is false) |
| `RequiredMcpScope` | required scope on the user's token | `Authorization.RequiredMetaScope` (`Startup.cs:133`) |
| `ResourceUri` | MCP resource URI | `string.Empty` (`Startup.cs:134`) |

Fallback object: `Startup.cs:129-136`. Neither `appsettings.json` nor `appsettings.Development.json` contains
an `Mcp` section, so out of the box the endpoint is **not mounted**. `appsettings.Swagger.json:25-27` sets
`Enabled: false`, `RequiredMcpScope: "openid"`, `ResourceUri: "http://localhost"` for the Swagger
environment.

Authentication uses a **user JWT** (the tool provider reads the user's tenant claim), not a service token.

## Which tools appear

Enumeration (`GenericTools.cs:24-44`):

1. unauthenticated → `UnauthorizedAccessException("User is not authenticated.")` (`:29-32`);
2. tenants from `principalUtils.GetUserTenantIds(user)` — **all** tenants of the user (`:36`);
3. entities per tenant from `IMetadataAdapter.SelectListForEntityAsync(tenantId)` (`:40`);
4. entity metadata per entity (`:44`);
5. only `ListQuery` entries with `AiEnabled` (`:46`) and `ByIdQuery` entries with `AiEnabled` (`:111`).

`AiEnabled` (`src/Ballware.Generic.Metadata/Entity.cs:29`) is mapped from the meta service's `ai_enabled`
(`src/Ballware.Generic.Service/Mappings/MetaServiceGenericMetadataProfile.cs:10-11`).
`NewQuery` and `SaveStatement` are **never** exposed — the tools are read-only, and no `save`, `remove` or
`import` tool exists.

## Tool naming, description, input parameters

| Tool | Name pattern | Citation |
| --- | --- | --- |
| list query | `generic_{tenantId}_{entity}_query_{queryIdentifier}` | `GenericTools.cs:56` |
| by-id query | `generic_{tenantId}_{entity}_byid_{queryIdentifier}` | `GenericTools.cs:121` |

`{tenantId}` is the **enumerated** tenant GUID. Description is `query.Description ?? ""` (`:57,122`).

Parameters are generated from `QueryEntry.Parameters` (`QueryParameter { Name, Type, Description }`,
`src/Ballware.Generic.Metadata/Entity.cs:6-13`) with **every declared parameter forced to
`required: true`** (`GenericTools.cs:63,128`):

| `parameters[].type` in metadata | tool parameter type | note |
| --- | --- | --- |
| `string` | string | case-insensitive match |
| `number` | number | |
| `boolean` | boolean | `bool` does **not** match and falls through to string |
| anything else | string | default branch (`GenericTools.cs:196-205`) |

The by-id tools additionally **require an `id` parameter at execution** (`:171-174`: missing or
non-parseable → `ArgumentException("Missing or invalid id parameter.")`) although `id` is not generated from
metadata — declare it in `parameters` or the agent will be asked for an undeclared argument.

Missing declared parameters fail at execution: `ArgumentException("Missing required query parameters: …")`
(`:68-76`).

## Output schema

Built with NJsonSchema from `QueryEntry.ResultColumns` (`QueryResultParameter { Name, Type, Description }`,
`Entity.cs:15-22`), `GenericTools.cs:218-248`:

```json
{
  "title": "<queryIdentifier>",
  "description": "<query description>",
  "type": "object",
  "properties": {
    "results": {
      "type": "array",
      "items": {
        "type": "object",
        "properties": { "<column>": { "description": "<column description>" } }
      }
    }
  }
}
```

Result values are serialized into `ToolResult.StructuredContent` as `{ "results": [ … ] }`
(`GenericTools.cs:103-106,183-186`).

> **Casing difference:** MCP results use **camelCase**
> (`JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`,
> `src/Ballware.Generic.Mcp/Endpoints/JsonSchemaDefaults.cs:8-16`), while the REST endpoints use PascalCase
> (`Startup.cs:234-242`). A row that is `Id` via `/byid` arrives as `id` via MCP.

## Checks performed at execution

| Check | List tool | By-id tool |
| --- | --- | --- |
| user present | `:84-86` | `:133-136` (`UnauthorizedAccessException("Authenticated user needed to access this entity.")`) |
| tenant resolves | `:88-89` (`InvalidOperationException("Tenant not found.")`) | `:154-161` |
| entity resolves | `:90-91` (`InvalidOperationException("Entity '<id>' not found.")`) | same |
| rights re-check | `view` only, tenant + entity (`:93-94`) | `view` only (`:163-164`); failure → `UnauthorizedAccessException("User is not authorized to access this entity.")` |
| query identifier as right | **no** — the identifier is never used as a right name | same |
| `id` required | n/a | `:171-174`; not found → `ArgumentException("Item not found.")` (`:178-181`) |
| data call | `ITenantGenericProvider.QueryAsync(…, capturedQueryIdentifier, …)` (`:101`) | `ByIdAsync` (`:183`) |

Two caveats to state plainly:

- Enumeration iterates **all** of the user's tenants (`GetUserTenantIds`), but execution resolves tenant,
  entity and metadata from the **single** `GetUserTenandId` value (`:84,88-91,154-161`) while the rights call
  uses the enumerated `tenantId` (`:94,164`). For a user whose token carries several tenants this is
  inconsistent — the safest assumption is one tenant per token.
- Because only `view` is checked, a tool backed by a query identifier that would be restricted by a
  `save`-style right is still callable by anyone who can view the entity.

## Not in the OpenAPI documents

- All four snapshots contain **zero** `mcp` paths (`docs/openapi/generic.json` has exactly the 13
  `/generic/...` paths), and `docs/openapi/manifest.json` records path counts 13/12/3/4.
- The MCP endpoint is mapped inside the external `MapBallwareUserMcpEndpoint` without
  `WithGroupName`/`WithTags`/`WithName` (`Startup.cs:510`), so it belongs to no Swagger group.
- ApiExplorer itself is registered unconditionally (`AddMvcCore().AddApiExplorer()` `Startup.cs:246-247`,
  `AddEndpointsApiExplorer()` `Startup.cs:407`); whether the MCP route participates in ApiExplorer is decided
  by the external package — this repository neither opts in nor opts out.

Generate MCP tool knowledge from entity metadata (`ai_enabled`, `parameters`, `result_columns`), not from the
OpenAPI documents. Conventions for those metadata fields:
[../best-practices/metadata-driven-payloads.md](../best-practices/metadata-driven-payloads.md).

## Related pages

- [tenancy-and-authorization.md](tenancy-and-authorization.md) — scope vs. rights
- [metadata-driven-execution.md](metadata-driven-execution.md) — what `QueryAsync`/`ByIdAsync` actually run
- [../api/README.md](../api/README.md#token-acquisition) — which scope an agent token needs
