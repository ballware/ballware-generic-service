# Architecture

`ballware-generic-service` is a stateless ASP.NET Core minimal-API host that executes **metadata**
(supplied by the external meta service) against **per-tenant database schemas**. All behaviour that a
conventional service would implement in controllers and repositories is described in metadata:
which SQL runs, which rights apply, which scripts run, which columns exist.

All paths below are relative to the repository root. `Startup.cs` means
`src/Ballware.Generic.Service/Startup.cs`.

## Three data layers

| Layer | What it holds | How it is accessed | Wiring |
| --- | --- | --- | --- |
| **Master store** | tenant registry: `TenantConnection` (provider, schema, connection string, persisted tenant model) and one `TenantEntity` record per entity model | EF Core 10, SQL Server or Npgsql provider; automatic migrations when `Storage.AutoMigrations` | `Startup.cs:349-357` (`AddBallwareTenantStorageForSqlServer` / `…ForPostgres`), options in `src/Ballware.Generic.Data.Ef*/` |
| **Tenant storage** | the business data itself, one database schema per tenant (default `dbo` / `public`), one database user per tenant | Dapper, provider-specific implementations registered side by side | `Startup.cs:359-398` (`AddBallwareTenantGenericStorage` gated by `SqlServerTenantStorage.Enabled` / `PostgresTenantStorage.Enabled`) |
| **External platform services** | metadata, jobs, export records (meta service), temporary import/export files (storage service), model predictions (ML service) | typed `HttpClient`s with OAuth2 client-credentials tokens | `Startup.cs:254-317` (`AddClientCredentialsTokenManagement` + `AddHttpClient<…>`) |

`TenantStorage.Provider` selects the provider of the **master store** and is validated against
`mssql|postgres` (`Startup.cs:122-127`). The **tenant data** providers are independent toggles, so a
deployment can serve SQL Server tenants and PostgreSQL tenants at the same time; the provider used
for a request comes from the stored `TenantConnection.Provider` of that tenant
(see [multi-tenancy-and-provisioning.md](multi-tenancy-and-provisioning.md)).

## Project layout

| Project | Responsibility in the request path |
| --- | --- |
| `src/Ballware.Generic.Service` | executable host: DI wiring, authentication, policies, service clients, Quartz, caching, MCP endpoint, Swagger (`Program.cs`, `Startup.cs`) |
| `src/Ballware.Generic.Api` | minimal-API endpoints and their payload binding (`Endpoints/*.cs`, `Public/*.cs`) |
| `src/Ballware.Generic.Metadata` | provider-neutral metadata model (`Entity`, `QueryEntry`, `CustomFunctionEntry`, `Lookup`, `Statistic`, `ProcessingState*`, `ReportDatasource*`, `Export*`, `Job*`, `Tenant`) plus `IMetadataAdapter` / `IMlAdapter` |
| `src/Ballware.Generic.Service.Adapter` (inside `.Service`) | `MetaServiceMetadataAdapter`, `MlServiceMlAdapter`, `StorageServiceFileStorageAdapter` — adapters from the external clients to the metadata/storage contracts (`Startup.cs:327-330`) |
| `src/Ballware.Generic.Tenant.Data` | provider abstraction: `ITenantGenericProvider`, `ITenantLookupProvider`, `ITenantStatisticProvider`, `ITenantMlModelProvider`, `ITenantSchemaProvider`, `ITenantStorageProvider`, `TenantPlaceholderOptions`, `TenantStorageBuilder` |
| `src/Ballware.Generic.Tenant.Data.Commons` | provider-independent pipelines: generic CRUD/query/count/export/import, lookup, statistic, ML model data |
| `src/Ballware.Generic.Tenant.Data.SqlServer` / `.Postgres` | Dapper implementations of the provider interfaces, including schema DDL and model diffing |
| `src/Ballware.Generic.Scripting` / `.Scripting.Jint` | script contracts and the Jint host with the script DSL |
| `src/Ballware.Generic.Jobs` | Quartz job infrastructure (`GenericImportJob`) |
| `src/Ballware.Generic.Caching`, `src/Ballware.Generic.Data.Caching` | tenant-connection cache over `IDistributedCache` and its read-through decorator |
| `src/Ballware.Generic.Mcp` | dynamic MCP tool registration from entity metadata |
| `src/Ballware.Generic.Service.Client`, `src/Ballware.Generic.Schema.Client` | NSwag-generated clients, generated in CI from the `service` and `schema` documents |

The executable has no MVC controllers: `AddMvcCore().AddApiExplorer()` is registered only so that the
API explorer can describe minimal-API endpoints for Swagger (`Startup.cs:246-249`).

