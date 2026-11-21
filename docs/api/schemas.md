# Component schemas

Field tables for **every component schema declared in the four committed snapshots**, followed by a
separate section for the enum-valued metadata models that the snapshots do **not** expose.

"Nullable" below mirrors the snapshot (`nullable: true`). All snapshot objects have
`additionalProperties: false`.

## `generic` document — 3 schemas

### CountResult

`docs/openapi/generic.json#/components/schemas/CountResult`
Source: `src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:19-23` (declared inline in the endpoint
file, not under `Public/`).

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `count` | integer (`int64`) | no | lower-case key forced by `[JsonPropertyName("count")]` (`:21`) — the only non-PascalCase body in the API |

### ExportUrlResult

`docs/openapi/generic.json#/components/schemas/ExportUrlResult`
Source: `src/Ballware.Generic.Api/Public/ExportUrlResult.cs:3-7`.

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `TenantId` | string (`uuid`) | no | the tenant from the calling user's tenant claim (`GenericDataEndpoint.cs:582,625`) |
| `Id` | string (`uuid`) | no | the export record id created in the meta service (`:619`); required by `download/{tenantId}/{id}` |

### StringStringValuesKeyValuePair

`docs/openapi/generic.json#/components/schemas/StringStringValuesKeyValuePair`
Source: generated from the `IFormCollection` declared by `Accepts<IFormCollection>("application/x-www-form-urlencoded")`
(`GenericDataEndpoint.cs:154`) — there is no C# type with this name; it is Swashbuckle's rendering of the
form collection (`Microsoft.Extensions.Primitives.StringValues`, .NET 10).

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `Key` | string | yes | the form field name |
| `Value` | array of string | no | all values submitted under that name; `null` when the key is absent |

Practical meaning: `POST …/exporturl` takes **arbitrary form fields**, each becoming a statement
parameter (`GenericDataEndpoint.cs:585-590`). A client generator sees an array of these pairs; the real
payload is `field1=value1&field2=value2`.

## `meta` document — 1 schema

### ProcessingStateSelectListEntry

`docs/openapi/meta.json#/components/schemas/ProcessingStateSelectListEntry`
Source: `src/Ballware.Generic.Metadata/ProcessingStateSelectListEntry.cs`.

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `Id` | string (`uuid`) | no | id of the state record in the meta service; also the identity used when intersecting successor lists (`ProcessingStateDataEndpoint.cs:11-15`) |
| `Name` | string | yes | display name from the meta service |
| `State` | integer (`int32`) | no | the numeric state value — this is what `{state}` in `isstateallowedforentity/{entity}/{state}/{id}` compares against |

## `service` document — 3 schemas

### ReportDatasourceDefinition

`docs/openapi/service.json#/components/schemas/ReportDatasourceDefinition`
Source: `src/Ballware.Generic.Metadata/ReportDatasource.cs:21-27`.

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `Provider` | string | yes | always overwritten with the tenant's provider (`mssql` \| `postgres`, `TenantDataEndpoint.cs:77`) |
| `Name` | string | yes | datasource name; the synthetic one is always `"Lookups"` (`:55`) |
| `ConnectionString` | string | yes | **the tenant database connection string, embedded in the response** (`:49,57,78`) |
| `Tables` | array of `ReportDatasourceTable` | yes | tables with an empty `Entity` are filtered out for metadata datasources (`:80,94`) |

### ReportDatasourceTable

`docs/openapi/service.json#/components/schemas/ReportDatasourceTable`
Source: `src/Ballware.Generic.Metadata/ReportDatasource.cs:3-11`.

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `Name` | string | no | lookup identifier or configured table name |
| `Entity` | string | yes | entity identifier whose `ListQuery` supplies the query; presence is the filter criterion |
| `Query` | string | yes | resolved SQL text, with `[ballwareschema]` replaced and the tenant variable substituted **textually** (`:62,88-90`) |
| `Relations` | array of `ReportDatasourceRelation` | yes | default empty (`ReportDatasource.cs:10`) |

### ReportDatasourceRelation

`docs/openapi/service.json#/components/schemas/ReportDatasourceRelation`
Source: `src/Ballware.Generic.Metadata/ReportDatasource.cs:13-19`.

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `Name` | string | no | relation name |
| `ChildTable` | string | no | related table |
| `MasterColumn` | string | no | column on the master table |
| `ChildColumn` | string | no | column on the child table |

