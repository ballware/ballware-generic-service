# Tenancy and authorization

Three distinct gates are involved, and they are frequently confused:

1. **Authentication** — a JWT from the configured OIDC authority
   (`src/Ballware.Generic.Service/Startup.cs:175-189`).
2. **Scope policy** — the `scope` claim must contain the scope value required by the endpoint's policy
   (`Startup.cs:191-219`). This is the only gate on the `meta`, `service` and `schema` documents.
3. **Rights evaluation** — a tenant-level and an entity-level right check, optionally driven by metadata
   scripts. Only the **Generic API** and the MCP tools do this.

`Startup.cs` means `src/Ballware.Generic.Service/Startup.cs`.

## Authentication

```text
Authorization: Bearer <access token>
```

| Setting | Source | Citation |
| --- | --- | --- |
| Scheme | JWT bearer (`JwtBearerDefaults.AuthenticationScheme`) | `Startup.cs:175-179` |
| `Authority`, `Audience`, `RequireHttpsMetadata` | `Authorization` options (`Authority` and `Audience` are `[Required]`) | `Startup.cs:182-184`; `src/Ballware.Generic.Service/Configuration/AuthorizationOptions.cs:8-12,23` |
| `ValidIssuer` | set to the same value as `Authority` | `Startup.cs:185-188` |
| Claim mapping | **disabled** (`MapInboundClaims = false`) | `Startup.cs:181` |

Because inbound claim mapping is off, claim types are read **exactly as issued** — no
`http://schemas.xmlsoap.org/ws/2005/05/identity/claims/...` rewriting. `app.UseAuthorization()` is applied in
the pipeline (`Startup.cs:488`); authorization is enforced per endpoint group with `RequireAuthorization(...)`.

## Scope → policy table

The policies all read the **`scope` claim** (`ClaimTypeScope = "scope"`, `Startup.cs:55`), split its value on
spaces, and compare each token **ordinal-case-sensitively** against a configured scope value.

| Policy name | Compared against | Option (default value) | Used by |
| --- | --- | --- | --- |
| `genericApi` | `Authorization.RequiredMetaScope` (`"metaApi"`) | `AuthorizationOptions.cs:24` | `/generic` (`src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:34`) |
| `metaApi` | `Authorization.RequiredMetaScope` (`"metaApi"`) | `:24` | `/tenant/lookup` (user), `/tenant/processingstate`, `/tenant/statistic` |
| `serviceApi` | `Authorization.RequiredServiceScope` (`"serviceApi"`) | `:25` | `/tenant/lookup` (service), `/tenent/mlmodel`, `/tenant/tenant` |
| `schemaApi` | `Authorization.RequiredSchemaScope` (`"schemaApi"`) | `:26` | `/api/tenant` |
| (MCP) | `Mcp.RequiredMcpScope`, defaulting to `Authorization.RequiredMetaScope` | `Startup.cs:129-136` | `/generic/mcp` |

**`genericApi` and `metaApi` are byte-for-byte identical policies** (`Startup.cs:192-198` vs `199-205`): both
compare against `RequiredMetaScope`. Consequences:

- A Generic API caller needs the **`metaApi` scope value**, not a `genericApi` value.
- The `security` block of the `generic` document lists `openid` + `metaApi`, because
  `GroupScopeSecurityFilter` maps by **API group name** (`generic` → `RequiredMetaScope`,
  `Startup.cs:571-593`), which coincides with the policy semantics.
- `Swagger.RequiredScopes` (default `"openid genericApi"`, `src/Ballware.Generic.Service/Configuration/SwaggerOptions.cs:8`)
  is used **only** for the Swagger UI OAuth button; the UI requests the union of that value and the three
  policy scopes (`RequiredUiScopes`, `Startup.cs:537-565`). It is not a policy input.

A refused scope yields `401` with an **empty** body — the endpoint handler never runs.

## Claims this service reads

