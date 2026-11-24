# Multi-tenancy and provisioning

The service is tenant-aware at three levels: the **master store** (which tenant exists, on which provider,
under which schema, with which credentials), the **tenant database** (one schema and one database user per
tenant), and the **provider selection** performed per request.

`Startup.cs` means `src/Ballware.Generic.Service/Startup.cs`.

## Model: schema-per-tenant with a per-tenant database user

| Object | Where it lives | Shape |
| --- | --- | --- |
| `TenantConnection` | master store | `{ Id, Provider?, Schema?, ConnectionString?, Model? }` (`src/Ballware.Generic.Data/Public/TenantConnection.cs:5-13`) — `Model` holds the last applied **tenant model** JSON, which is the baseline for the next diff |
| `TenantEntity` | master store | `{ Id, Entity?, Model? }` (`src/Ballware.Generic.Data/Public/TenantEntity.cs:5-11`) — one record per provisioned entity table |
| tenant schema + business tables | tenant database | one schema per tenant (default `dbo` / `public`) |
| tenant database user | tenant database | `tenant_<lowercased tenant id>` with a generated password (`SqlServerSchemaProvider.cs:248-249`) |

`TenantConnection.ConnectionString` is the **tenant-scoped** connection (tenant user, tenant schema);
`TenantStorage.ConnectionString` in configuration is only the **name** of a `ConnectionStrings` entry that
points at the tenant master (`Startup.cs:336-348`).

Both stores are EF Core (`src/Ballware.Generic.Data.Ef*/`), using the provider named by
`TenantStorage.Provider`, with automatic migrations only when `Storage.AutoMigrations` is enabled
(`src/Ballware.Generic.Data.Ef/Initialization/InitializationWorker.cs:18-27`, option default `false` in
`src/Ballware.Generic.Data.Ef/Configuration/StorageOptions.cs:5`).

## Provider enablement

| Option section | Properties (defaults) | Effect |
| --- | --- | --- |
| `TenantStorage` | `Provider` `[Required]`, `ConnectionString` `[Required]` (a `ConnectionStrings` key) — `src/Ballware.Generic.Service/Configuration/TenantStorageOptions.cs:5-11` | selects the **master store** provider; validated against `mssql|postgres` (`Startup.cs:122-127`), applied case-insensitively (`Startup.cs:350-357`) |
| `SqlServerTenantStorage` | `Enabled = false`, `UseContainedDatabase = false`, `ConnectionString?` (`src/Ballware.Generic.Tenant.Data.SqlServer/Configuration/SqlServerTenantStorageOptions.cs:3-7`) | registers the SQL Server **tenant data** providers (`Startup.cs:361-378`) |
| `PostgresTenantStorage` | `Enabled = false`, `ConnectionString?` (`src/Ballware.Generic.Tenant.Data.Postgres/Configuration/PostgresTenantStorageOptions.cs:3-6`) | registers the PostgreSQL tenant data providers (`Startup.cs:380-397`) |
| `Storage` | `AutoMigrations = false`, `EnableCaching = false` (`src/Ballware.Generic.Data.Ef/Configuration/StorageOptions.cs:3-6`) | master-store migrations + the read-through tenant-connection cache |

The two tenant-data toggles are **independent**, so one deployment can serve SQL Server tenants and
PostgreSQL tenants simultaneously; neither is required. If an enabled storage section's connection-string
name does not resolve, startup fails with `ConfigurationException` (`Startup.cs:365-375,384-394`).

Registration names the provider implementations under `"mssql"` and `"postgres"`
(`src/Ballware.Generic.Tenant.Data.SqlServer/TenantStorageBuilderExtensions.cs:11,37-43`,
`src/Ballware.Generic.Tenant.Data.Postgres/TenantStorageBuilderExtensions.cs:11,34-40`): storage, generic,
schema, lookup, ML model, statistic and scripting-data providers, each a scoped service.

## Provider selection per request