## `schema` document — 2 schemas

### EntitySchema

`docs/openapi/schema.json#/components/schemas/EntitySchema`
Source: `src/Ballware.Generic.Api/Public/EntitySchema.cs:3-7`.

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `UserId` | string (`uuid`) | no | auditing user forwarded to the master-store write (`TenantSchemaEndpoint.cs:61`); **not** the calling user |
| `SerializedEntityModel` | string | yes | JSON **string** holding the provider table model (camelCase), see [schema-api.md](schema-api.md) |

### TenantSchema

`docs/openapi/schema.json#/components/schemas/TenantSchema`
Source: `src/Ballware.Generic.Api/Public/TenantSchema.cs:3-8`.

| Field | Type | Nullable | Notes |
| --- | --- | --- | --- |
| `UserId` | string (`uuid`) | no | auditing user (`TenantSchemaEndpoint.cs:75`) |
| `Provider` | string | no | `"mssql"` or `"postgres"`; selects the provider whose schema implementation runs |
| `SerializedTenantModel` | string | no | JSON **string** holding the provider tenant model (`server`, `catalog`, `schema`, `databaseObjects[]`), see [schema-api.md](schema-api.md) |

## Payloads that are **not** component schemas

Everything else in the API is either untyped or internal, and no field table can be derived from the
snapshots:

| Payload | Why it is not a schema |
| --- | --- |
| rows from `all`/`query`/`byid`/`new`/`newquery`/`export`/lookup/statistic/ML training data | Dapper dynamics declared as `Produces<IEnumerable<object>>` / `Produces<object>`; keys come from metadata SQL aliases |
| `save` / `savebatch` request bodies | bound by `BodyValueBag` / `BodyValuesBag` (`src/Ballware.Generic.Api/Endpoints/BodyValueBag.cs`, `BodyValuesBag.cs`); no `requestBody` is declared |
| free query parameters | bound by `QueryValueBag` (`src/Ballware.Generic.Api/Endpoints/QueryValueBag.cs:10-18`) via `BindAsync`, invisible to OpenAPI |
| `import` multipart field | declared inline (`files: array of binary`), no named schema |
| `404` / `400` bodies | JSON strings or serialized `Exception` produced by `Results.NotFound(...)` / `Results.BadRequest(...)` |
| `JobCreatePayload`, `JobUpdatePayload`, `ExportCreatePayload`, `Export`, `Tenant`, `Lookup`, `Statistic`, `Entity` | metadata models in `src/Ballware.Generic.Metadata/` sent to or received from the **external** meta service; never part of this service's HTTP contracts |

## Enum wire values referenced by the concepts pages

These appear in metadata and in the job/export payloads sent **to the meta service**, so they are listed
here for completeness; none of them is a snapshot component schema.

### `CustomFunctionTypes`

`src/Ballware.Generic.Metadata/Entity.cs:37-62` — serialized values
(`[EnumMember]`) are: `add`, `edit`, `default_add`, `default_view`, `default_edit`, `external`, `export`,
`import`. Only `import` and `export` are consulted by this service
(`CommonGenericProvider.cs:433,521`). `CustomFunctionEntry` is `{ Id, Type, Options? }` (`Entity.cs:71-76`)
with `CustomFunctionOptions { Format?, Delimiter? }` (`:64-69`).

### `JobStates`

`src/Ballware.Generic.Metadata/Job.cs:21-37` — serialized values `Unknown`, `Queued`, `InProgress`,
`Finished`, `Error`. This service writes `InProgress`, `Finished` and `Error`
(`src/Ballware.Generic.Jobs/Internal/GenericImportJob.cs:43,75,81`); `Queued` is assigned by the meta
service when the job record is created.

### Tenant storage provider names

`"mssql"` (`src/Ballware.Generic.Tenant.Data.SqlServer/TenantStorageBuilderExtensions.cs:11`) and
`"postgres"` (`src/Ballware.Generic.Tenant.Data.Postgres/TenantStorageBuilderExtensions.cs:11`); the master
store validates `TenantStorage.Provider` against exactly these two
(`src/Ballware.Generic.Service/Startup.cs:122-127`).

## Related pages

- [README.md](README.md) · [generic-api.md](generic-api.md) · [meta-api.md](meta-api.md) ·
  [service-api.md](service-api.md) · [schema-api.md](schema-api.md)
- [../openapi/manifest.json](../openapi/manifest.json) — per-document schema counts