## Request pipeline

```text
request
  -> Kestrel (dev: http://localhost:5002)
  -> exception handler                      Startup.cs:458-483
  -> CORS                                    Startup.cs:485  (policy from Startup.cs:221-232)
  -> routing                                 Startup.cs:486
  -> authentication (JWT bearer)             Startup.cs:175-189, enforced by routing
  -> authorization (policy per endpoint)     Startup.cs:192-219, 488
  -> endpoint handler                        src/Ballware.Generic.Api/Endpoints/*.cs
       1. resolve tenant id / user id / claims from the JWT
       2. IMetadataAdapter -> tenant + entity/lookup/statistic metadata (meta service)
       3. ITenantRightsChecker + IEntityRightsChecker (optionally Jint scripts)
       4. ITenant*Provider (mssql | postgres, Dapper + Jint scripts)
       5. optionally the storage client (files), the ML client, or the Quartz scheduler
  -> JSON response (PascalCase POCOs, metadata-derived rows)
```

Notable details:

- **Claim mapping is disabled** (`options.MapInboundClaims = false`, `Startup.cs:180`), so claim types
  are read exactly as issued (`tenant`, `sub`, `right` by default; configurable via
  `Authorization:TenantClaim` / `UserIdClaim` / `RightClaim`, `Startup.cs:332`).
- Metadata resolution happens **before** the rights check on most routes; a missing tenant or entity
  yields `404` with a JSON string body (`GenericDataEndpoint.cs:188-191`), while a refused right yields
  `401` with an **empty** body (`GenericDataEndpoint.cs:197-200`).
- `byid` inverts the order deliberately: the row is fetched first, then the `view` right is evaluated
  **against the fetched row**, so scripts can decide per row (`GenericDataEndpoint.cs:345-358`).
- `savebatch` evaluates the right for **every** item before saving **any** of them
  (`GenericDataEndpoint.cs:426-440`).
- `import` performs no rights check at the HTTP layer at all — it only uploads the file, registers a
  job, and triggers the scheduler (`GenericDataEndpoint.cs:485-527`); per-row authorization happens in
  the job (see [data-integration-and-jobs.md](data-integration-and-jobs.md)).

## Mounted routes

`Startup.cs:490-498`:

| Base path | Mapping call | API group (→ OpenAPI document) | Authorization policy | Operations | Endpoint file |
| --- | --- | --- | --- | --- | --- |
| `/generic` | `MapGenericDataApi` | `generic` | `genericApi` | 13 | `src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:30-173` |
| `/tenant/lookup` | `MapLookupUserDataApi` | `meta` | `metaApi` | 8 | `src/Ballware.Generic.Api/Endpoints/LookupDataEndpoint.cs:17-97` |
| `/tenant/lookup` | `MapLookupServiceDataApi` | `service` | `serviceApi` | 1 | `src/Ballware.Generic.Api/Endpoints/LookupDataEndpoint.cs:99-116` |
| `/tenent/mlmodel` | `MapMlModelDataApi` | `service` | `serviceApi` | 1 | `src/Ballware.Generic.Api/Endpoints/MlModelDataEndpoint.cs:12-30` |
| `/tenant/processingstate` | `MapProcessingStateDataApi` | `meta` | `metaApi` | 3 | `src/Ballware.Generic.Api/Endpoints/ProcessingStateDataEndpoint.cs:19-55` |
| `/tenant/statistic` | `MapStatisticDataApi` | `meta` | `metaApi` | 1 | `src/Ballware.Generic.Api/Endpoints/StatisticDataEndpoint.cs:12-30` |
| `/tenant/tenant` | `MapTenantServiceDataApi` | `service` | `serviceApi` | 1 | `src/Ballware.Generic.Api/Endpoints/TenantDataEndpoint.cs:10-31` |
| `/api/tenant` | `MapTenantServiceSchemaApi` | `schema` | `schemaApi` | 4 | `src/Ballware.Generic.Api/Endpoints/TenantSchemaEndpoint.cs:10-57` |
| `/generic/mcp` | `MapBallwareUserMcpEndpoint` (only when `Mcp.Enabled`) | — not in any document | `Mcp.RequiredMcpScope` | MCP endpoint | `Startup.cs:505-511` |

The base path `/tenent/mlmodel` contains a typo that is **live in the API surface** — it is part of
the `service` document and of the generated clients, and is not corrected here.

## Configuration validation