`ITenantGenericProvider` and friends are proxies that resolve **at call time** from the stored
`TenantConnection.Provider`:

```text
request -> ITenantGenericProvider (TenantGenericProviderProxy)
             -> ConnectionRepository.ByIdAsync(tenantId)
                  -> TenantConnection.Provider
                     -> DefaultProviderConfiguration.GetGenericProvider(providerName)
```

- Missing `TenantConnection` → `ArgumentException "Tenant <guid> does not exist"`
  (`src/Ballware.Generic.Tenant.Data/Internal/TenantStorageProviderProxy.cs:19-43`,
  `src/Ballware.Generic.Tenant.Data.SqlServer/Internal/SqlServerStorageProvider.cs:19-22`) → `500`.
- Provider name not registered (e.g. a `postgres` tenant on a deployment without `PostgresTenantStorage.Enabled`)
  → `KeyNotFoundException "No generic provider found for provider postgres"`
  (`src/Ballware.Generic.Tenant.Data/Internal/DefaultProviderConfiguration.cs:69`).
- When `TenantConnection.Provider` is `null`, the storage proxy falls back to `"mssql"`
  (`TenantStorageProviderProxy.cs:8,28,40`).
- All seven provider families must be registered under the same name, otherwise the corresponding route
  family fails for that tenant.

## `[ballwareschema]`

Every metadata SQL string goes through `ApplyTenantPlaceholderAsync`, which replaces `[ballwareschema]`
with `TenantConnection.Schema` — **always**, independent of the placeholder options
(`src/Ballware.Generic.Tenant.Data.SqlServer/Internal/SqlServerStorageProvider.cs:40-43` → `?? "dbo"`,
`src/Ballware.Generic.Tenant.Data.Postgres/Internal/PostgresStorageProvider.cs:40-43` → `?? "public"`).

That single placeholder is what makes the same entity metadata work against a tenant schema, a tenant DB user
with a restricted `search_path` (`PostgresDbConnectionExtensions.cs:156`), or a different default schema.
Tenant-owned helper objects (views, functions, types) created through the tenant model are reached the same
way — see [../api/schema-api.md](../api/schema-api.md).

## Provisioning: diff-based alignment

Provisioning is driven by the `schema` document (`/api/tenant/...`), whose payload semantics are documented in
[../api/schema-api.md](../api/schema-api.md). What happens in the tenant database:

1. **Tenant creation** (when no `TenantConnection` exists): create the schema and its database user through
   the tenant-master connection, then persist `TenantConnection` with `Provider`, `Schema` and the generated
   connection string (`SqlServerSchemaProvider.cs:239-285`, `PostgresSchemaProvider.cs:250-276`).
   For SQL Server, `SqlServerTenantStorage.UseContainedDatabase` chooses the **contained** user variant
   (`src/Ballware.Generic.Tenant.Data.SqlServer/Internal/SqlServerDbConnectionExtensions.cs:95` vs `:115`).
2. **Tenant model diff**: per object type (`type`, `function`, `table`, `view`), objects missing from the new
   model are dropped and objects that are new, changed (`sql` differs) or flagged `execute: true` are
   (re)created; `statement` objects with `execute: true` are executed verbatim
   (`SqlServerSchemaProvider.cs:100-162,172-208`). The submitted model replaces the stored one
   (`:164-169`).
3. **Entity table alignment** (`createorupdateentityschemafortenant`): create the table if absent, then diff
   **columns** — added columns are `ADD`ed, missing ones `DROP`ed, changed ones `ALTER`ed — with mandatory
   columns protected from removal (`SqlServerDbConnectionExtensions.cs:257-305`,
   `PostgresDbConnectionExtensions.cs:285-320`), and indexes are rebuilt.
4. **Drop**: `dropentityschemafortenant` drops the table and the `TenantEntity` record; `dropschemafortenant`
   drops the schema, its custom objects and the user/login (`SqlServerDbConnectionExtensions.cs:139-190`) and
   removes the `TenantConnection`.

