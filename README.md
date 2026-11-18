# ballware-generic-service

**ballware Generic Service** is the generic data engine of the ballware platform. It exposes a metadata-driven REST API and an MCP (Model Context Protocol) endpoint that let applications and AI agents query, edit, import, export, and manage tenant-owned business data in a multi-tenant database — without any application-specific code.

Entities, queries, lookups, statistics, state machines, and rights are described as **metadata** (provided by the external ballware **meta service**). This service executes that metadata against **per-tenant database schemas** on SQL Server or PostgreSQL, applies JavaScript-based business logic via [Jint](https://github.com/sebastienros/jint), and enforces scope- and right-based authorization from OIDC/JWT claims.

- Target framework: **.NET 10** (`net10.0`) across all projects
- License: **MIT** (ballware Software & Consulting)

---

## Table of Contents

- [Architecture](#architecture)
- [Solution Structure](#solution-structure)
- [Features](#features)
  - [Generic Data API](#generic-data-api)
  - [Service & Schema APIs](#service--schema-apis)
  - [Multi-Tenancy Model](#multi-tenancy-model)
  - [Authentication & Authorization](#authentication--authorization)
  - [MCP Endpoint for AI Agents](#mcp-endpoint-for-ai-agents)
  - [Scripting & Business Logic](#scripting--business-logic)
  - [Background Jobs & Import/Export](#background-jobs--importexport)
  - [Caching](#caching)
- [Configuration](#configuration)
- [Technology Stack](#technology-stack)
- [Build, Run & Test](#build-run--test)
- [CI/CD & Versioning](#cicd--versioning)

---

## Architecture

The service is a stateless ASP.NET Core (Minimal API) host. It combines three data layers:

1. **Master store** — a tenant registry (tenant connections and persisted entity models) managed with **Entity Framework Core** (SQL Server or PostgreSQL provider, automatic migrations).
2. **Tenant storage** — per-tenant business data in dedicated database schemas (schema-per-tenant, one database user per tenant), accessed through **Dapper** with provider-specific implementations for SQL Server and PostgreSQL that can run side by side.
3. **External platform services** — the **meta service** (entity/metadata model), the **storage service** (temporary import/export files), and the **ML service** (model predictions). These are called via typed HTTP clients using OAuth2 client-credentials.

```
                       ┌────────────────────────────────────────────────────┐
                       │              Ballware.Generic.Service              │
                       │                                                    │
 OIDC / JWT ───────►   │  Minimal API endpoints  ·  MCP endpoint            │
 (user + service       │  (auth policies: metaApi / serviceApi /            │
  clients)             │   schemaApi / genericApi)                          │
                       │        │                        │                  │
                       │        ▼                        ▼                  │
                       │  IMetadataAdapter          ITenant*Provider        │
                       │  IMlAdapter                (proxy → registry)      │
                       │  IFileStorageAdapter         │        │            │
                       └────────┼───────────┬─────────┼────────┼────────────┘
                                ▼           ▼         ▼        ▼
                        meta service   storage     EF Core   Dapper
                        (metadata)     service    master     tenant schemas
                                       (files)    store      (mssql / postgres)
                                                    │
                                                 Quartz clustered job store
```

Key runtime flows:

- **Request handling**: endpoints resolve the tenant and entity metadata from the caller's claims, verify tenant/entity rights (`ITenantRightsChecker`, `IEntityRightsChecker`), then delegate to the tenant provider selected by `Tenant.Provider` (`mssql` or `postgres`).
- **Metadata SQL**: all metadata-defined statements use the `[ballwareschema]` placeholder (replaced by the tenant's schema) plus optional `@tenantId` and `@claims` substitutions.
- **Schema management**: the schema API provisions a tenant database user (`tenant_<guid>` with generated password), creates/updates/drops the tenant schema, and diffs stored vs. new tenant models (types, functions, tables, views, columns, indexes) before executing DDL. Previous models are persisted as JSON in the master store.

---

## Solution Structure

| Project | Responsibility |
|---|---|
| `Ballware.Generic.Service` | Executable host (`Program.cs`, `Startup.cs`): DI wiring, auth, Quartz, Mapster profiles, service clients, MCP endpoint, Swagger, Dockerfile |
| `Ballware.Generic.Api` | Minimal-API endpoint layer (generic data, lookup, ML model data, processing state, statistics, tenant data, tenant schema) |
| `Ballware.Generic.Metadata` | Provider-neutral metadata model: `Tenant`, `Entity` (queries, scripts, custom functions, state), `Lookup`, `Statistic`, `ProcessingState`, `ReportDatasource`, `Export`, `Job`; `IMetadataAdapter` / `IMlAdapter` contracts |
| `Ballware.Generic.Data` | Provider-agnostic data contracts: `TenantConnection`, `TenantEntity`, repository interfaces |
| `Ballware.Generic.Data.Ef` | EF Core abstractions for the master store (model customizer, base repositories, `StorageOptions`, auto-migration worker) |
| `Ballware.Generic.Data.Ef.SqlServer` / `.Postgres` | EF Core master-store implementations (Npgsql snake-case naming, migrations) |
| `Ballware.Generic.Caching` | `ITenantConnectionCache` over `IDistributedCache` |
| `Ballware.Generic.Data.Caching` | Read-through caching decorator for `ITenantConnectionRepository` |
| `Ballware.Generic.Tenant.Data` | Tenant provider abstraction: provider interfaces, `TenantStorageBuilder`, provider registry/proxies, placeholder options |
| `Ballware.Generic.Tenant.Data.Commons` | DB-agnostic provider logic (generic CRUD/query/import/export pipelines, lookup/statistic/ML providers, CSV handling) |
| `Ballware.Generic.Tenant.Data.SqlServer` / `.Postgres` | Dapper-based tenant providers: storage, generic, lookup, statistic, ML model, schema (DDL, model diffing), scripting data provider |
| `Ballware.Generic.Scripting` | Scripting contracts (entity lifecycle hooks, statistic executor, scripting user context/data provider) |
| `Ballware.Generic.Scripting.Jint` | JavaScript (Jint) implementation of the scripting contracts with DB/claims/ML host functions |
| `Ballware.Generic.Jobs` | Quartz background job infrastructure (`GenericImportJob`) |
| `Ballware.Generic.Mcp` | Dynamic MCP tool registration generated from entity metadata |
| `Ballware.Generic.Service.Client` / `Ballware.Generic.Schema.Client` | NSwag-generated HTTP clients (generated in CI from the `service`/`schema` Swagger documents) |

Tests (`test/`): `Ballware.Generic.Api.Tests`, `Ballware.Generic.Caching.Tests`, `Ballware.Generic.Data.Ef.SqlServer.Tests`, `Ballware.Generic.Data.Ef.Postgres.Tests`, `Ballware.Generic.Jobs.Tests`, `Ballware.Generic.Tenant.Data.SqlServer.Tests`, `Ballware.Generic.Tenant.Data.Postgres.Tests`.

---

## Features

### Generic Data API

Mounted at `/generic` (authorization policy `genericApi`). Operates on metadata-defined entities addressed by `{application}/{entity}`:

| Method | Route | Description |
|---|---|---|
| GET | `/generic/{application}/{entity}/all` | Query all items (`identifier` defaults to `primary`, right `view`) |
| GET | `/generic/{application}/{entity}/query` | Query items by query identifier and parameters |
| GET | `/generic/{application}/{entity}/count` | Count items for a query → `{ "count": n }` |
| GET | `/generic/{application}/{entity}/new` | New-item template (right `add`) |
| GET | `/generic/{application}/{entity}/newquery` | New-item template with parameters |
| GET | `/generic/{application}/{entity}/byid?id=` | Single item by id |
| POST | `/generic/{application}/{entity}/save` | Save item (right `edit` for the primary identifier) |
| POST | `/generic/{application}/{entity}/savebatch` | Save a list of items with per-item rights checks |
| DELETE | `/generic/{application}/{entity}/remove/{id}` | Delete item (right `delete`; provider preliminary check) |
| POST | `/generic/{application}/{entity}/import` | Upload a file, register a job, trigger the Quartz import job |
| GET | `/generic/{application}/{entity}/export` | Export query results inline (e.g. CSV media type) |
| POST | `/generic/{application}/{entity}/exporturl` | Export to a temporary file and return a download reference |
| GET | `/generic/{application}/{entity}/download/{tenantId}/{id}` | Anonymous download of a generated export (expires after 1 day) |

### Service & Schema APIs

Mounted in Startup: `/tenant/lookup` (lookup user + service), `/tenent/mlmodel` (service), `/tenant/processingstate` (meta), `/tenant/statistic` (meta), `/tenant/tenant` (service), `/api/tenant` (schema), `/generic/mcp` (MCP).

- **Lookup API** — select lists, single entries (by id or identifier, with/without lookup parameter), and autocomplete strings for tenant lookups (`metaApi`); plus a service endpoint returning a single column value for an explicit tenant (`serviceApi`).
- **Processing-state API** — state-machine support for entities with a state column: allowed successor states for one or many instances, and `isstateallowed` checks (`metaApi`).
- **Statistic API** — executes metadata-defined statistics (`FetchSql` plus optional `FetchScript`) and returns data rows (`metaApi`).
- **ML model API** — `POST /tenent/mlmodel/trainingdatabytenantandplainquery/{tenantId}` executes a plain SQL query and returns rows as training data for the ML service (`serviceApi`).
- **Tenant data API** — `GET /tenant/tenant/reportdatasourcesfortenant/{tenantId}` resolves report datasource definitions (provider, connection string, tables, relations) for a reporting service (`serviceApi`).
- **Tenant schema API** (`schemaApi`, `/api/tenant`):
  - `POST /createorupdateentityschemafortenant/{tenantId}` — create/update one entity table from a serialized entity model
  - `DELETE /dropentityschemafortenant/{tenantId}/{identifier}` — drop one entity table
  - `POST /createorupdateschemafortenant/{tenantId}` — create/align the complete tenant schema (diff-based: types, functions, tables, views, ad-hoc statements)
  - `DELETE /dropschemafortenant/{tenantId}` — drop tenant schema, tenant user, and the tenant connection record

Three Swagger documents are published — **ballware Generic API** (`generic`), **ballware Service API** (`service`), **ballware Schema API** (`schema`) — with an OIDC security scheme (`.well-known/openid-configuration`) and an optional SwaggerUI with OAuth/PKCE (`Swagger.EnableClient`).

### Multi-Tenancy Model

- **Schema-per-tenant** with a dedicated database user per tenant (`tenant_<guid>`; password generated at provisioning), default schema `dbo` (SQL Server) or `public` (PostgreSQL). SQL Server additionally supports **contained databases** (`UseContainedDatabase`) so tenant users can be created without a server-level login.
- `TenantStorage.Provider` selects the provider for the **master store**; `SqlServerTenantStorage.Enabled` and `PostgresTenantStorage.Enabled` enable the two **tenant data storages independently**, so both providers can serve tenants in parallel. Providers are selected per tenant from the stored `TenantConnection.Provider`.
- Tenant connections (schema, provider, connection string, last-applied tenant model JSON) are persisted in the EF-managed master store; each entity table model is persisted in `TenantEntity`.
- Schema provisioning aligns the physical database with the tenant model by **diffing** the stored model against the new model (add/alter/drop columns, indexes, views, functions, types).
- Provisioning and tenant-scoped queries are executed against the tenant master connection string (validated at startup).

### Authentication & Authorization

- **User/service authentication** via JWT Bearer against an OIDC authority (`Authorization.Authority`), with claim-type mapping disabled.
- **Scope policies**: `metaApi`, `serviceApi`, `schemaApi`, `genericApi` — each checks the configured `scope` claim (`Authorization.RequiredMetaScope`, `RequiredServiceScope`, `RequiredSchemaScope`).
- **Tenant/right resolution** via configurable claims: tenant claim (`tenant`), user id claim (`sub`), right claim (`right`) — handled by `Ballware.Shared.Authorization`.
- **Fine-grained rights**: tenant- and entity-level rights checks, optionally extended by JavaScript scripts (`RightsCheckScript`, `StateAllowedScript`) through `Ballware.Shared.Authorization.Jint`.
- **Service-to-service calls** (meta/storage/ML) use client-credentials token management (`Duende.AccessTokenManagement`).

### MCP Endpoint for AI Agents

Mounted at `/generic/mcp` when `Mcp.Enabled` (authentication with user JWT, scope from `Mcp.RequiredMcpScope`).

Tools are **generated dynamically per authenticated user** from metadata: for every tenant of the user and every entity, each query with `AiEnabled = true` becomes a read-only MCP tool:

- `generic_{tenantId}_{entity}_query_{queryIdentifier}` — parameterized list query
- `generic_{tenantId}_{entity}_byid_{queryIdentifier}` — by-id query (requires an `id` GUID parameter)

Tool descriptions, typed parameters, and a structured output schema (`{ "results": [...] }`, generated with NJsonSchema) are derived from the query metadata. Execution re-checks the caller's tenant membership and `view` rights before running the query, so AI agents can only reach metadata-whitelisted, rights-approved data.

### Scripting & Business Logic

JavaScript (Jint) hooks are defined on the metadata and executed around data operations:

- **Entity lifecycle scripts**: `ListScript`, `ByIdScript`, `BeforeSaveScript`, `SaveScript`, `RemovePreliminaryCheckScript`, `RemoveScript`, `StateAllowedScript` — computed columns, validation, cascading side effects, state-machine permission rules. Tenant-wide helper libraries via `Tenant.ServerScriptDefinitions`.
- **Statistic post-processing** (`FetchScript`) — enriches statistic rows, including `mlPredict(model, input)` calls against the ML service.
- **Host functions** available in scripts: JSON (`parse`/`stringify`), claims (`getClaim`, `hasClaim`, `hasAnyClaim`), entity operations (`entityQueryList`, `entitySave`, `entityRemove`, …), raw SQL (`getList`, `dbInsert`, `dbUpdate`, `dbDelete`, …), `mlPredict`, `log`, `tenantId`.

### Background Jobs & Import/Export

- **Quartz** is embedded with a clustered, persistent `JobStoreTX` (PostgreSQL delegate, JSON serialization) — safe for multi-instance deployments.
- **`GenericImportJob`** (job `import` / group `generic`) is triggered by `POST /generic/.../import`: it streams an uploaded CSV file (CsvHelper, configurable format/delimiter) through the tenant provider's import pipeline with a per-row authorization callback, tracks job state (`Queued`/`InProgress`/`Finished`/`Error`) in the meta service, and cleans up the temporary file.
- Import/export files are exchanged through the **storage service** (temporary storage).

### Caching

- Tenant connections are cached via `ITenantConnectionCache` over `IDistributedCache` (read-through decorator on the repository) when `Storage.EnableCaching` is enabled; expiry configured by `Cache.CacheExpirationHours`.
- Distributed cache backend: **Redis** (`StackExchangeRedis`) when `Cache.RedisConfiguration` is set, otherwise an in-memory distributed cache.

---

## Configuration

Configuration sources (in order): `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.local.json` → **environment variables** (double underscore = hierarchy, e.g. `Authorization__Authority`). Options are bound with `ValidateOnStart` + DataAnnotations; invalid configuration aborts startup. Secrets should be supplied via environment variables or secret stores — never committed.

| Section | Purpose / key settings |
|---|---|
| `Authorization` | `Authority`, `Audience` (required); `TenantClaim` (`tenant`), `UserIdClaim` (`sub`), `RightClaim` (`right`); `RequireHttpsMetadata`; `RequiredMetaScope` (`metaApi`), `RequiredServiceScope` (`serviceApi`), `RequiredSchemaScope` (`schemaApi`) |
| `Storage` | `AutoMigrations` (run EF migrations at startup), `EnableCaching` |
| `TenantStorage` | `Provider` (`mssql` \| `postgres`, required), `ConnectionString` — **name** of a `ConnectionStrings` entry pointing at the tenant master database |
| `SqlServerTenantStorage` | `Enabled`, `UseContainedDatabase`, `ConnectionString` (name of a `ConnectionStrings` entry) |
| `PostgresTenantStorage` | `Enabled`, `ConnectionString` (name of a `ConnectionStrings` entry) |
| `Cache` | `RedisConfiguration` (empty → in-memory), `RedisInstanceName`, `CacheExpirationHours` |
| `Mcp` | `Enabled`, `RequiredMcpScope` (defaults to `RequiredMetaScope`), `ResourceUri` |
| `Swagger` | `EnableClient`, `ClientId`, `ClientSecret`, `RequiredScopes` |
| `MetaClient`, `StorageClient`, `MlClient` | `ServiceUrl`, `TokenEndpoint`, `ClientId`, `ClientSecret`, `Scopes` (all required) — clients for the meta, storage, and ML services |
| `ConnectionStrings` | Entries referenced by `TenantStorage` / tenant storage options / Quartz (e.g. `TenantPostgresMasterConnection`, `QuartzConnection`) |
| `Quartz` | Scheduler, clustered job store, and datasource settings (PostgreSQL by default) |
| `Cors` | Optional: `AllowedOrigins`, `AllowedMethods`, `AllowedHeaders` |
| `Kestrel` | Server options, e.g. `Limits.MaxRequestBodySize` (default 40 MB) |
| `Serilog` | Structured console logging, level overrides per category |

Required at startup: `Authorization`, `Storage`, `TenantStorage` (with valid provider and resolvable connection string), and all three service clients (`MetaClient`, `StorageClient`, `MlClient`).

---

## Technology Stack

| Area | Technology |
|---|---|
| Runtime | ASP.NET Core on .NET 10, Minimal APIs, Kestrel |
| Master store ORM | Entity Framework Core 10 (SQL Server `Microsoft.EntityFrameworkCore.SqlServer`, `Npgsql.EntityFrameworkCore.PostgreSQL` with snake-case naming) |
| Tenant data access | Dapper with `Microsoft.Data.SqlClient` and `Npgsql` |
| Scheduling | Quartz.NET 3.8 (`Quartz.AspNetCore`, clustered AdoJobStore, JSON serialization) |
| Scripting | Jint 4 (JavaScript interpreter) |
| Auth | JWT Bearer / OIDC, `Duende.AccessTokenManagement` (client credentials), scope policies, script-based rights checks |
| MCP | `Ballware.Shared.Mcp*` packages + NJsonSchema for tool schemas |
| Mapping/serialization | Mapster, Newtonsoft.Json |
| API docs | Swashbuckle.AspNetCore (3 documents), NSwag (generated service clients) |
| Logging | Serilog (console sink, request logging) |
| Caching | `IDistributedCache` — Redis (StackExchangeRedis) or in-memory |
| Import/export | CsvHelper; temporary files via the ballware storage service |
| ballware platform packages | `Ballware.Shared.Data[.Ef]`, `Ballware.Shared.Authorization[.Jint]`, `Ballware.Shared.Mcp[.Authorization, .Endpoints]`, `ballware.meta.service.client`, `ballware.storage.service.client`, `Ballware.Ml.Service.Client` (private GitHub NuGet feed) |

---

## Build, Run & Test

**Prerequisites**: .NET 10 SDK and access to the private `ballware` GitHub NuGet feed (restore source `https://nuget.pkg.github.com/ballware/index.json`) for the `Ballware.Shared.*` and service-client packages.

```bash
dotnet nuget add source "https://nuget.pkg.github.com/ballware/index.json" \
  --name github --username <user> --password <token>

dotnet restore ballware-generic-service.sln
dotnet build ballware-generic-service.sln -c Release
dotnet run --project src/Ballware.Generic.Service
```

Provide configuration through environment variables (see [Configuration](#configuration)). A development certificate lives in `certificate/` (dev only).

**Docker** (multi-arch `linux/amd64`, `linux/arm64`; requires the private feed credentials as build args):

```bash
docker build -t ballware-generic-service \
  --build-arg GITHUB_USERNAME=<user> --build-arg GITHUB_PAT=<token> \
  -f src/Ballware.Generic.Service/Dockerfile src/
```

Published images: `ghcr.io/ballware/ballware-generic-service` (tags: GitVersion SemVer, plus `stable` for `main`, `latest` for other branches).

**Tests** — NUnit 4 + Moq, coverage via coverlet; integration tests for EF and tenant providers run against **Testcontainers** (PostgreSQL and MS SQL) and expect the tenant database password via `TestSettings:DatabasePassword` (config or `TestSettings__DatabasePassword`):

```bash
dotnet test ballware-generic-service.sln -c Release
```

---

## CI/CD & Versioning

- **`publish-packages.yml`** (push to `main`/`develop`): GitVersion → build → Swagger export (`service`, `schema` documents) → NSwag client generation → `dotnet pack` → push NuGet packages to the ballware GitHub feed → `v<semver>` tag on `main` → multi-arch Docker image build/push to GitHub Container Registry.
- **`sonarqube-latest.yml`** (push to `main`, `develop`, `feature/*`, `bugfix/*`): SonarQube analysis with OpenCover test coverage (integration test password injected as a secret).
- **Branching/versioning** (GitVersion, `ContinuousDeployment`): `main` → patch increments, `develop`/`feature/*` → minor increments, `bugfix/*` → patch increments; `+semver:` commit-message directives supported.

---

## License

MIT — see [LICENSE](LICENSE).
