# API surface and conventions

Four OpenAPI documents describe the HTTP surface of the Generic Service. They are generated from the
same running configuration the service uses and committed in [../openapi/](../openapi/).

| Document | Title | Paths | Schemas | Required scope (per operation) | Documented in |
| --- | --- | --- | --- | --- | --- |
| `generic` | ballware Generic API | 13 | 3 | `metaApi` | [generic-api.md](generic-api.md) |
| `meta` | ballware Generic Meta API | 12 | 1 | `metaApi` | [meta-api.md](meta-api.md) |
| `service` | ballware Service API | 3 | 3 | `serviceApi` | [service-api.md](service-api.md) |
| `schema` | ballware Schema API | 4 | 2 | `schemaApi` | [schema-api.md](schema-api.md) |

Document registration: `src/Ballware.Generic.Service/Startup.cs:413-435`; SwaggerUI endpoints
`src/Ballware.Generic.Service/Startup.cs:523-526`; per-operation security requirements
`src/Ballware.Generic.Service/Startup.cs:445` (`GroupScopeSecurityFilter`,
`src/Ballware.Generic.Service/Startup.cs:571-593`). Raw JSON while the service runs:
`http://localhost:5002/swagger/<document>/swagger.json`.

The scope values are the defaults of `AuthorizationOptions`
(`src/Ballware.Generic.Service/Configuration/AuthorizationOptions.cs:24-26`) and are configurable.
Note that the `generic` document's operations are authorized by the **`metaApi` scope value** even
though their authorization policy is named `genericApi`: the two policies are identical
(`src/Ballware.Generic.Service/Startup.cs:192-205`). See
[../concepts/tenancy-and-authorization.md](../concepts/tenancy-and-authorization.md).

## Groups, tags, operationIds

Each endpoint declares an API explorer group (`WithGroupName`) that selects the document, a tag
(`WithTags`) and a name (`WithName`), which becomes the OpenAPI `operationId`:

| Mount | Base path | Group → document | Tag | `WithName` prefix | Registered at |
| --- | --- | --- | --- | --- | --- |
| `MapGenericDataApi` | `/generic` | `generic` | `Generic` | `Generic` | `src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:30-35`, mounted `Startup.cs:496` |
| `MapLookupUserDataApi` | `/tenant/lookup` | `meta` | `Lookup` | `Lookup` | `src/Ballware.Generic.Api/Endpoints/LookupDataEndpoint.cs:17-22`, mounted `Startup.cs:490` |
| `MapLookupServiceDataApi` | `/tenant/lookup` | `service` | `Lookup` | `Lookup` | `src/Ballware.Generic.Api/Endpoints/LookupDataEndpoint.cs:99-104`, mounted `Startup.cs:491` |
| `MapMlModelDataApi` | `/tenent/mlmodel` | `service` | `MlModel` | `MlModel` | `src/Ballware.Generic.Api/Endpoints/MlModelDataEndpoint.cs:12-18`, mounted `Startup.cs:492` |
| `MapProcessingStateDataApi` | `/tenant/processingstate` | `meta` | `ProcessingState` | `ProcessingState` | `src/Ballware.Generic.Api/Endpoints/ProcessingStateDataEndpoint.cs:19-24`, mounted `Startup.cs:493` |
| `MapStatisticDataApi` | `/tenant/statistic` | `meta` | `Statistic` | `Statistic` | `src/Ballware.Generic.Api/Endpoints/StatisticDataEndpoint.cs:12-18`, mounted `Startup.cs:494` |
| `MapTenantServiceDataApi` | `/tenant/tenant` | `service` | `Tenant` | `Tenant` | `src/Ballware.Generic.Api/Endpoints/TenantDataEndpoint.cs:10-19`, mounted `Startup.cs:495` |
| `MapTenantServiceSchemaApi` | `/api/tenant` | `schema` | `Tenant` | `Tenant` | `src/Ballware.Generic.Api/Endpoints/TenantSchemaEndpoint.cs:10-17`, mounted `Startup.cs:498` |