### Implicit columns of an entity table

Entity tables are created with a fixed base column set; the serialized model only supplies the **custom**
columns (`SqlServerDbConnectionExtensions.cs:44-62`, `PostgresDbConnectionExtensions.cs:58-76`):

| SQL Server | PostgreSQL |
| --- | --- |
| `Id bigint identity primary key` | `id bigserial primary key` |
| `Uuid uniqueidentifier not null` (unless `noIdentity`) | `uuid uuid not null` (unless `noIdentity`) |
| `TenantId uniqueidentifier not null` | `tenant_id uuid not null` |
| `CreatorId uniqueidentifier`, `CreateStamp datetime` | `creator_id uuid`, `create_stamp timestamp` |
| `LastChangerId uniqueidentifier`, `LastChangeStamp datetime` | `last_changer_id uuid`, `last_change_stamp timestamp` |

Standard indexes are created automatically: non-unique on the tenant column, and unique on
`(Uuid, TenantId)` / `(uuid, tenant_id)` unless `noIdentity`
(`SqlServerDbConnectionExtensions.cs:286-299`, `PostgresDbConnectionExtensions.cs:311-320`).
The mandatory-column lists (`SqlServerDbConnectionExtensions.cs:17`, `PostgresDbConnectionExtensions.cs:19`)
are what the column diff never removes.

Identifier safety is provider-specific: PostgreSQL validates schema/user/table/column/function/index names
against `^[a-zA-Z_][a-zA-Z0-9_]*$` and a 63-character limit before emitting DDL
(`src/Ballware.Generic.Tenant.Data.Postgres/Internal/PostgresValidator.cs:5-31`); the SQL Server
implementation relies on bracket quoting instead.

## Provider differences visible to API callers

Both providers implement the same contracts, but their vocabulary appears in payloads and in the SQL text of
metadata. Details and test evidence:
[metadata-driven-execution.md](metadata-driven-execution.md#provider-differences-visible-in-payloads).

| Aspect | `mssql` | `postgres` |
| --- | --- | --- |
| item id member | `Id` | `id` |
| tenant SQL variable | `@tenantId` | `@tenant_id` |
| default schema | `dbo` | `public` |
| column/alias casing in rows | as aliased (typically PascalCase) | as aliased (typically lower case) |
| upsert idiom in test metadata | `UPDATE … ; if @@ROWCOUNT = 0 begin insert … end` | `INSERT … ON CONFLICT (…) DO UPDATE SET …` |
| raw-table script host functions | supported | not supported (`NotSupportedException`) |
| user creation variant | login or contained user (`UseContainedDatabase`) | role with `search_path` set |

## Caching of tenant connections

`Storage.EnableCaching` swaps the tenant-connection repository for a read-through decorator
(`src/Ballware.Generic.Data.Caching/CachableTenantConnectionRepository.cs:21-34`) backed by
`IDistributedCache` — Redis when `Cache.RedisConfiguration` is set, otherwise in-memory
(`Startup.cs:160-171`). Entries expire absolutely after `Cache.CacheExpirationHours`
(`src/Ballware.Generic.Caching/Internal/DistributedTenantConnectionCache.cs:45-54`). `SaveAsync` updates the
entry; `RemoveAsync` purges it **only** when the remove parameters contain a parseable `Id`
(`CachableTenantConnectionRepository.cs:66-85`). Metadata, query results, jobs and files are never cached.

Consequence for provisioning: after changing a tenant's connection through `dropschemafortenant` or a
tenant-model update, a stale cache entry can keep the old `TenantConnection` alive for up to
`CacheExpirationHours`.

## Related pages

- [../api/schema-api.md](../api/schema-api.md) — the four provisioning operations
- [architecture.md](architecture.md) — the three data layers
- [metadata-driven-execution.md](metadata-driven-execution.md) — placeholders and provider asymmetries
- [../api/service-api.md](../api/service-api.md) — the endpoint that exposes the tenant connection string
