# Plan: `docs/` folder for ballware-generic-service (API calls + data payloads)

## Goal

Add a self-contained `docs/` folder (English, tool-agnostic Markdown) that explains the service's
functionality with a strong focus on **API calls and data payloads**, derived from the generated
OpenAPI documents plus the metadata-driven payload semantics, so that `ballware-docs` can later ingest
per-service documentation directly from the service repos. Same pattern as
`ballware-meta-service/docs/` (`README.md` + `concepts/` + `api/` + `openapi/` with committed snapshots
and a fetch script). The repository `README.md` keeps its setup role and gains a pointer to `docs/`.

## Verified context (evidence — do not re-derive)

### API surface (32 mapped HTTP operations + 1 MCP endpoint)

Mounting in `src/Ballware.Generic.Service/Startup.cs:489-497`:

| Base path | apiGroup | policy | operations |
|---|---|---|---|
| `/generic` | `generic` | `genericApi` | 13 (GenericDataEndpoint.cs:37-170) |
| `/tenant/lookup` (user) | `meta` | `metaApi` | 8 (LookupDataEndpoint.cs:24-94) |
| `/tenant/lookup` (service) | `service` | `serviceApi` | 1 (LookupDataEndpoint.cs:106-113) |
| `/tenant/processingstate` | `meta` | `metaApi` | 3 (ProcessingStateDataEndpoint.cs:26-54) |
| `/tenant/statistic` | `meta` | `metaApi` | 1 (StatisticDataEndpoint.cs:20-27) |
| `/tenant/tenant` | `service` | `serviceApi` | 1 (TenantDataEndpoint.cs:21-28) |
| `/tenent/mlmodel` (sic, typo is live) | `service` | `serviceApi` | 1 (MlModelDataEndpoint.cs:20-27) |
| `/api/tenant` | `schema` | `schemaApi` | 4 (TenantSchemaEndpoint.cs:18-57) |

Policies (`Startup.cs:190-218`): all four compare the `scope` claim against
`AuthorizationOptions.RequiredMetaScope` / `RequiredServiceScope` / `RequiredSchemaScope`
(`Configuration/AuthorizationOptions.cs:24-26` → `metaApi` / `serviceApi` / `schemaApi`).
**`genericApi` and `metaApi` are identical policies** — the Generic API is authorized by the
`metaApi` scope value, not by a `genericApi` scope.

Swagger documents today: only `generic`, `service`, `schema` (`Startup.cs:412-428`); global security
requirement uses `SwaggerOptions.RequiredScopes`, default `"openid genericApi"`
(`Configuration/SwaggerOptions.cs:8`, `Startup.cs:438-444`). SwaggerUI endpoints are relative
`generic|service|schema/swagger.json` (`Startup.cs:522-524`).

### Payload semantics (source-verified)

- `identifier` is a **required query parameter** on every `/generic` route (`GenericDataEndpoint.cs:179,
  209,239,270,300,331,368,406,489,535,578`); `DefaultQuery = "primary"` (:27) is only used for the
  right-name rule and for `remove`/`byid` lookups — the README claim "identifier defaults to primary"
  is wrong.
- Rights per operation: `view` (all/query/byid), `add` (new/newquery), `edit` only when
  `identifier == "primary"` else the identifier itself (save/savebatch, :388,428), `delete` (remove),
  identifier verbatim (export/exporturl, :558,600). `POST .../import` performs **no** right check at the
  HTTP layer (:485-527); the per-row check happens in the job
  (`src/Ballware.Generic.Jobs/Internal/GenericImportJob.cs:65-71`).
- Query rows are Dapper dynamics: **JSON keys are the SELECT aliases**, no case normalization
  (`select Uuid as Id` → `Id` on mssql, `uuid as id` → `id` on postgres). POCO bodies stay PascalCase
  (`PropertyNamingPolicy = null`, `Startup.cs:233-241`); `CountResult` pins `{"count":n}` via
  `[JsonPropertyName]` (`GenericDataEndpoint.cs:19-23`).
