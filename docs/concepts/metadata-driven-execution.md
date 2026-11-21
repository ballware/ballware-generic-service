# Metadata-driven execution

This page explains **what decides the payloads**. The service has no entity-specific code: every HTTP
request is translated into metadata lookups plus provider calls, and the metadata — authored and served
by the meta service — chooses the SQL, the parameters, the scripts and therefore the JSON shape.

Metadata is *not* modeled or stored in this repository. The provider-neutral model lives in
`src/Ballware.Generic.Metadata/` and is filled from the external `ballware.meta.service.client`
(`src/Ballware.Generic.Service/Ballware.Generic.Service.csproj:12`) through
`src/Ballware.Generic.Service/Adapter/MetaServiceMetadataAdapter.cs` and the Mapster profile
`src/Ballware.Generic.Service/Mappings/MetaServiceGenericMetadataProfile.cs`. The authoring model
(entities, queries, lookups, statistics, states, pages) is documented in `ballware-meta-service/docs`
and is not duplicated here.

All citations below are relative to the repository root.
`CommonGenericProvider.cs` means `src/Ballware.Generic.Tenant.Data.Commons/Provider/CommonGenericProvider.cs`.

## The metadata members that shape a payload

| Metadata member | Type on `Entity` | Used by | Shapes |
| --- | --- | --- | --- |
| `ListQuery` | collection of `QueryEntry` | `all`, `query`, `count`, `export` | row keys = SELECT aliases (`src/Ballware.Generic.Metadata/Entity.cs:80-84`) |
| `ByIdQuery` | collection of `QueryEntry` | `byid`, the save existence probe, `export` (json) | single row / row enrichment |
| `NewQuery` | collection of `QueryEntry` | `new`, `newquery` | pre-fill row |
| `SaveStatement` | collection of `QueryEntry` (SQL text) | `save`, `savebatch`, `import` | the DML statement |
| `ScalarValueQuery` | **a string identifier** | `isstateallowed` via `getValue` | resolved *inside* `ByIdQuery` (`CommonGenericProvider.cs:388`) |
| `RemoveStatement` | **a single SQL string**, no identifier | `remove` | `string?`, `Entity.cs:89` |
| `CustomFunctions` | collection of `CustomFunctionEntry` (`Type = import\|export`, `Options.Format`, `Options.Delimiter`) | `import`, `export` | format + file name + media type |
| `ListScript`, `ByIdScript`, `BeforeSaveScript`, `SaveScript`, `RemovePreliminaryCheckScript`, `RemoveScript`, `StateAllowedScript` | optional script strings | read/save/remove/state paths | see [Scripts](#scripts) |
| `QueryEntry.Identifier`, `.Query`, `.AiEnabled`, `.Description`, `.Parameters`, `.ResultColumns` | `Entity.cs:24-35` | identifiers; MCP tool schemas | `AiEnabled`/`Parameters`/`ResultColumns` are **only** used by MCP tool generation (`src/Ballware.Generic.Mcp/Endpoints/GenericTools.cs:46,52,58-65,111`) — the data providers ignore them |

## Identifier resolution

`identifier` (a required query parameter on the `/generic` routes — see
[../api/README.md](../api/README.md#identifier-semantics)) selects one metadata entry. The default
`primary` is a **provider-internal fallback**, not an API default:
`DefaultQueryIdentifier = "primary"` (`CommonGenericProvider.cs:28`).

Matching is **case-sensitive ordinal** for query entries (`q.Identifier.Equals(identifier)`,
`CommonGenericProvider.cs:170,197,225,260,291,514`) and exact for custom functions
(`f.Id == identifier`, `:434,521`).

| Operation | Metadata member | Fallback when the identifier is unknown |
| --- | --- | --- |
| `all`, `query` | `ListQuery` (`:170-175`) | **silent** → `primary`; if that is missing too, an **empty array** (`:177-180`) |
| `count` | `ListQuery` (`:197-202`) | **silent** → `primary`; else `0` (`:204-207`) |
| `byid` | `ByIdQuery` (`:225-230`) | **silent** → `primary`; else `null` (`:232-235`) |
| `new`, `newquery` | `NewQuery` (`:260-265`) | **silent** → `primary`; else `null` (`:267-270`) |
| `save`, `savebatch` | `SaveStatement` (`:291-296`) | **silent** → `primary`; if `primary` is missing too → `ArgumentException "Unsupported function identifier <id>"` (`:298-301`) |
| `import` | `CustomFunctions(Type = import)` (`:433-434`) | **throws** `ArgumentException "Unsupported function identifier <id>"` (`:436-439`) |
| `export` | `ListQuery` **and** `CustomFunctions(Type = export)` (`:514-526`) | **no fallback at all** — `ArgumentException "Unsupported query identifier <id>"` (`:516-519`) or `"Unsupported function identifier <id>"` (`:523-526`) |
| `remove` | `RemoveStatement` | not applicable: `ITenantGenericProvider.RemoveAsync` has **no identifier parameter** (`src/Ballware.Generic.Tenant.Data/ITenantGenericProvider.cs:28-29`) |
| `isstateallowed` | `StateAllowedScript` + `ScalarValueQuery` | absent script → `false` (`src/Ballware.Generic.Scripting.Jint/Internal/JintEntityMetadataScriptingExecutor.cs:214`) |

Two consequences callers hit in practice:

- A typo in a read identifier usually produces an **empty result or an empty body**, not an error —
  because it silently falls back to `primary`.
- `import` and `export` identifiers must be **declared**: an import function must exist for `import`,
  and an export must exist under the *same* identifier in both `ListQuery` and `CustomFunctions`.
  An undeclared export identifier surfaces as an unhandled `ArgumentException` → `500`
  `application/problem+json` (`src/Ballware.Generic.Service/Startup.cs:458-483`).

The import identifier is **also used as a save identifier**: `ProcessImportAsync` calls
`ProcessSaveAsync(context, identifier, …)` (`CommonGenericProvider.cs:449,460`), so an import function
named e.g. `importjson` is looked up in `SaveStatement` first and then falls back to `primary`
(`:291-296`). A provider test demonstrates exactly this: only a `primary` save statement is declared
while importing under `importjson`
(`test/Ballware.Generic.Tenant.Data.SqlServer.Tests/Generic/SqlServerGenericProviderTest.cs:314-335`,
`test/Ballware.Generic.Tenant.Data.Postgres.Tests/Generic/PostgresGenericProviderTest.cs:307-328`).

## SQL placeholders and bound parameters

`ApplyTenantPlaceholderAsync` runs on **every** metadata SQL string immediately before execution
(`CommonGenericProvider.cs:188-191,216-217,244,279,334,371,401,529`;
`CommonLookupProvider.cs:114-115,126-127,143-144,160-161,172-173,184-185,202-204,222-224,235-236,246-247,258-259,270-271`;
`CommonStatisticProvider.cs:35-36`; `CommonMlModelProvider.cs:31-32`).

| Placeholder | Effect | Citation |
| --- | --- | --- |
| `[ballwareschema]` | replaced **always**, with `TenantConnection.Schema` (`dbo` on SQL Server, `public` on PostgreSQL) | `src/Ballware.Generic.Tenant.Data.SqlServer/Internal/SqlServerStorageProvider.cs:40-43`, `src/Ballware.Generic.Tenant.Data.Postgres/Internal/PostgresStorageProvider.cs:40-43` |
| `@tenantId` / `@tenant_id` | replaced **textually** with `'<guid>'` only when `options.ReplaceTenantId` | `SqlServerStorageProvider.cs:45-48`, `PostgresStorageProvider.cs:45-48` |
| `@claims` (deprecated) | replaced **textually** with `''` only when `options.ReplaceClaims` | `SqlServerStorageProvider.cs:50-53`, `PostgresStorageProvider.cs:50-53` |

`TenantPlaceholderOptions` (`src/Ballware.Generic.Tenant.Data/TenantPlaceholderOptions.cs`) has a
private constructor, a `Create()` factory with both flags **false** (`:12-15`) and the fluent
`WithReplaceTenantId()` (`:17-22`) / `WithReplaceClaims()` (`:24-29`).

**Every data-path call uses `Create()`.** So on all `/generic`, `/tenant/lookup`, `/tenant/statistic`
and `/tenent/mlmodel` routes the tenant variable is a **Dapper-bound `Guid` parameter**, not a text
substitution. The provider injects it under its own name:

| Provider | Bound parameter name | Citation |
| --- | --- | --- |
| SQL Server | `tenantId` | `src/Ballware.Generic.Tenant.Data.SqlServer/Internal/SqlServerGenericProvider.cs:7` (also the lookup/statistic/ML providers, all `:7`) |
| PostgreSQL | `tenant_id` | `src/Ballware.Generic.Tenant.Data.Postgres/Internal/PostgresGenericProvider.cs:7` (also the lookup/statistic/ML providers, all `:7`) |

Injected at `CommonGenericProvider.cs:185,211,239,274,287,364,397,512` — the **only** non-string value
the provider adds by itself. Test metadata shows the dialect difference directly: `TenantId=@tenantId`
(`test/Ballware.Generic.Tenant.Data.SqlServer.Tests/Generic/SqlServerGenericProviderTest.cs:190`)
versus `tenant_id=@tenant_id::uuid`
(`test/Ballware.Generic.Tenant.Data.Postgres.Tests/Generic/PostgresGenericProviderTest.cs:185`).

Textual replacement is used in exactly one place — the report-datasource endpoint: lookup list queries
with **both** flags (`src/Ballware.Generic.Api/Endpoints/TenantDataEndpoint.cs:62`) and the
report-datasource table query with `WithReplaceTenantId()` only (`TenantDataEndpoint.cs:88-90`).

### Claims as parameters

Claims are transferred into the SQL parameter dictionary with the prefix `claim_`
(`ClaimVariablePrefix = "claim_"`, `CommonGenericProvider.cs:30`, `CommonLookupProvider.cs:11`) through
`TransferToVariablesAsync` (`CommonGenericProvider.cs:184,213,241,276,289,362`;
`CommonLookupProvider.cs:110,139,168,197,232`). A claim named `sub` therefore binds as `@claim_sub`
(test SQL: `SqlServerGenericProviderTest.cs:193`, `PostgresGenericProviderTest.cs:188`,
`PostgresLookupProviderTest.cs:72`, `PostgresStatisticProviderTest.cs:74`).

- **There is no `@userId` parameter anywhere.** The user id reaches SQL only as `@claim_sub` and is
  otherwise passed to scripting contexts and rights checks.
- Claims are merged into the **same dictionary** as the item/query parameters
  (`Utils.TransferToSqlVariables` mutates and returns its target —
  `src/Ballware.Generic.Tenant.Data.SqlServer/Internal/Utils.cs:7-19`, and the byte-identical
  `src/Ballware.Generic.Tenant.Data.Postgres/Internal/Utils.cs:7-19`). A metadata query can therefore
  reference `@id` and `@claim_right` side by side.
- Ordering nuance: the tenant parameter is assigned **after** the claim transfer in the list
  (`:184` then `:185`), remove (`:362`/`:364`) and export (`:510`/`:512`) paths, but **before** it in the
  count (`:211`/`:213`), single-row (`:239`/`:241`), new (`:274`/`:276`) and save (`:287`/`:289`) paths.
  A claim literally named `tenantId` (or `tenant_id`) wins in the second group and loses in the first.
- The export path hard-codes the literal `"claim_"` instead of the virtual property
  (`CommonGenericProvider.cs:510`), which matters only if a provider were to override the prefix.
- The deprecated `@claims` is still **bound** (comma-joined rights) by the rights-based lookup
  overloads: `DeprecatedRightVariableName = "claims"` (`CommonLookupProvider.cs:13`), assigned
  `string.Join(",", rights)` (`:123,157,181,220,244,267`).

### Multi-value parameters

Three conventions coexist; the metadata SQL must match the one in play:

| Path | Encoding of a multi-valued key | Citation |
| --- | --- | --- |
| Claims that are `string[]` | joined into `\|a\|b\|` | `src/Ballware.Generic.Tenant.Data.SqlServer/Internal/Utils.cs:14`, `…Postgres/Internal/Utils.cs:14` (only `string[]` is joined; everything else passes through unchanged) |
| `/query`, `/count`, `/newquery`, `/tenant/statistic/dataforidentifier/{id}` | repeated keys joined into `\|a\|b\|` when there is more than one value | `src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:649-666`, `src/Ballware.Generic.Api/Endpoints/StatisticDataEndpoint.cs:41-43` |
| `/export`, `/exporturl` | raw `StringValues` forwarded to Dapper, which **expands the list** | `GenericDataEndpoint.cs:543-548,585-590`; SQL written as `Uuid in @id` (`SqlServerGenericProviderTest.cs:308`) / `uuid = ANY(@id::uuid[])` (`PostgresGenericProviderTest.cs:301`) |

Scalar values reach SQL as **text**, so the metadata must cast them. PostgreSQL test metadata is
explicit: `@id::uuid` (`PostgresGenericProviderTest.cs:308`), `@tenant_id::uuid` (`:185`),
`@claim_sub::uuid` (`:188,308`). SQL Server test metadata relies on implicit conversion
(`Uuid = @id`, `SqlServerGenericProviderTest.cs:190`). CSV-imported values are always strings
(`CommonGenericProvider.cs:480-486`), and `mlPredict` stringifies every model input
(`src/Ballware.Generic.Scripting.Jint/Internal/JintEngineExtensions.cs:16-19`).

Counter-example to keep the docs honest: a **JSON request body keeps its JSON types**.
`BodyValueBag.ReadElement` maps JSON numbers to `long`/`double`, booleans, `null`, strings, and nested
objects to `Dictionary<string, object>` / `List<object>`
(`src/Ballware.Generic.Api/Endpoints/BodyValueBag.cs:38-54`; array variant
`src/Ballware.Generic.Api/Endpoints/BodyValuesBag.cs:25-27`). Those nested containers are then removed
before the SQL statement (see [Save](#save-path)).

## Save path

`ProcessSaveAsync` (`CommonGenericProvider.cs:283-343`); `SaveAsync` opens a connection and a
transaction and commits or rolls back (`:80-97`).

1. `value[tenantVariable] = tenant.Id` (`:287`), then the claim transfer (`:289`) — claims land in the
   **same** dictionary instance.
2. `SaveStatement` lookup, `primary` fallback, `ArgumentException` when nothing exists (`:291-301`).
3. **Insert vs update** (`:303-325`):
   - `value.ContainsKey(ItemIdIdentifier) && Guid.TryParse(value[ItemIdIdentifier], out _)` (`:305`).
     `ItemIdIdentifier` is `"Id"` on SQL Server (`SqlServerGenericProvider.cs:8`) and `"id"` on
     PostgreSQL (`PostgresGenericProvider.cs:9`), and `ContainsKey` is **case-sensitive** — a lowercase
     `id` on a SQL Server tenant reads as "no id" and forces an insert.
   - Existence probe: `ProcessQuerySingleAsync(…, identifier, …, { { "id", guid } })` (`:307-308`) —
     the key is **hard-coded lowercase `id`**, so the tenant's `ByIdQuery` must reference `@id` in both
     dialects (`SqlServerGenericProviderTest.cs:190`). The probe resolves with the **save**
     identifier and falls back to `ByIdQuery` `primary`, and it runs `ByIdScript` (`:247-253`).
   - Row found → every column of that row is injected as `original_<column>` (`:312-315`), keyed after
     `ByIdScript` has already added properties.
   - Probe empty (`:317-320`), or no id key / non-Guid-parsable id (`:322-325`) → `insert = true`.
4. `BeforeSaveScript(context, identifier, insert, value)` (`:327-331`) with the enriched dictionary.
5. SQL statement (`:333-336`): `DropComplexMemberAsync` first removes values that are
   `Dictionary<string, object>`, `List<object>` or `object[]`
   (`src/Ballware.Generic.Tenant.Data.Commons/Utils/ComplexDataUtils.cs:7-14`, wrapped by
   `CommonStorageProvider.cs:49-52`). It returns a **new filtered copy**, so nested members stay in
   `value` and remain visible to `SaveScript`. Note the filter does *not* strip
   `Dictionary<string,string>` / `List<string>`, and `original_*`, `claim_*` and the tenant key stay in
   the parameter dictionary.
6. `SaveScript(context, identifier, insert, value)` (`:338-342`) — after the statement.

`savebatch` binds a JSON **array** of items (`src/Ballware.Generic.Api/Endpoints/BodyValuesBag.cs`) and
the HTTP layer checks the right for **every** item before saving **any**
(`GenericDataEndpoint.cs:426-440`).

## Remove path

`RemoveAsync` (`CommonGenericProvider.cs:99-118`) — one transaction with rollback (`:101-117`), the only
parameter is `{ "id": id }` (`:106`).

1. `RemovePreliminaryCheckScript` first (`:349-351`). On refusal the method returns immediately with
   its collected messages — **no `RemoveScript`, no SQL** (`:353-360`).
2. `RemoveScript` runs next (`:366-368`) and receives the **original** parameters, while the SQL gets the
   claim-transferred copy (`:362-364` vs `:372`). Its return value is always `true` and its messages are
   ignored.
3. A **single** SQL string `Entity.RemoveStatement` is executed with `Create()` placeholders
   (`:370-373`); rows affected are **not** inspected, and a `null` statement would be handed straight to
   Dapper.
4. Success → `RemoveResult { Result = true, Messages = [] }` (`:375-379`;
   `src/Ballware.Generic.Tenant.Data/RemoveResult.cs:3-6`).
5. HTTP mapping (`GenericDataEndpoint.cs:446-482`): the row is loaded with `ByIdAsync(..., "primary", …, id)`
   (`:464`, `DefaultQuery` at `:27`) for the `delete` right check against the **fetched row**
   (`:466-472`), then refusal → `400` whose body is a serialized `Exception` with the messages joined by
   `\r\n` (`:476-479`), otherwise `200` with an **empty** body (`:481`).

## Import path

`ProcessImportAsync` (`CommonGenericProvider.cs:425-468`) — see
[data-integration-and-jobs.md](data-integration-and-jobs.md) for the HTTP and job side.

- Function lookup: `CustomFunctions` with `Type == import && Id == identifier` (`:433-434`), else
  `ArgumentException` (`:436-439`).
- Format from `function.Options?.Format`: `csv` (`:443-453`), `json` (`:454-464`), else
  `ArgumentException "Unsupported import format …"` (`:465-467`).
- **CSV**: CsvHelper with `InvariantCulture` and `Delimiter = function.Options?.Delimiter ?? ","`
  (`:470-475`). Each **header name becomes the item key verbatim**, empty cells become `null`, and all
  values arrive as `string` (`:480-486`) — so headers must match the save statement's `@param` names.
- **JSON**: the whole stream is read and deserialized as an **array of objects** (`:493`), then
  normalized to CLR primitives/containers (`NormalizeJsonMemberAsync`, `:499`).
- Per-row rights gate: `if (await authorized(item)) ProcessSaveAsync(...)` (`:447-451` csv,
  `:458-462` json). Non-authorized rows are **skipped silently** — no counter, no report; `ImportAsync`
  returns `Task` with no result (`:135-141`). In production the callback comes from the import job, not
  from the endpoint (`src/Ballware.Generic.Jobs/Internal/GenericImportJob.cs:65-71`).
- One transaction for the whole file (`:142-157`): a single failing row aborts the entire import.

## Export path

`ProcessExportAsync` (`CommonGenericProvider.cs:506-574`).

- Claims are transferred (`:510`) and the tenant parameter added (`:512`).
- The identifier must exist in **`ListQuery`** (`:514-519`) **and** in `CustomFunctions` with
  `Type == export` (`:521-526`) — the same identifier in both, **no `primary` fallback**.
- The query runs with `Create()` placeholders (`:528-530`); **no read scripts are applied to the rows
  except in the JSON format** (below).
- Result `GenericExport { string FileName; string MediaType; byte[] Data }`
  (`src/Ballware.Generic.Tenant.Data/GenericExport.cs:3-7`), built at `:532-536`:
  `FileName = "<identifier>.<format>"` (`:534`) and `MediaType = MimeTypeMap.GetMimeType(format)`
  (`:535`, package `MimeTypeMapOfficial`,
  `src/Ballware.Generic.Tenant.Data.Commons/Ballware.Generic.Tenant.Data.Commons.csproj:18`).
  Verified: `exportjson` → `application/json` / `exportjson.json`
  (`SqlServerGenericProviderTest.cs:372-373`, `PostgresGenericProviderTest.cs:365-366`).
- `csv` (`:540-558`): CsvHelper `InvariantCulture`, delimiter from `function.Options.Delimiter ?? ","`,
  `WriteRecordsAsync(queryResult)` — **raw SQL rows**, header names are the SELECT aliases.
- `json` (`:559-568`): `ByIdScript` is applied to every row **with the export identifier**
  (`:561-564`), then UTF-8 `System.Text.Json` serialization (`:566`) with default options — keys are
  exactly the SQL result column names and casing (byte-for-byte test comparison:
  `SqlServerGenericProviderTest.cs:360,375-377` with `Id/Coltextline/Colnumber`,
  `PostgresGenericProviderTest.cs:353,370-372` with `id/coltextline/colnumber`).
- Unknown format → `ArgumentException "Unsupported export format …"` (`:569-571`).
- Hazard: the export path dereferences `function.Options.Format`/`.Delimiter` **without** a
  null-conditional (`:534,538,544,570`), unlike the import path's `function.Options?.Format` (`:441`).
  A custom function without `options` throws `NullReferenceException` on export.

## Scripts

Each evaluation is prefixed with the tenant-wide helper library:
`(Tenant.ServerScriptDefinitions ?? "") + "\n" + <script>`
(`src/Ballware.Generic.Scripting.Jint/Internal/JintEntityMetadataScriptingExecutor.cs:39-40,65,95-97,135,166-169,193`).
There are **no platform-provided JS helpers** beyond the host functions below; shared libraries are a
per-tenant metadata feature. `StateAllowedScript` is the one exception — it evaluates the script text
only (`:208`).

### What each script sees

| Script | Available variables | Notably missing |
| --- | --- | --- |
| `ListScript` (`:21-47`) | `identifier`, `item`, `addProperty`, json helpers, claim helpers, reading `entity*`, reading-SQL helpers | no `tenantId`, no `log`, no `entitySave`/`entityRemove`, no `db*`, no `params`. The result is a **lazy** projection, so the script runs while the response is enumerated (`:25-43`) |
| `ByIdScript` (`:49-69`) | same as `ListScript` | same |
| `BeforeSaveScript` (`:71-107`) | `log`, `tenantId`, `identifier`, `insert`, `item`, json helpers, claim helpers, `entity*` (read **and** write), reading-SQL helpers, `getProcessingStateName` | no `dbInsert`/`dbUpdate`/`dbDelete`, no `addProperty`, no `params` |
| `SaveScript` (`:109-146`) | as `BeforeSaveScript` **plus** `dbDelete`/`dbInsert`/`dbUpdate` (`:129`) | no `addProperty`, no `params` |
| `RemovePreliminaryCheckScript` (`:148-179`) | `params`, `tenantId`, `addResultMessage`, `querySingle(queryId, params)`, json/claim/entity/reading-SQL | no `db*`, no `identifier`, no `log`. Must **return** a boolean-able value (`bool.Parse`, `:154,171`); absent script → `(true, [])` (`:175-178`) |
| `RemoveScript` (`:181-197`) | `params`, `tenantId`, json/claim/entity/reading-SQL | no `db*`, no `addResultMessage`, no `identifier`, no `log`; result always `true` |
| `StateAllowedScript` (`:199-215`) | `state`, `hasRight`, `hasAnyRight`, `getValue` | no `ServerScriptDefinitions`, no claims/entity/SQL helpers; absent script → `false` (`:214`) |
| Statistic `FetchScript` (`JintStatisticScriptingExecutor.cs:16-41`) | `identifier`, `item`, `addProperty`, json/claim helpers, `mlPredict` | no entity/SQL helpers; `mlPredict` is registered **only** here (`JintEngineExtensions.cs:10` + `JintStatisticScriptingExecutor.cs:27`) |

The script-side tenant variable is always `tenantId` regardless of provider
(`TenantIdVariableName = "tenantId"`, `JintEntityMetadataScriptingExecutor.cs:13`, and the literal
`p.tenantId` added by the `entity*`/`db*` helpers in `JintEngineExtensions.cs:50,55,60,65,75,93,102,120,140,147,154`)
— even on PostgreSQL, where the SQL convention is `@tenant_id`.

### Host functions

| Group | Functions | Citation (`src/Ballware.Generic.Scripting.Jint/Internal/JintEngineExtensions.cs`) |
| --- | --- | --- |
| JSON | `parse(str)`, `stringify(obj)` | `:36-42` |
| Claims | `getClaim(claim)` (first value; arrays → first element; missing → `null`), `hasClaim(claim, value)`, `hasAnyClaim(claim, prefix)` | `:28-30`; `ClaimUtils.cs:5-50` |
| Other entities (read) | `entityCount(app, entity, queryId, params)`, `entityQueryList(...)`, `entityQuerySingle(...)`, `entityNew(...)` | `:48-67` |
| Other entities (write) | `entitySave(app, entity, statementId, params)` → the full save path of that entity; `entityRemove(app, entity, params)` → the full remove path | `:73-81` |
| Raw tables (**SQL Server only**) | `getCount(table, where, params)`, `getList(table, columns, where, params)`, `getSingleColumnList(...)`, `getStringColumnList(...)`, `dbDelete(table, where, params)`, `dbInsert(table, columns, values, params)`, `dbUpdate(table, columns, where, params)` | `:87-157`; `src/Ballware.Generic.Tenant.Data.SqlServer/Internal/SqlServerGenericScriptingDataProvider.cs:17-40` |
| Item enrichment | `addProperty(name, value)` — throws on an existing key | `JintEntityMetadataScriptingExecutor.cs:34-38,56-60`; `JintStatisticScriptingExecutor.cs:29-33` |
| Remove contract | `addResultMessage(msg)`, `querySingle(queryId, params)` | `JintEntityMetadataScriptingExecutor.cs:157-160` |
| State | `getProcessingStateName(state)`, `hasRight(right)`, `hasAnyRight(prefix)` (compared lower-cased), `getValue(column)` (scalar via `ScalarValueQuery ?? "primary"`) | `:90-94,130-134,203-209`; `SqlServerGenericScriptingDataProvider.cs:42-53` |
| ML | `mlPredict(model, input)` — every input value is stringified | `:10-23` (`:16-19`) |
| Diagnostics | `log(obj)` → `Debug.WriteLine` | `JintEntityMetadataScriptingExecutor.cs:80,119` |

### What scripts cannot do

- **Mutate the saved item.** `BeforeSaveScript`/`SaveScript` receive `JsonSerializer.Serialize(item)`
  and then `item = JSON.parse(item)` (`JintEntityMetadataScriptingExecutor.cs:84,96` and `:123,135`);
  each evaluation creates a **new** `Engine()` (`:77`, `:116`) that is discarded (`:95-98`, `:136`) and
  nothing is read back. Persisted side effects require `entitySave`/`entityRemove` or
  `dbInsert`/`dbUpdate`/`dbDelete`. Failures are wrapped as
  `InvalidOperationException("Error executing save script")` (`:99-103`, `:138-142`).
- Influence `original_*`, `insert`, or the chosen `SaveStatement` — all computed in the provider
  before/around the script (`CommonGenericProvider.cs:303-331`). `insert` is decided before
  `BeforeSaveScript` runs.
- Add properties during save: `addProperty` exists only for the read scripts and statistics.
- Write SQL from `BeforeSaveScript`, `ListScript`, `ByIdScript`, `RemoveScript` or
  `RemovePreliminaryCheckScript` — writing-SQL host functions are attached only in `SaveScript`
  (`JintEntityMetadataScriptingExecutor.cs:129`).
- Use the raw-table helpers on **PostgreSQL**: `getList`, `getSingleColumnList`, `getStringColumnList`,
  `getCount`, `dbDelete`, `dbInsert`, `dbUpdate` all throw `NotSupportedException`
  ("Operation deprecated and not supported on PosgreSQL storage layer" — sic) per helper
  (`src/Ballware.Generic.Tenant.Data.Postgres/Internal/PostgresGenericScriptingDataProvider.cs:11-13,19-42`).
  Only the `entity*`, `querySingle` and `getValue` families work there
  (dispatch through `src/Ballware.Generic.Tenant.Data/Internal/TenantScriptingDataProviderProxy.cs:15-48`).
- Run concurrently: scripts are synchronous and the host bridge blocks on the async providers
  (`SqlServerGenericScriptingDataProvider.cs:58-88`, `PostgresGenericScriptingDataProvider.cs:60-90`).
- Treat interpolated table/where strings as safe: `getList`/`getSingleColumnList`/`getStringColumnList`/
  `db*` interpolate caller-supplied fragments directly into SQL
  (`SqlServerGenericScriptingDataProvider.cs:19,24,29,34,39`) — they are script-controlled SQL.
- Share state across scripts or across rows: every evaluation gets a fresh `Engine()`, and `ListScript`
  runs per enumerated row.

Scripts re-enter the data paths: `entitySave` → `SaveAsync` → `ProcessSaveAsync` on a context duplicated
for the other entity, **sharing the same connection and transaction**
(`JintEngineExtensions.cs:73-77`; `SqlServerGenericScriptingDataProvider.cs:79-83`;
`PostgresGenericScriptingDataProvider.cs:81-85`;
`DefaultScriptingEntityUserContext.DuplicateForEntity` `:18-21`).

## Provider differences visible in payloads

Both providers implement the same pipeline, but the identifiers they use leak into request and response
payloads. Verified by the provider tests
(`test/Ballware.Generic.Tenant.Data.SqlServer.Tests/`, `test/Ballware.Generic.Tenant.Data.Postgres.Tests/`).

| Aspect | SQL Server | PostgreSQL | Evidence |
| --- | --- | --- | --- |
| Item id member for insert/update detection | `Id` | `id` | `SqlServerGenericProvider.cs:8` / `PostgresGenericProvider.cs:9`; `SqlServerGenericProviderTest.cs:202-204` / `PostgresGenericProviderTest.cs:197-199` |
| Row key casing | follows aliases, e.g. `Id`, `Coltextline`, `Colnumber` | e.g. `id`, `coltextline`, `colnumber` | `SqlServerGenericProviderTest.cs:183,190,207-208` / `PostgresGenericProviderTest.cs:178,185,202-203` |
| Tenant SQL variable | `@tenantId` | `@tenant_id` | test SQL as above |
| Id SQL variable inside save statements | `@Id` | `@id` / `@id::uuid` | `SqlServerGenericProviderTest.cs:193` / `PostgresGenericProviderTest.cs:188,308` |
| Typical upsert idiom | `UPDATE …; if @@ROWCOUNT = 0 begin insert … end` | `INSERT … ON CONFLICT (…) DO UPDATE SET …` | `SqlServerGenericProviderTest.cs:193` / `PostgresGenericProviderTest.cs:188` |
| Raw-table host functions | supported | `NotSupportedException` | see [What scripts cannot do](#what-scripts-cannot-do) |
| Column-name lookups | case-sensitive | case-sensitive | `CommonGenericProvider.cs:405`, `SqlServerGenericScriptingDataProvider.cs:47`, `PostgresGenericScriptingDataProvider.cs:49`; a wrong-cased column returns the **default** value instead of the scalar (`PostgresGenericProviderTest.cs:248-252`) |
| Default `[ballwareschema]` | `dbo` | `public` | `SqlServerStorageProvider.cs:42` / `PostgresStorageProvider.cs:42` |

HTTP JSON serialization renames nothing: `PropertyNamingPolicy = null` for both MVC and minimal-API JSON
options (`src/Ballware.Generic.Service/Startup.cs:234-242`), so response keys are exactly the dynamic row
keys produced by the metadata SQL aliases.

## Verification caveat

No test in this repository exercises the Jint executor: all provider tests inject
`Mock<IGenericEntityScriptingExecutor>`
(`SqlServerGenericProviderTest.cs:29,73,142-154`, `PostgresGenericProviderTest.cs:27,70,137-147`,
`SqlServerGenericScriptingDataProviderTest.cs:25,68,135-145`,
`PostgresGenericScriptingDataProviderTest.cs:23,65,130-140`). The scripting semantics above are
**source-derived**, not test-verified. Production wiring is
`services.AddScoped<IGenericEntityScriptingExecutor, JintEntityMetadataScriptingExecutor>()`
(`src/Ballware.Generic.Scripting.Jint/ServiceCollectionExtensions.cs:10-11`), enabled by
`Services.AddBallwareJintGenericScripting()` (`src/Ballware.Generic.Service/Startup.cs:334`), with Jint 4.5.0
(`src/Ballware.Generic.Scripting.Jint/Ballware.Generic.Scripting.Jint.csproj:17`).

Claim extraction and rights evaluation (including `Tenant.RightsCheckScript` and
`Entity.ExtendedRightsCheckScript`, `src/Ballware.Generic.Metadata/Entity.cs:116`) are implemented in the
external packages `Ballware.Shared.Authorization` / `Ballware.Shared.Authorization.Jint`
(`src/Ballware.Generic.Api/Ballware.Generic.Api.csproj:21`, `src/Ballware.Generic.Service/Startup.cs:332-333`)
— see [tenancy-and-authorization.md](tenancy-and-authorization.md) for what this repository does with them.

## Related pages

- [../api/README.md](../api/README.md) — parameter and payload conventions
- [../api/generic-api.md](../api/generic-api.md) — the 13 operations that drive these paths
- [tenancy-and-authorization.md](tenancy-and-authorization.md) — rights grammar and script enforcement
- [multi-tenancy-and-provisioning.md](multi-tenancy-and-provisioning.md) — provider selection and schemas
- [data-integration-and-jobs.md](data-integration-and-jobs.md) — import/export HTTP + job lifecycle
- [../best-practices/metadata-driven-payloads.md](../best-practices/metadata-driven-payloads.md) — conventions for authoring metadata