`operationId`s are therefore `<prefix><Name>` compounds, e.g. `GenericAll`,
`GenericSaveBatch`, `LookupSelectListForLookupId`, `LookupSelectColumnValueByIdForTenantAndLookupId`,
`ProcessingStateIsStateAllowedForEntityByIdentifierStateAndId`, `StatisticPayloadByIdentifier`,
`MlModelTrainingDataByTenantAndPlainQuery`, `TenantReportDatasources`,
`TenantCreateOrUpdateEntitySchemaForTenant`. These names are stable identifiers for generated
clients (see the `Ballware.Generic.Service.Client` / `Ballware.Generic.Schema.Client` projects).

## `identifier` semantics

`identifier` is the metadata **query identifier** (the name of a query defined on the entity, see
[../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md)). Two facts
matter for callers:

1. On every `/generic` route that takes an `identifier`, it is a **required query parameter** — it
   is not defaulted by the API. An internal `primary` default exists only inside the provider as a
   fallback for unknown identifiers and for the `remove`/`byid` lookups that resolve the row before
   the actual statement runs
   (`src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:27,464` and
   `src/Ballware.Generic.Tenant.Data.Commons/`).
2. On write and export routes the `identifier` **is the right name**: `save`/`savebatch` check the
   `edit` right when `identifier == "primary"` and otherwise check a right named exactly like the
   identifier; `export`/`exporturl` always check the right named after the identifier
   (`src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:388-389,428-429,558-559,600-601`).

| Route | `identifier` | Right checked at the HTTP layer |
| --- | --- | --- |
| `GET .../all` | required query param | `view` |
| `GET .../query` | required query param | `view` |
| `GET .../count` | required query param | `view` |
| `GET .../byid` | required query param | `view` (after the row is fetched) |
| `GET .../new` | required query param | `add` |
| `GET .../newquery` | required query param | `add` |
| `POST .../save` | required query param | `edit` if `identifier == primary`, else `<identifier>` |
| `POST .../savebatch` | required query param | `edit` if `identifier == primary`, else `<identifier>` (per item, all before any save) |
| `POST .../import` | required query param | **none** — rights are checked per row inside the import job |
| `GET .../export` | required query param | `<identifier>` |
| `POST .../exporturl` | required query param | `<identifier>` |
| `DELETE .../remove/{id}` | absent (always the `primary` lookup internally) | `delete` |
| `GET .../download/{tenantId}/{id}` | absent | none (`AllowAnonymous()`) |

The `meta`, `service` and `schema` documents do **not** use the entity query `identifier`
convention. Their path parameters (`{identifier}` in `/tenant/lookup/selectlistforlookupidentifier/{identifier}`
and `/tenant/statistic/dataforidentifier/{identifier}`) are the identifier of a *lookup* or
*statistic*, not of an entity query, and the `/api/tenant/dropentityschemafortenant/{tenantId}/{identifier}`
identifier is the entity table name.

## Payload conventions

- **PascalCase** for every POCO body and response: `PropertyNamingPolicy = null` for both MVC and
  minimal-API JSON options (`src/Ballware.Generic.Service/Startup.cs:234-242`). Example:
  `{"TenantId":"…","Id":"…"}` from `exporturl`.
- **Exception:** `CountResult` pins the key to lower-case `count` via `[JsonPropertyName("count")]`
  (`src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:19-23`), so `count` returns
  `{"count": 42}`.
- **Query rows are not POCOs.** `all`, `query`, `byid`, `new`, `newquery`, `export`, lookup and
  statistic responses are Dapper dynamic rows: the JSON keys are the **SELECT aliases** of the
  metadata query, with no case normalization. `select Uuid as Id` yields `Id` on SQL Server, and
  `uuid as id` yields `id` on PostgreSQL. Their OpenAPI schema is an untyped array (`type: array`
  with no `items` schema) because the shape is metadata-defined.
- Values arrive from HTTP as strings; the metadata SQL is responsible for casting
  (see [../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md)).