- SQL placeholders: `[ballwareschema]` → tenant schema; `@tenantId` (mssql) / `@tenant_id` (postgres)
  are bound parameters in the data paths (`TenantPlaceholderOptions.Create()`,
  `CommonGenericProvider.cs:191,217,244,279,334,371,401,529`); only the report-datasource endpoint
  substitutes them textually (`TenantDataEndpoint.cs:62,88-90`). Claims become `@claim_<claimtype>`
  (`ClaimVariablePrefix = "claim_"`, `CommonGenericProvider.cs:30`); **there is no `@userId`** — the
  user id reaches SQL as `@claim_sub`. `@claims` is deprecated (`CommonLookupProvider.cs:13`) but still
  used in real metadata SQL.
- Two multi-value conventions: `/query` + `/count` join repeated keys into `|a|b|`
  (`GenericDataEndpoint.cs:649-666`, same inline in `StatisticDataEndpoint.cs:39-49`), while
  `/export` + `/exporturl` forward raw `StringValues` for Dapper list expansion
  (`Uuid in @id` / `uuid = ANY(@id::uuid[])`). All values arrive as strings; SQL must cast.
- Save: body is the **bare item object** (`BodyValueBag.cs:11-54`); `savebatch` is a bare JSON array.
  Insert vs update decided by Guid-parsable `Id`/`id` + existence via `ByIdQuery`; existing columns are
  injected as `original_<column>`; nested JSON members are stripped before the SQL statement but remain
  visible to scripts (`CommonGenericProvider.cs:283-343`). `BeforeSaveScript`/`SaveScript` receive a
  JSON copy of `item`, so **mutating `item` does not persist** — side effects require
  `entitySave`/`dbInsert`/`dbUpdate` (`JintEntityMetadataScriptingExecutor.cs:71-146`). Response:
  `200` with **empty body**.
- Remove: preliminary check script → on refusal `400` whose body is a serialized `Exception` with
  messages joined by `\r\n` (`GenericDataEndpoint.cs:476-479`).
- Import: multipart field `files` → upload to storage service + job record in the meta service +
  Quartz trigger `JobKey.Create("import","generic")` → `201` empty. Job states
  `Unknown|Queued|InProgress|Finished|Error` (`Metadata/Job.cs:21-37`), persisted in the meta service.
  Format comes from entity `CustomFunctions` (`Type = import`, `options.format` = `csv`|`json`,
  `options.delimiter`, `CommonGenericProvider.cs:425-504`); CSV headers must match the save statement's
  `@param` names.
- Export: identifier must exist **both** in `ListQuery` and in `CustomFunctions(Type = export)` else
  `ArgumentException` → 500 (`CommonGenericProvider.cs:506-574`). `MediaType` via `MimeTypeMap`.
  `export` returns the raw body; `exporturl` reads **form fields** as query params and returns
  `{"TenantId":guid,"Id":guid}`; `download/{tenantId}/{id}` is `AllowAnonymous()` with 1-day expiry
  (`:616,639`) and its declared `contentType: "application/json"` is not what is served (:165 vs :646).
- Errors: metadata resolution → `404` (`"Unknown tenant or entity"`, lookup/statistic/state/ML
  messages), rights → `401` empty, `isstateallowed` → `403` empty
  (`ProcessingStateDataEndpoint.cs:170`), malformed save body → `400 "No value(s) provided"`;
  unhandled exceptions → `500 application/problem+json` (`Startup.cs:457-482`).
- MCP: mounted at `/generic/mcp` when `Mcp.Enabled` (`Startup.cs:504-510`); per-user dynamic tools
  `generic_{tenantId}_{entity}_query_{queryId}` / `..._byid_{queryId}` from `AiEnabled` queries,
  output `{ "results": [...] }` (`Ballware.Generic.Mcp/Endpoints/GenericTools.cs:56,121,207-248`).

### Research inputs (private, never quoted in docs)