| Purpose | API | Default claim type | Config key |
| --- | --- | --- | --- |
| tenant | `IPrincipalUtils.GetUserTenandId(user)` (the misspelling is the external package's) | `tenant` | `Authorization:TenantClaim` (`AuthorizationOptions.cs:14-15`) |
| user | `IPrincipalUtils.GetUserId(user)` | `sub` | `Authorization:UserIdClaim` (`:17-18`) |
| rights | `IPrincipalUtils.GetUserRights(user)` | `right` | `Authorization:RightClaim` (`:20-21`) |
| all claims (for scripts + SQL) | `IPrincipalUtils.GetUserClaims(user)` | — | — |
| scope | read directly from the policy assertion | `scope` | hardcoded (`Startup.cs:55`) |
| tenants (MCP tool enumeration only) | `IPrincipalUtils.GetUserTenantIds(user)` | `tenant` | `src/Ballware.Generic.Mcp/Endpoints/GenericTools.cs:36` |

Wiring: `AddBallwareSharedAuthorizationUtils(tenantClaim, userIdClaim, rightClaim)` (`Startup.cs:332`).
Claim **extraction** and rights **evaluation** are implemented in the external packages
`Ballware.Shared.Authorization` and `Ballware.Shared.Authorization.Jint`
(`src/Ballware.Generic.Api/Ballware.Generic.Api.csproj:21`) — there is no `IPrincipalUtils`
implementation in this repository.

### Tenant from claim vs. tenant from path

| Routes | Tenant source |
| --- | --- |
| `/generic/...`, `/tenant/lookup/...` (user), `/tenant/processingstate/...`, `/tenant/statistic/...` | the **tenant claim** of the caller (`GenericDataEndpoint.cs:181`, `LookupDataEndpoint.cs:120`, `ProcessingStateDataEndpoint.cs:59`, `StatisticDataEndpoint.cs:34`) |
| `/tenant/lookup/columnvalue…`, `/tenent/mlmodel/...`, `/tenant/tenant/reportdatasourcesfortenant/...`, `/api/tenant/...` | the **`{tenantId}` path segment** — a service caller acts on an explicitly named tenant |
| `/generic/.../download/{tenantId}/{id}` | path segment, anonymous |

The MCP tool **enumeration** uses all of the user's tenants (`GetUserTenantIds`), but tool **execution**
resolves metadata from the single `GetUserTenandId` value — see [mcp-endpoint.md](mcp-endpoint.md).

## Rights: what this service builds

The checkers are called with a **candidate right name** plus the tenant/application/entity context:

```csharp
tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, <rightName>)
entityRightsChecker.HasRightAsync(tenantId, entityMetadata, claims, <rightName>, <contextPayload>, tenantAuthorized)
```

Whether `<rightName>` is granted is decided by the external authorization package (typically from `right`
claims following a `<application>.<entity>.<right>` pattern, and from `Tenant.RightsCheckScript` /
`Entity.ExtendedRightsCheckScript`). This repository **names** the right and **consumes** the boolean; it
does not define the grammar. The right name this service asks for:

| Route | Right name requested | Citation |
| --- | --- | --- |
| `all`, `query`, `count` | `view` | `GenericDataEndpoint.cs:194-195,224-225,254-255` |
| `byid` | `view`, evaluated against the **fetched row** | `:352-353` |
| `new`, `newquery` | `add` | `:284-285,315-316` |
| `save` | `edit` if `identifier == "primary"`, else the **identifier itself** | `:388-389` |
| `savebatch` | as `save`, for **every** item before any save | `:426-435` |
| `remove` | `delete`, against the row fetched with `primary` | `:464-467` |
| `import` | **none at the HTTP layer** — the job checks the `identifier` as right name per row | `:486-487` (unused), `src/Ballware.Generic.Jobs/Internal/GenericImportJob.cs:67-68` |
| `export`, `exporturl` | the **identifier itself** | `:558-559,600-601` |
| lookup / processingstate / statistic / service routes | none (scope policy only); the rights **collection** is instead passed to SQL as `@claims` | `LookupDataEndpoint.cs:121`, `CommonLookupProvider.cs:13,123` |
| MCP tools | `view` re-checked at execution | `GenericTools.cs:93-94,163-164` |

`contextPayload` matters: the entity-level check receives the query parameters (`query`, `count`,
`newquery`, `export`, `exporturl`), the item (`save`, `savebatch`), or the fetched row (`byid`, `remove`) —
so row-level and parameter-level authorization are possible from metadata alone. `all`/`new` pass an empty
dictionary (`:195,285`).

## Script-based enforcement

| Metadata member | Where it runs | Effect |
| --- | --- | --- |
| `Tenant.RightsCheckScript` | inside the external `Ballware.Shared.Authorization(.Jint)` checkers (`src/Ballware.Generic.Metadata/Tenant.cs:12`) | decides tenant-level rights |
| `Entity.ExtendedRightsCheckScript` | same, per entity (`src/Ballware.Generic.Metadata/Entity.cs:106`) | decides entity/row-level rights |
| `Entity.StateAllowedScript` | this service, `CommonGenericProvider.cs:418-422` | state transitions; **absent script means "not allowed"** (`src/Ballware.Generic.Scripting.Jint/Internal/JintEntityMetadataScriptingExecutor.cs:214`) |
| `Tenant.ServerScriptDefinitions` | prepended to every entity/statistic script (`JintEntityMetadataScriptingExecutor.cs:39-40`) | shared helper library, **not** a platform built-in |

Scripts available to rights checks are provided by the external package and are **not** inspectable from this
repository — do not assume the host-function set of the entity scripts applies there.

Illustrative pattern only (synthetic, not taken from any tenant): a rights script that allows an edit only
for records owned by the caller, and a state script that gates a transition on a right plus a column value.

```javascript
// entity rights pattern: keep the row out of reach unless a claim matches the row owner
const owner = getClaim("sub");
return item.ownerId === owner || hasAnyRight("admin");
```

```javascript
// state-allowed pattern: right plus a scalar read from the row
if (!hasRight("workflow.advance")) return false;
const status = getValue("statecolumn");
return state === 2 && status === 1;
```

`hasRight` / `hasAnyRight` inside `StateAllowedScript` are compared **case-insensitively** after
`ToLowerInvariant()` (`JintEntityMetadataScriptingExecutor.cs:205-206`).

## Statuses to expect

| Status | Meaning | Body |
| --- | --- | --- |
| `401` | scope policy refused **or** rights refused | empty |
| `403` | only `isstateallowedforentity/{entity}/{state}/{id}` when the state is not allowed | empty (`ProcessingStateDataEndpoint.cs:170`) |
| `404` | tenant/entity/lookup/statistic metadata missing, or `byid` row missing | JSON string (see [../api/meta-api.md](../api/meta-api.md) and [../api/generic-api.md](../api/generic-api.md)) |
| `400` | empty save body, empty ML query, refused remove precondition | JSON string or serialized `Exception` |
| `500` | unhandled exception | `application/problem+json` (`Startup.cs:458-483`) |

Ordering traps worth knowing:

- Metadata resolution comes **before** the rights check on most Generic routes → a missing entity is `404`
  even for an unauthorized caller (`GenericDataEndpoint.cs:188-191`).
- `byid` fetches first → a missing row is `404 "Item not found"` (`:347-350`) and only an existing row can
  produce a rights-based `401`.
- `remove` checks rights against the row fetched with `primary`; if the metadata's `ByIdQuery` does not
  return the row, the right check sees `null` as its context payload (`:464-467`).
- Import authorization is **asynchronous** — a `201` says nothing about whether any row was actually
  imported ([data-integration-and-jobs.md](data-integration-and-jobs.md)).

## Claims reaching SQL

Rights and claims are also **data** here: `@claim_<claimtype>` parameters are bound on the generic data
paths (prefix `claim_`, `src/Ballware.Generic.Tenant.Data.Commons/Provider/CommonGenericProvider.cs:30`),
while the deprecated `@claims` (comma-joined rights) is bound on the lookup paths
(`CommonLookupProvider.cs:13`). There is **no** `@userId`; the user id is `@claim_sub`. Details:
[metadata-driven-execution.md](metadata-driven-execution.md#claims-as-parameters).

## Related pages

- [architecture.md](architecture.md) — where the gates sit in the pipeline
- [metadata-driven-execution.md](metadata-driven-execution.md) — `StateAllowedScript`, claim bindings
- [mcp-endpoint.md](mcp-endpoint.md) — MCP scope and the `view` re-check
- [../api/README.md](../api/README.md) — token acquisition per caller type
