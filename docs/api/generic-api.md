# Generic API (document `generic`)

13 operations, one per data capability. Snapshot:
[../openapi/generic.json](../openapi/generic.json) (OpenAPI `3.0.4`, 13 paths, 3 component schemas:
`CountResult`, `ExportUrlResult`, `StringStringValuesKeyValuePair`).

Source: `src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs` (routes `:37-170`, handlers
`:176-666`), mounted at `/generic` (`src/Ballware.Generic.Service/Startup.cs:496`). Authorization policy
`genericApi`, which is **identical** to `metaApi` — see
[../concepts/tenancy-and-authorization.md](../concepts/tenancy-and-authorization.md).

Common shape: `/generic/{application}/{entity}/<verb>?identifier=<queryIdentifier>`.

- `{application}` and `{entity}` are path segments. `{entity}` selects the **entity metadata**;
  `{application}` is **not** used for metadata resolution — it is only passed to the rights checkers
  (e.g. `GenericDataEndpoint.cs:194`), i.e. it selects the right scope.
- The **tenant is taken from the user's tenant claim**, not from the URL
  (`principalUtils.GetUserTenandId(user)`, e.g. `:181` — the misspelled method name is the external
  package's).
- `identifier` is a **required query parameter** on all 11 identifier-taking routes; `remove/{id}` and
  `download/{tenantId}/{id}` have none. Semantics and the fallback behavior:
  [../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md#identifier-resolution).
- Free query parameters are **not declared** in the snapshot (they are bound through
  `QueryValueBag`, `src/Ballware.Generic.Api/Endpoints/QueryValueBag.cs:10-18`).

## Table of the 13 operations

| Verb + path | `identifier` | Right checked | Declared responses | Success body |
| --- | --- | --- | --- | --- |
| `GET .../all` | required | `view` | `200`, `401`, `404` | array of rows |
| `GET .../query` | required | `view` | `200`, `401`, `404` | array of rows |
| `GET .../count` | required | `view` | `200`, `401`, `404` | `{"count": n}` |
| `GET .../new` | required | `add` | `200`, `401`, `404` | single row |
| `GET .../newquery` | required | `add` | `200`, `401`, `404` | single row |
| `GET .../byid?id=<guid>` | required | `view` (against the fetched row) | `200`, `401`, `404` | single row |
| `POST .../save` | required | `edit` or `<identifier>` | `200`, `401`, `404` | **empty** |
| `POST .../savebatch` | required | `edit` or `<identifier>` (all items first) | `200`, `401`, `404` | **empty** |
| `DELETE .../remove/{id}` | — | `delete` (against the fetched row) | `200`, `401`, `404` | **empty** |
| `POST .../import` | required | **none** at the HTTP layer | `201`, `401`, `404` | **empty** |
| `GET .../export` | required | `<identifier>` | `200`, `401`, `404` | raw export body |
| `POST .../exporturl` | required | `<identifier>` | `200`, `401`, `404` | `ExportUrlResult` |
| `GET .../download/{tenantId}/{id}` | — | none (`AllowAnonymous`) | `200`, `404` | file stream |

## The item shape

Rows are Dapper dynamics, not DTOs. `Produces<IEnumerable<object>>` / `Produces<object>` therefore
produce an **untyped array/object** in OpenAPI — the actual keys come from the metadata SQL aliases and
nothing renames them (`PropertyNamingPolicy = null`, `src/Ballware.Generic.Service/Startup.cs:234-242`).

Consequences to design for:

- Key names **and casing** are the SELECT aliases: `select Uuid as Id, Coltextline, Colnumber` yields
  `Id`/`Coltextline`/`Colnumber` on SQL Server, and `select uuid as id, coltextline, colnumber` yields
  `id`/`coltextline`/`colnumber` on PostgreSQL
  (`test/Ballware.Generic.Tenant.Data.SqlServer.Tests/Generic/SqlServerGenericProviderTest.cs:183,190,207-208`
  vs `test/Ballware.Generic.Tenant.Data.Postgres.Tests/Generic/PostgresGenericProviderTest.cs:178,185,202-203`).
- `Guid`, dates, numbers and `null` arrive as whatever the provider/Dapper mapping produces; the service
  adds no normalization.
- Scripts may add columns: `ListScript`/`ByIdScript` can call `addProperty(name, value)`
  (`src/Ballware.Generic.Scripting.Jint/Internal/JintEntityMetadataScriptingExecutor.cs:34-38,56-60`),
  so the column set is metadata-defined, not fixed.
- Example (synthetic):

```json
[
  { "Id": "<guid>", "Coltextline": "sample text", "Colnumber": 7 }
]
```

## `GET .../all`

- Handler `HandleAllAsync` (`:176-203`). No parameters besides the paths and `identifier`.
- Metadata → `404` with body `"Unknown tenant or entity"` (`:188-191`, constant at `:28`).
- Rights `view` (`:194-195`), refused → `401` **empty** body (`:197-200`).
- Calls `AllAsync` → `Entity.ListQuery` with `identifier`, silent fallback to `primary`, empty array when
  nothing exists (`src/Ballware.Generic.Tenant.Data.Commons/Provider/CommonGenericProvider.cs:170-180`).

## `GET .../query`

- Handler `:206-233`. Extra free query parameters are collected by `GetQueryParams` (`:649-666`) and
  **repeated keys are joined into `|a|b|c|`** (`:655-657`) — a metadata query written for multi-values
  must match this idiom.
- Rights `view` are evaluated against the **query parameters** (`:225`), so a tenant script can gate the
  query itself, not only its rows.
- Unknown `identifier` → the `primary` list query, then `[]`.

## `GET .../count`

- Handler `:236-263`. Response is the only pinned lower-case key in this API:
  `{"count": 42}` (`CountResult` with `[JsonPropertyName("count")]`, `:19-23`).
- The count statement is looked up in the same `ListQuery` family (`CommonGenericProvider.cs:197-202`);
  nothing found → `{"count": 0}` (`:204-207`).

## `GET .../new` and `GET .../newquery`

- Handlers `:266-293` / `:296-324`. Right `add`. `newquery` adds free parameters.
- Resolve `Entity.NewQuery` (`:260-270`); missing → `null`, i.e. a `200` with an empty body.
- A `NewQuery` is the metadata's template query — it is how default values arrive.

## `GET .../byid?id=<guid>`

- Handler `:327-361`. `id` is a **query parameter** (`Guid`), not a path segment.
- Order is deliberate: the row is fetched **first** (`:345`), then
  `404 "Item not found"` (`:347-350`) when the row does not exist, and only then the `view` right is
  evaluated **against the fetched row** (`:352-353`) so per-row scripts can decide. A row the user may
  not see is therefore reported as `404` **or** `401` depending on whether the row exists.
- `identifier` selects the `ByIdQuery` (`:225-235`), with `primary` fallback.

## `POST .../save`

- Handler `:364-399`. Body is the **bare item object** (`BodyValueBag`,
  `src/Ballware.Generic.Api/Endpoints/BodyValueBag.cs:11-54`), i.e. `{ "Id": "<guid>", ... }` — there is
  no wrapper property and the OpenAPI document declares **no request body** at all.
- Missing/unparseable body → `400 "No value provided"` (`:383-386`).
- Right: `edit` when `identifier == "primary"`, otherwise a right **named after the identifier**
  (`:388-389`) — this is the state-change and workflow-save idiom
  (`../best-practices/metadata-driven-payloads.md`).
- Response `200` with an **empty body** (`:398`): no id, no timestamp, no saved row. Callers must re-read
  with `byid`/`query`.
- Insert-vs-update rules, `original_<column>` injection, and complex-member stripping:
  [../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md#save-path).

## `POST .../savebatch`

- Handler `:402-443`. Body is a **bare JSON array** of item objects (`BodyValuesBag`,
  `src/Ballware.Generic.Api/Endpoints/BodyValuesBag.cs:25-27`); missing body → `400 "No values provided"`
  (`:421-424`).
- **All-rights-first**: rights are evaluated for every item before any save happens
  (`:426-435`), then the saves run sequentially (`:437-440`). Each `save` runs in its own transaction, so
  an early success is **not** rolled back when a later item fails.

## `DELETE .../remove/{id}`

- Handler `:446-482`. `{id}` is a path segment. There is **no `identifier`**: the row is loaded with
  `primary` (`:464`), then the `delete` right is checked against that row (`:466-467`).
- Refused → `401` empty; otherwise the metadata `RemoveStatement` runs (`:474`).
- Refused precondition → **`400`** whose body is a serialized exception whose message is the script
  messages joined by `\r\n` (`:476-479`):

```json
{
  "ClassName": "System.Exception",
  "Message": "state cannot be changed\nsecond reason",
  // remaining System.Exception serialization fields
}
```

  (Exact members depend on the runtime's `Exception` serialization; treat it as an
  implementation detail and read `Message`.)
- Success → `200` with an **empty body** (`:481`). `400` is **not declared** in the snapshot.

## `POST .../import`

- Handler `:485-527`, `multipart/form-data` with the field name **`files`**, and multiple files are
  allowed — each file produces **one job** (`:496-524`). Declared request body:
  `files: array of string(format: binary)`.
- `201 Created` with **empty body** (`:526`) — no job id is returned. Track progress through the meta
  service job record (`identifier` = `"import"`, `scheduler` = `"generic"`).
- **No rights check at the HTTP layer** even though both rights checkers are injected
  (`:486-487`): enforcement happens per row inside the Quartz job
  (`src/Ballware.Generic.Jobs/Internal/GenericImportJob.cs:65-71`). See
  [../concepts/data-integration-and-jobs.md](../concepts/data-integration-and-jobs.md).
- The format (csv/json), the delimiter, and the expected column names are defined by the entity's
  `CustomFunctions` entry with `Type = import` and `Id == identifier`
  (`CommonGenericProvider.cs:433-467`); CSV **headers must match the save statement's `@param` names**.

## `GET .../export`

- Handler `:530-569`. Free parameters come straight from `request.Query` **without** pipe-joining
  (`:543-548`), so a repeated key is a **list** and Dapper expands it (`Uuid in @id` /
  `uuid = ANY(@id::uuid[])`).
- Right name is the `identifier` (`:558-559`).
- The identifier must exist **both** in `ListQuery` and in `CustomFunctions(Type = export)`
  (`CommonGenericProvider.cs:514-526`), otherwise an `ArgumentException` surfaces as `500`
  `application/problem+json` — not as `404`.
- Response is the raw export body with the media type from `MimeTypeMap`
  (`GenericDataEndpoint.cs:568`). `Produces<string>` in the snapshot (`:143`) reflects neither the JSON
  nor the CSV media type.

## `POST .../exporturl`

- Handler `:572-628`. Parameters are read from the **form body** (`request.ReadFormAsync()`, `:580`),
  not the query string (`:585-590`); the snapshot declares the request body as
  `application/x-www-form-urlencoded` (`Accepts<IFormCollection>`, `:154`).
- Creates an export record in the meta service with `Application`, `Entity`, `Query = identifier`,
  `MediaType`, and `ExpirationStamp = DateTime.Now.AddDays(1)` (`:610-617`), uploads the bytes to the
  storage service under the **export id** (`:621`), and returns
  [`ExportUrlResult`](schemas.md#exporturlresult) (`:623-627`):

```json
{ "TenantId": "<guid>", "Id": "<guid>" }
```

## `GET .../download/{tenantId}/{id}`

- Handler `:630-647`. `.AllowAnonymous()` (`:164`) — **no token, no scope, no right check**. Possession of
  the tenant id + export id is the only gate, plus the 1-day expiration stored in the meta service
  (`:639-642`).
- Unknown or expired export → `404` with an empty body.
- File name is `<Query>_<yyyyMMdd_HHmmss><ext>` where the timestamp is the **download** time (`:646`);
  e.g. `exportjson_20260101_120000.json`.
- `{application}` and `{entity}` are part of the route but **unused** in the handler (`:632-633`).

## Known spec inaccuracies

Documented rather than hidden:

| Deviation | Snapshot says | Behavior is |
| --- | --- | --- |
| `download` content type | `contentType: "application/json"` (`GenericDataEndpoint.cs:165`) | the export's own `MediaType`, e.g. `text/csv` (`:646`) |
| `export` body | `Produces<string>` (`:143`) | `text/csv`, `application/json`, … per `MimeTypeMap` (`CommonGenericProvider.cs:535`) |
| `save` / `savebatch` bodies | **no** `requestBody` declared | a bare item object / bare JSON array is required; otherwise `400` |
| `400` responses | not declared (`remove` refusal, empty save body) | produced by `:476-479`, `:383-386`, `:421-424` |
| `byid` "not found" | `404` declared generically | body is the JSON string `"Item not found"` (`:349`) |
| Metadata not found | `404` | body is the JSON string `"Unknown tenant or entity"` (`:28,190`) |
| Rights refused | `401` declared | `401` with an **empty** body (`:197-200`) |
| Free query parameters | not declared | accepted, bound by `QueryValueBag`; unknown names are simply unused by the statement |
| Untyped row payloads | `array` with no `items` schema | metadata-defined columns (see [The item shape](#the-item-shape)) |

## Related pages

- [README.md](README.md) — document-level conventions and the `identifier` table
- [meta-api.md](meta-api.md), [service-api.md](service-api.md), [schema-api.md](schema-api.md)
- [schemas.md](schemas.md) — the three component schemas of this document
- [examples.md](examples.md) — curl sequences
- [../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md)
- [../concepts/data-integration-and-jobs.md](../concepts/data-integration-and-jobs.md)