`/Users/frank/Documents/GitHub/ballware-fwpro/meta/metadata-entities.json` (20 platform meta entities),
`tenant/ffw_pr_oldendorf*.json`, and the split exports `entity|lookup|statistic|page/__export.json`
(42 entities, 71 lookups, 10 statistics, 42 pages). Conventions to abstract (not copy): query identifier
families (`primary`, `report`, `reporting`, `exportjson`, `importjson`, `state`, `<scope>count`,
`my…`), `parameters`/`result_columns` typed `string|number|bool` only where `ai_enabled`,
`ScalarValueQuery` is a **string identifier** (sometimes empty) while `RemoveStatement` is a **single SQL
string**, states keyed by state-row GUIDs in `Successors`, rights grammar
`<app>.<entity>.<action>` with templated scope suffixes and `.state.<n>` variants, tenant-wide helper
functions live in `Tenant.ServerScriptDefinitions` (not platform built-ins), binary columns travel as
hex strings (`CONVERT(varbinary(max), @X, 2)`), and no server-side paging/sorting exists (`ORDER BY`
lives in the query text).

## Decisions (agreed with user)

1. **Add a fourth Swagger document `meta`** so the 12 currently undocumented operations
   (lookup user ×8, processingstate ×3, statistic ×1) become OpenAPI-backed. Title
   **"ballware Generic Meta API"** (distinct from the meta-service's "ballware Meta API"), group name
   stays `meta` so `SwaggerDoc("meta")` matches `WithGroupName("meta")`.
2. **Per-document security requirements** derived from `AuthorizationOptions` instead of one global
   requirement from `SwaggerOptions.RequiredScopes`.
3. Fix the three copy-paste `WithSummary("Query existing tenant")` strings on `byid`/`save`/`remove`.
4. Language English; structure mirrors `ballware-meta-service/docs/`
   (`README.md` + `concepts/` + `api/` + `openapi/` + `fetch-spec.sh` + `manifest.json`), plus **one
   anonymized best-practices page**.
5. Commit OpenAPI snapshots for all four documents; regeneration is a documented manual step
   (no CI job).
6. Tool-agnostic plain Markdown: relative links, ASCII diagrams, tables, JSON fences, `path:line`
   citations. No MDX, no frontmatter, no Docusaurus sidebars/admonitions.
7. Production exports are research input only: **no** tenant names, GUIDs, emails, hosts, secrets,
   schemas/table names, SQL snippets, right strings, or file paths from `ballware-fwpro` or
   `appsettings.local.json` may appear in docs. All examples are synthetic with
   `<application>`, `<entity>`, `<guid>`, `<token>`, `<tenantId>` placeholders.

## Target tree

```
docs/
  README.md                         index + contents table + scope + consumption notes for ballware-docs
  concepts/
    architecture.md                 project layout, request pipeline, mounted routes, caching, Quartz, error contract
    tenancy-and-authorization.md    OIDC/JWT, scope→policy table, claims, rights grammar, script enforcement
    multi-tenancy-and-provisioning.md schema-per-tenant, provider selection, master store, schema diffing
    metadata-driven-execution.md    how metadata decides payloads: identifiers, SQL placeholders, scripts, DSL
    data-integration-and-jobs.md    import/export pipelines, storage client, job lifecycle
    mcp-endpoint.md                 /generic/mcp overview (explicitly not in OpenAPI)
  api/
    README.md                       four documents, groups/tags/operationIds, identifier + payload conventions
    generic-api.md                  13 operations + payload contracts
    meta-api.md                     12 operations (lookup user, processingstate, statistic)
    service-api.md                  3 operations (lookup column value, ml training data, report datasources)
    schema-api.md                   4 operations (EntitySchema / TenantSchema payloads)
    schemas.md                      field tables for every component schema of all four snapshots
    examples.md                     copy-pasteable curl sequences
  best-practices/
    metadata-driven-payloads.md     anonymized conventions for query/save/import/export/state definitions
  openapi/
    README.md                       how the snapshot is produced/refreshed, consumer notes
    fetch-spec.sh                   swagger-to-file driver (4 documents)
    generic.json  meta.json  service.json  schema.json
    manifest.json                   provenance: tool + version, environment, git describe, fetchedAt, counts
```

## Tasks (ordered)

### A. Source changes (do first — snapshots must reflect them)

1. `src/Ballware.Generic.Service/Startup.cs`
   - Add `c.SwaggerDoc("meta", new OpenApiInfo { Title = "ballware Generic Meta API", Version = "v1" })`
     next to the three existing documents (`:412-428`).
   - Replace the single global `AddSecurityRequirement` (`:438-444`) with a per-operation filter that
     maps `ApiDescription.GroupName` → scope: `service` → `RequiredServiceScope`,
     `schema` → `RequiredSchemaScope`, `meta`/`generic`/ungrouped → `RequiredMetaScope`, emitting
     `operation.Security = [ { oidc → ["openid", <scope>] } ]` via an `IOperationFilter`
     (register inside the existing `AddSwaggerGen` lambda). Keep `Swagger.RequiredScopes` only for the
     SwaggerUI OAuth button; extend the UI `OAuthScopes` so it can exercise all four documents.
   - Add `c.SwaggerEndpoint("meta/swagger.json", "ballware Generic Meta API")` to the SwaggerUI block
     (`:522-524`).
   - Verify with the generated snapshots that the existing three documents are **unchanged** except for
     the security block (all operations have explicit group names, so no operation moves documents).
2. `src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs`
   - Correct the three summaries at `:95` (`byid`), `:106` (`save`), `:128` (`remove`) to
     "Query item by id", "Save item", "Remove item" (keep summaries short and accurate).
3. Build/test gate before writing any prose: `dotnet restore` + `dotnet build ballware-generic-service.sln -c Release`
   + `dotnet test ballware-generic-service.sln -c Release` (private `ballware` NuGet feed required;
   Testcontainers tests need `TestSettings__DatabasePassword`).

### B. Snapshot generation

4. `docs/openapi/fetch-spec.sh` — model directly on
   `ballware-meta-service/docs/openapi/fetch-spec.sh`:
   - `dotnet build src/Ballware.Generic.Service --configuration Release`
   - tool availability: `dotnet tool install Swashbuckle.AspNetCore.Cli --version 10.3.0 --global`
     guarded by `command -v swagger`, with the CI alternative documented
     (`dotnet new tool-manifest` + `dotnet tool update … --version 10.3.0`, CI pin
     `publish-packages.yml:32`); pin **10.3.0** to match `Swashbuckle.AspNetCore 10.3.0`
     (`Ballware.Generic.Service.csproj:33-34`) and avoid formatting drift.
   - `export ASPNETCORE_ENVIRONMENT=Swagger` **and** `export Authorization__Authority="https://localhost"`.
     The env override is mandatory here: `src/Ballware.Generic.Service/appsettings.local.json` exists in
     the working tree (gitignored, `.gitignore:487`) and is loaded *after* the Swagger file
     (`Program.cs:13-17`), so without the override the live IdP URL would be baked into four committed
     documents.
   - run from the project directory (content root, like CI) for each of `generic meta service schema`:
     `swagger tofile --output "$REPO_ROOT/docs/openapi/<doc>.json" bin/Release/net10.0/Ballware.Generic.Service.dll <doc>`
   - validate each file (parses, `openapi` starts with `3.`, `paths` non-empty) and write
     `manifest.json` with `source`, `toolVersion`, `environment: Swagger`,
     `serviceVersion: git describe --tags --always --dirty`, `fetchedAt`, and per-document
     `{file, openapi, title, pathCount, schemaCount}`.
5. Run the script and commit the four snapshots + `manifest.json`. Expected (verify, do not hard-code):
   `generic` 13 paths / 3 schemas, `meta` 12 paths / 1 schema, `service` 3 paths / 3 schemas,
   `schema` 4 paths / 2 schemas. Record the real counts in `manifest.json` and in the docs tables.
   No database or IdP is required (`swagger tofile` resolves the host without starting it, and CI proves
   the mechanism for `service`/`schema`). Fallback to document if it ever fails: run the host with
   `ASPNETCORE_ENVIRONMENT=Swagger` and curl `http://localhost:5002/swagger/<doc>/swagger.json`.

### C. Documentation pages

6. `docs/README.md` — purpose, scope note (*backend view of the generic data engine; the frontend
   interpretation of metadata lives in `ballware-angular/docs/metadata`; the metadata authoring model
   lives in `ballware-meta-service/docs`*), contents table, quick facts (four documents and their
   Swagger JSON URLs, dev port `5002` from `appsettings.Development.json:8-14` and the compose mapping,
   MCP mount, `Mcp.RequiredMcpScope`), request-flow ASCII diagram
   (`client → JWT/scope → policy → metadata + rights → tenant provider (mssql|postgres) → meta/storage/ML
   clients → Quartz`), link to `../README.md` for setup and to `openapi/README.md` for regeneration, plus
   a **Consumption notes** section for `ballware-docs` (plain Markdown, relative links, no frontmatter,
   snapshots in `openapi/` are canonical, `manifest.json` provenance, and the note that
   `ballware-docs/scripts/fetch-openapi.mjs` should extend its generic `docs` list to
   `["generic","meta","service","schema"]`).
7. `docs/concepts/architecture.md` — three data layers (master store / tenant schemas / external
   services), project table from `README.md`, request pipeline with `Startup.cs` citations, mounted-route
   table (base path → group → policy → operations), caching (`Storage.EnableCaching`, `Cache.*`), Quartz
   clustered job store, CORS, `ValidateOnStart` configuration failure, `ProblemDetails` contract.
8. `docs/concepts/tenancy-and-authorization.md` — JWT/OIDC setup (`Startup.cs:174-188`),
   **scope→policy table incl. the `genericApi` ≡ `metaApi` equivalence**, claim names
   (`tenant`, `sub`, `right`) and their config keys, tenant-from-claim vs explicit `{tenantId}` routes,
   rights grammar `<application>.<entity>.<right>` with the identifier→right rule, per-endpoint right
   table, script-based enforcement (tenant `RightsCheckScript`, entity `ExtendedRightsCheckScript`,
   `StateAllowedScript`) as the real control point, expected statuses (401/403/404/400/500), and the
   fact that `import` checks rights only inside the job. Synthetic script patterns only.
9. `docs/concepts/multi-tenancy-and-provisioning.md` — schema-per-tenant + per-tenant DB user,
   provider enablement (`TenantStorage.Provider`, `SqlServerTenantStorage.Enabled`,
   `PostgresTenantStorage.Enabled`), `TenantConnection` persistence, diff-based schema alignment,
   `ProviderModelDefinition`-style tenant DB objects reached via `[ballwareschema]`, and the provider
   naming differences that show up in payloads (`Id` vs `id`, `@tenantId` vs `@tenant_id`).
10. `docs/concepts/metadata-driven-execution.md` — the payload-defining contract: `ListQuery`,
    `ByIdQuery`, `NewQuery`, `SaveStatement`, `ScalarValueQuery`, `RemoveStatement`, `CustomFunctions`,
    `Parameters`/`ResultColumns`, `AiEnabled`; unknown identifier → silent fallback to `primary`;
    `original_<column>` injection; complex members stripped from SQL; the Jint host-function DSL
    (`entityQueryList/entityQuerySingle/entityNew/entitySave/entityRemove/entityCount`,
    `getList/getCount/dbInsert/dbUpdate/dbDelete`, `getClaim/hasClaim/hasAnyClaim`, `parse/stringify`,
    `mlPredict`, `addProperty`, `addResultMessage`, `getValue`, `hasRight/hasAnyRight`,
    `getProcessingStateName`, `log`, `tenantId`, `identifier`, `item`, `insert`, `params`, `state`) with
    an explicit "scripts cannot mutate the saved item" caveat; note that metadata itself is authored and
    served by the meta service (cross-link, do not duplicate).
11. `docs/concepts/data-integration-and-jobs.md` — import sequence (multipart → storage service →
    job record → Quartz trigger → per-row rights → file cleanup) with the `JobDataMap` keys and
    `JobCreatePayload`/`JobUpdatePayload` shapes and state lifecycle; export sequence
    (`export` inline vs `exporturl` + anonymous `download` with 1-day expiry, filename
    `<query>_<yyyyMMdd_HHmmss><ext>`); CSV/JSON format options incl. header-name ↔ `@param` mapping and
    binary-as-hex convention; storage/meta/ML client-credentials calls (`Startup.cs:253-316`).
12. `docs/concepts/mcp-endpoint.md` — `/generic/mcp`, `Mcp` options (`Enabled`, `RequiredMcpScope`
    defaulting to the meta scope, `ResourceUri`), tool naming/description/input params/output schema,
    re-check of tenant membership + `view` right, and the explicit statement that MCP is **not** part of
    the OpenAPI documents.
13. `docs/api/README.md` — the four documents (`generic`, `meta`, `service`, `schema`) with titles,
    path/schema counts and Swagger JSON URLs; group/tag/operationId conventions (`WithName` prefixes →
    `GenericAll`, `LookupSelectListForLookupId`, `ProcessingStateIsStateAllowedForEntityByIdentifierStateAndId`,
    `TenantReportDatasources`, …); per-document required scope (from the new security block);
    PascalCase bodies and the `count` exception; **`identifier` semantics table** (required on all
    `/generic` routes, absent on `remove/{id}` and `download`, right-name consequences); the two
    multi-value conventions (`|a|b|` vs raw list expansion); anonymous download; token acquisition sketch
    (client credentials for `serviceApi`/`schemaApi` callers, authorization code + PKCE for Swagger UI).
14. `docs/api/generic-api.md` — one section per operation: method + path, params, body, declared vs
    actual responses, right checked, and a payload example. Dedicated subsections for the item shape
    (alias-derived keys, GUID/date/number/null handling), `save` insert-vs-update rules, `savebatch`
    all-rights-first-then-sequential-save, the failed-remove `400` body, import multipart (`files`),
    `export` → `exporturl` → `download` flow. Note the spec limitations honestly
    (`Produces<IEnumerable<object>>` yields an untyped array in OpenAPI; payload shape comes from
    metadata) and the known inaccuracies that remain: the `download` `contentType` declaration, the
    undeclared 400/403/404 statuses, and the `/tenent/mlmodel` path typo.
15. `docs/api/meta-api.md` — the 12 operations of the new `meta` document: lookup select-list /
    by-id / with-param / autocomplete pairs (200 shapes: array of rows, single row, array of strings),
    the processing-state trio (allowed successors for one and many ids incl. intersection semantics and
    the empty-200 when no `id` is given; `200` vs `403` for `isstateallowed`), and the statistic
    operation (row array, `FetchScript` enrichment, `|a|b|` params). Include the exact 404 message
    strings and the note that these routes carry **no tenant/entity right check** beyond the scope policy.
16. `docs/api/service-api.md` — the 3 operations with payloads: single column value lookup (with the
    `404 "Column '…' not found in the result."` case), ML training data (`[FromBody] string` raw SQL,
    `400 "Query is empty."`), report datasources (synthetic `Lookups` definition, provider/connection
    string overwritten from the tenant connection, tables filtered to those with `Entity`, and the
    security note that the tenant connection string is embedded in the response).
17. `docs/api/schema-api.md` — the 4 operations with `EntitySchema { UserId, SerializedEntityModel }` and
    `TenantSchema { UserId, Provider, SerializedTenantModel }` payloads, the `userId` query parameter on
    the two DROP routes, the fact that no metadata/right checks exist, and that DDL failures surface as
    `500 application/problem+json`.
18. `docs/api/schemas.md` — field tables (Field / Type / Nullable / Notes) for every component schema in
    the four snapshots, each table headed with `docs/openapi/<doc>.json#/components/schemas/<Name>` plus
    the DTO source (`src/Ballware.Generic.Api/Public/*.cs`, `src/Ballware.Generic.Metadata/*.cs`).
    Document enum-valued fields with their serialized values (`CustomFunctionTypes`:
    `add|edit|default_add|default_view|default_edit|external|export|import`; `JobStates`).
19. `docs/api/examples.md` — copy-pasteable curl blocks against `http://localhost:5002` with `$TOKEN`:
    query with parameters and multi-value params, count, new/newquery, byid, save (new + update),
    savebatch, remove (incl. the failed-precondition `400`), import multipart CSV + json, export inline,
    exporturl → download, lookup select-list/with-param/autocomplete, lookup column value, processing
    state successors + `isstateallowed`, statistic with params, ML training data, report datasources,
    schema create-or-update entity schema. **Every request path must be validated against the snapshots
    before it is written**; all payloads synthetic/sanitized.
20. `docs/best-practices/metadata-driven-payloads.md` — anonymized conventions abstracted from the
    production exports: query identifier families (`primary` default, `<verb><format>` functions such as
    `exportjson`/`importjson`, `<scope>count` pairs for `/count`, `report`/`reporting`), the
    parameter/result-column declaration that feeds MCP tool schemas (`ai_enabled` + typed
    `string|number|bool`), placeholder usage (`[ballwareschema]`, `@tenantId`/`@tenant_id`,
    `@claim_sub`, `@claim_right`, `@param`, `@id`, `|v1|v2|` matching idiom), read-from-view /
    write-to-table split, dialect-specific SQL + upsert idioms for both providers, audit columns written
    in SQL, state-change save via a dedicated `SaveStatement` identifier (right name = identifier),
    association maintenance through `entitySave`/`entityRemove` in `SaveScript` with tenant helper
    libraries (`ServerScriptDefinitions`), binary-as-hex transfer, and "no server-side paging/sorting —
    `ORDER BY` lives in the query text". Explicit disclaimer: patterns are generalized, not taken from
    any specific tenant.
21. `README.md` — insert a short `## Documentation` section (after `Features`/before `Configuration`)
    pointing to `docs/README.md`, `docs/api/README.md`, and the committed OpenAPI snapshots in
    `docs/openapi/`; reword the existing Swagger sentence to mention the four raw documents
    (`/swagger/generic|meta|service|schema/swagger.json`). **Also correct the single inaccurate line**
    in the Generic Data API table ("`identifier` defaults to `primary`" → required query parameter with
    an internal `primary` fallback). No other README changes.

## Content rules

- English; plain `.md`; relative links; ASCII diagrams; tables + JSON fences; citations as `path:line`.
  No MDX/frontmatter/Docusaurus directives.
- Never invent endpoints or fields: every documented path must exist in
  `docs/openapi/{generic,meta,service,schema}.json`. Untyped OpenAPI payloads are explained as
  metadata-derived, never fabricated as fixed schemas.
- Secrets and production data stay out: no `appsettings.local.json` values (IdP URL, client secrets,
  connection strings), no fwpro tenant names/GUIDs/emails/table or schema names/SQL/right strings.
- Payload emphasis: request/response bodies, parameter semantics, and identifier/rights coupling over
  prose.
- State known deviations explicitly rather than hiding them (identifier requiredness, `genericApi` ≡
  `metaApi`, undeclared 400/403/404 responses, `download` content type, `/tenent/mlmodel` typo).

## Out of scope

- CI changes: no job that regenerates or verifies the snapshots. Extending
  `.github/workflows/publish-packages.yml` with a `meta` (and `generic`) `swagger tofile` step and
  publishing the documents as artifacts is a documented optional follow-up only.
- Any change to `Ballware.Generic.Service.Client` / `Ballware.Generic.Schema.Client` or their NSwag
  configs.
- Changes in `ballware-docs` (import mechanics, sidebars, `fetch-openapi.mjs` `meta` entry) — noted as a
  consumer follow-up, not implemented here.
- Behavioral API changes (no policy, route, or rights changes; no fix of the `/tenent/mlmodel` typo, no
  change to `genericApi`'s scope source, no per-operation 400/403 declarations).
- MCP protocol internals of `Ballware.Shared.Mcp*` (external packages, not verifiable from this repo).

## Risks / gotchas

- `swagger tofile` needs a successful `Release` build, which needs the private `ballware` NuGet feed.
  If restore is unavailable, snapshots cannot be regenerated — document the prerequisite.
- Snapshot leakage: `appsettings.local.json` overrides the Swagger configuration in this repo today;
  the script pins `Authorization__Authority`, but regeneration from a dirty checkout must be checked for
  unexpected `openIdConnectUrl` values.
- The new `meta` document is a public API-surface change: verify the three existing documents are
  byte-identical except the security block, and confirm `pathCount` 13/3/4 for them so the new
  document did not absorb operations.
- Changing the security block alters every snapshot (`security` moves from a global array to
  per-operation). Regenerate before writing `docs/api/schemas.md` counts and example paths.
- The 12 `meta`-group operations have no tenant/entity right checks; the docs must not imply rights
  enforcement where only the scope policy applies.
- Docs/spec drift: snapshots are manual artifacts; re-running the script should only change
  `manifest.fetchedAt`.
- Payload claims about Dapper row serialization and list-parameter expansion are derived from provider
  tests (`test/Ballware.Generic.Tenant.Data.{SqlServer,Postgres}.Tests/`) and shared packages, not from
  package source; phrase them as observed behavior and cite the tests.

## Validation

1. `dotnet build ballware-generic-service.sln -c Release` and
   `dotnet test ballware-generic-service.sln -c Release` green after the source changes.
2. `jq -r '.openapi' docs/openapi/*.json` → all `3.*`;
   `jq -r '.paths|keys|length' docs/openapi/<doc>.json` → record actual counts (expect 13/12/3/4) and
   confirm `meta.json` contains exactly the 8 lookup + 3 processingstate + 1 statistic paths.
3. Regression check: diff regenerated `generic.json` / `service.json` / `schema.json` against the
   equivalent files in `~/Documents/GitHub/ballware-docs/openapi/generic/` — path sets must match
   (only the security block and the three corrected summaries may differ).
4. Path audit: extract every path literal from `docs/api/*.md` and `docs/api/examples.md` and confirm
   each exists in the snapshots (`jq -r '.paths|keys[]'`); no extras.
5. Schema audit: every `#/components/schemas/<Name>` referenced in `docs/api/schemas.md` exists in the
   corresponding snapshot; counts match `manifest.json`.
6. Link audit: `test -f` every relative link target in `docs/**` and in the new `README.md` section.
7. Idempotency: re-run `docs/openapi/fetch-spec.sh` → `git diff` shows only `manifest.json` `fetchedAt`.
8. Sanitization audit: search `docs/**` and the `README.md` diff for `identity.ballware.de`, `coolify`,
   `appsettings.local`, fwpro tenant identifiers, GUID literals, email domains, client secrets, and
   `ballware-fwpro` / `metadata-entities` references. Any match must be replaced with placeholders.
9. Spec-leak check: each snapshot's `components.securitySchemes.oidc.openIdConnectUrl` must be
   `https://localhost/.well-known/openid-configuration` and must be labeled in `docs/openapi/README.md`
   as a placeholder, not a real endpoint.

## Open questions

- Should CI also export `generic` and `meta` so the committed snapshots can be refreshed automatically
  (deferred follow-up)?
- Should `ballware-docs` import `docs/**` statically from the service repos, or keep fetching live
  specs from port 5002? The import mechanics live in `ballware-docs` and are not decided here.