Options are bound with `AddOptionsWithValidateOnStart<T>()` + `ValidateDataAnnotations()` for
`Authorization`, `Storage`, `TenantStorage`, `Mcp`, the two `CacheOptions` variants, `Swagger`, and the
three client option sections (`Startup.cs:77-115`). In addition, `Startup.cs:116-156` throws
`ConfigurationException` when `Authorization`, `Storage`, `TenantStorage` or any of the three service
client sections is missing, when `TenantStorage.Provider` is not `mssql|postgres`, or when the
connection-string **name** referenced by `TenantStorage.ConnectionString` does not resolve in
`ConnectionStrings`. `Program.cs:27-35` catches `ConfigurationException`, prints the message and exits
with code `-1` — a misconfigured instance never starts.

## Caching

- `IMemoryCache` is always registered (`Startup.cs:158`); the distributed cache is **Redis**
  (`AddStackExchangeRedisCache`) when `Cache.RedisConfiguration` is non-empty, otherwise an
  in-memory distributed cache (`Startup.cs:160-170`).
- `AddBallwareGenericDistributedCaching()` wires `ITenantConnectionCache` over `IDistributedCache`
  (`Startup.cs:173`); the read-through decorator on the tenant-connection repository is applied when
  `Storage.EnableCaching` is enabled (`src/Ballware.Generic.Data.Ef/Configuration/StorageOptions.cs:7`),
  with expiry from `Cache.CacheExpirationHours`.
- Caching affects **tenant connection lookup**, not query results: business data is never cached by
  this service.

## Background jobs (Quartz)

Quartz is configured from the `Quartz` section (`Startup.cs:251`) and the job infrastructure is
registered by `AddBallwareGenericBackgroundJobs()` (`Startup.cs:252`,
`src/Ballware.Generic.Jobs/ServiceCollectionExtensions.cs:10-22`), which stores the import job durably
under the key `import`/`generic` and starts an in-process scheduler server that waits for jobs to
complete on shutdown. The committed base configuration uses a persistent, **clustered** ADO job
store (`Quartz.Impl.AdoJobStore.JobStoreTX` with `PostgreSQLDelegate`, `quartz.serializer.type: json`,
`quartz.scheduler.instanceId: AUTO`, `src/Ballware.Generic.Service/appsettings.json:32-38`) so several
service instances can share one scheduler; no `quartz.dataSource` is committed, so the job-store
datasource must be supplied through configuration/environment. `appsettings.Development.json` does
**not** override Quartz, so a development run also expects a real job store; only
`appsettings.Swagger.json:42-44` switches to `Quartz.Simpl.RAMJobStore` so that snapshot generation
needs no database.
The only job implemented here is the generic import job — see
[data-integration-and-jobs.md](data-integration-and-jobs.md).

Job **records** (state, result) are not stored by this service: they are created and updated in the
meta service through `IMetadataAdapter.CreateJobForTenantBehalfOfUserAsync` /
`UpdateJobForTenantBehalfOfUserAsync` (`src/Ballware.Generic.Metadata/IMetadataAdapter.cs:25-29`),
while Quartz owns only the trigger/execution.

## CORS

If a `Cors` section is present, a default policy is registered with the configured
origins/methods/headers (`Startup.cs:221-232`) and applied with `app.UseCors()` (`Startup.cs:485`).
Without the section no CORS middleware is configured. The `Cors` section in `appsettings.json` uses
`*` for all three — a development convenience, not a production default.

## Error contract

`app.UseExceptionHandler` installs a terminal handler (`Startup.cs:458-483`): any unhandled exception
is logged (Serilog) and answered with `500` and `application/problem+json`:

```json
{
  "type": "https://httpstatuses.com/500",
  "title": "An unexpected error occurred.",
  "status": 500,
  "detail": "<exception ToString() — development only>",
  "instance": "/generic/<application>/<entity>/query"
}
```

`detail` is only populated in Development (`Startup.cs:473`). Endpoint handlers do **not** catch their
own exceptions, so provider or metadata-client failures surface as this problem document rather than
as a `4xx` — including the export case where an unknown export identifier raises `ArgumentException`
(see [../api/generic-api.md](../api/generic-api.md)).

## Related pages

- [tenancy-and-authorization.md](tenancy-and-authorization.md) — policies, claims, rights, scripts
- [multi-tenancy-and-provisioning.md](multi-tenancy-and-provisioning.md) — providers, master store, schema diffing
- [metadata-driven-execution.md](metadata-driven-execution.md) — how metadata shapes every payload
- [data-integration-and-jobs.md](data-integration-and-jobs.md) — import/export and the job lifecycle
- [mcp-endpoint.md](mcp-endpoint.md) — the non-OpenAPI MCP endpoint