- Two different **multi-value** conventions are in effect:

  | Routes | Encoding of a repeated query key | Implementation |
  | --- | --- | --- |
  | `/query`, `/count`, `/newquery`, `/tenant/statistic/dataforidentifier/{identifier}` | joined into `\|a\|b\|c\|` | `GenericDataEndpoint.cs:649-666` (`GetQueryParams`), inline the same way in `StatisticDataEndpoint.cs:39-49` |
  | `/export`, `/exporturl` | raw `StringValues` list forwarded to Dapper, which expands it (`Uuid in @id` / `uuid = ANY(@id::uuid[])`) | `GenericDataEndpoint.cs:545-548,587-590` |
  | `/tenant/processingstate/selectlistallowedsuccessorsforentities/{entity}` | raw `StringValues`, iterated **in the handler** (one `id` per value) | `ProcessingStateDataEndpoint.cs:106-110` |

  So `?tag=a&tag=b` becomes the single string `|a|b|` for a query statement, but a two-element list
  for an export statement. A metadata query written for one convention is not portable to the other.
- **Anonymous download**: `GET /generic/{application}/{entity}/download/{tenantId}/{id}` is
  `AllowAnonymous()` (`src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:163-170`) and
  authorized by possession of the export id plus the 1-day expiration recorded in the meta service
  (`GenericDataEndpoint.cs:639`).

## Free-form query parameters

`/query`, `/count`, `/newquery`, `/export`, `/exporturl`, `/tenant/statistic/dataforidentifier/{identifier}`
and `/tenant/processingstate/selectlistallowedsuccessorsforentities/{entity}` accept **arbitrary**
query parameters that are passed to the metadata statement as `@param` bindings
(`src/Ballware.Generic.Api/Endpoints/QueryValueBag.cs:10-18`). Because they are bound through
`BindAsync`, they are **not declared** in the OpenAPI documents — only the path parameters and
`identifier` are. The valid names are whatever the metadata query defines.

## Token acquisition

| Caller | Flow | Scope to request |
| --- | --- | --- |
| Browser app / Swagger UI | authorization code + PKCE (`c.OAuthUsePkce()`, `Startup.cs:531`) against `Authorization.Authority` | `openid` plus `metaApi` for the `generic`/`meta` documents |
| Reporting / ML / other platform service | client credentials against its own token endpoint | `serviceApi` for the `service` document |
| Schema provisioning tooling | client credentials | `schemaApi` for the `schema` document |
| AI agent (MCP) | user JWT | the scope in `Mcp.RequiredMcpScope` (defaults to `metaApi`), see [../concepts/mcp-endpoint.md](../concepts/mcp-endpoint.md) |

The Swagger UI's OAuth button requests the union of `Swagger.RequiredScopes` and the three
configured policy scopes so that all four documents can be exercised from one token
(`Startup.cs:530`, `RequiredUiScopes` at `Startup.cs:537-565`).

## Error contract (all four documents)

| Status | When | Body |
| --- | --- | --- |
| `401` | scope policy refused, or tenant/entity right check refused | empty |
| `403` | only `isstateallowedforentity` when the state is not allowed | empty (`ProcessingStateDataEndpoint.cs:170`) |
| `404` | metadata resolution failed, or a not-found message | JSON string, e.g. `"Unknown tenant or entity"` |
| `400` | malformed save body, empty ML query, refused remove precondition | JSON string or serialized exception |
| `500` | unhandled exception | `application/problem+json` (`Startup.cs:458-483`) |

The `400` and `500` cases are **not declared** in the snapshots; the declared sets are `200/201`,
`401`, `404` and (for `isstateallowed`) `403`. See
[generic-api.md](generic-api.md#known-spec-inaccuracies) for the full list of spec gaps.

## Pages

- [generic-api.md](generic-api.md) — 13 Generic API operations
- [meta-api.md](meta-api.md) — 12 operations of the `meta` document
- [service-api.md](service-api.md) — 3 Service API operations
- [schema-api.md](schema-api.md) — 4 Schema API operations
- [schemas.md](schemas.md) — component schema field tables for all four snapshots
- [examples.md](examples.md) — copy-pasteable curl sequences
