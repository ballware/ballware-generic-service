# Schema API (document `schema`)

4 operations that create, align and drop tenant database objects. This is the **provisioning** surface,
used by tooling that owns tenant/entity models — not by application callers.

Snapshot: [../openapi/schema.json](../openapi/schema.json) (OpenAPI `3.0.4`, 4 paths, 2 component schemas:
`EntitySchema`, `TenantSchema`).

Source: `src/Ballware.Generic.Api/Endpoints/TenantSchemaEndpoint.cs` (routes `:18-57`, handlers `:59-85`),
mounted at `/api/tenant` (`src/Ballware.Generic.Service/Startup.cs:498`). All four require the `schemaApi`
scope.

**No metadata lookup and no rights check happen here.** All four handlers delegate straight to
`ITenantSchemaProvider` and return `200` with an **empty body**. A failed DDL statement surfaces as
`500 application/problem+json` (`src/Ballware.Generic.Service/Startup.cs:458-483`) — not as `4xx`.

| Verb + path | Body | Extra parameter |
| --- | --- | --- |
| `POST /api/tenant/createorupdateentityschemafortenant/{tenantId}` | `EntitySchema` | — |
| `DELETE /api/tenant/dropentityschemafortenant/{tenantId}/{identifier}` | — | optional `userId` **query** parameter |
| `POST /api/tenant/createorupdateschemafortenant/{tenantId}` | `TenantSchema` | — |
| `DELETE /api/tenant/dropschemafortenant/{tenantId}` | — | optional `userId` **query** parameter |

## `POST /api/tenant/createorupdateentityschemafortenant/{tenantId}`

`TenantSchemaEndpoint.cs:59-64`. Creates or updates the **table of one entity** in the tenant database and
records the model in the master store.

Body ([EntitySchema](schemas.md#entityschema)):

```json
{
  "UserId": "<guid>",
  "SerializedEntityModel": "<provider table model, JSON-encoded string>"
}
```

`SerializedEntityModel` is a **string containing JSON**, deserialized with camelCase naming
(`src/Ballware.Generic.Tenant.Data.SqlServer/Internal/SqlServerSchemaProvider.cs:36-39`,
`src/Ballware.Generic.Tenant.Data.Postgres/Internal/PostgresSchemaProvider.cs:36-39`). Its shape is
provider-specific (`SqlServerModels.cs:108-122` / `PostgresModels.cs:110-115`):

```json
{
  "tableName": "<table name>",
  "noIdentity": false,
  "customColumns": [
    { "columnName": "name", "columnType": "nvarchar", "maxLength": 100, "nullable": true },
    { "columnName": "amount", "columnType": "float", "nullable": false }
  ],
  "customIndexes": [
    { "indexName": "ix_<table>_name", "unique": false, "columnNames": ["name"] }
  ]
}
```

Column type literals are provider vocabulary — e.g. `bigint`, `uniqueidentifier`, `bit`, `int`, `float`,
`date`, `time`, `datetime`, `nvarchar`, `text` (`SqlServerModels.cs:33-43`) with an `unknown` fallback for
anything unrecognized (`:30`), plus custom literals.

Effect (`SqlServerSchemaProvider.cs:28-55`):

1. Look up the stored `TenantConnection` of the tenant; **nothing happens if it does not exist** (`:32`).
2. Deserialize the model, then find or create the `TenantEntity` record for the table name in the master
   store (`:41-47`).
3. Apply the table DDL in the tenant schema (`:49`).
4. Store the serialized model on the `TenantEntity` record (`:51-53`).

## `DELETE /api/tenant/dropentityschemafortenant/{tenantId}/{identifier}`

`TenantSchemaEndpoint.cs:66-71`. `{identifier}` here is the **table/entity name**, not a query identifier
(`SqlServerSchemaProvider.cs:57-77`).

- Drops the table in the tenant schema (`:67`) and removes the `TenantEntity` record when one exists
  (`:69-75`).
- `userId` is an optional query `Guid` (`TenantSchemaEndpoint.cs:66`) used only as the auditing user for
  the master-store write.
- If the tenant has no stored connection, the route still returns `200` (`:31-32` gating).

## `POST /api/tenant/createorupdateschemafortenant/{tenantId}`

`TenantSchemaEndpoint.cs:73-78`. Creates the tenant (schema + database user) if it does not exist, then
**aligns the tenant database by diffing** the serialized tenant model against the model stored in
`TenantConnection.Model`.

Body ([TenantSchema](schemas.md#tenantschema)):

```json
{
  "UserId": "<guid>",
  "Provider": "mssql",
  "SerializedTenantModel": "<provider tenant model, JSON-encoded string>"
}
```

Tenant model shape (`SqlServerModels.cs:124-160`, `PostgresModels.cs:125-158`):

```json
{
  "server": "<optional server override>",
  "catalog": "<optional database override>",
  "schema": "<optional schema override>",
  "databaseObjects": [
    { "name": "<object name>", "type": "table", "sql": "<create statement>", "execute": true },
    { "name": "<view name>", "type": "view", "sql": "<create statement>", "execute": true },
    { "name": "<function name>", "type": "function", "sql": "<create statement>", "execute": true },
    { "name": "<type name>", "type": "type", "sql": "<create statement>", "execute": true },
    { "name": "<one-off script>", "type": "statement", "sql": "<statement>", "execute": true }
  ]
}
```

`type` values are `unknown | table | view | function | type | statement`
(`SqlServerModels.cs:124-139`, `PostgresModels.cs:125-140`). `execute` gates creation/re-execution
(`SqlServerSchemaProvider.cs:200-206`).

Diff semantics (`SqlServerSchemaProvider.cs:100-162`):

- Per object type, objects present in the stored model but absent from the new model are **dropped**
  (`GetDroppedItems`, `:172-188`).
- Objects that are new, whose `sql` differs, or that carry `execute: true` are **(re)created**
  (`GetAddedOrChangedItems`, `:190-208`) — so `execute: true` is the way to re-run a statement.
- `type` objects are handled first, then `function`, then `table`, then `view`; `statement` objects with
  `execute: true` are executed as-is (`:104-162`). The PostgreSQL provider uses the same diff helpers with
  its own object order (`PostgresSchemaProvider.cs:100-160`).
- The new model is then written back to `TenantConnection.Model` (`:164-169`) — the stored model **is** the
  last submitted model, which is what the next diff compares against.

Tenant creation (`SqlServerSchemaProvider.cs:239-285`), when no connection record exists:

- a per-tenant database **user** named `tenant_<lowercased tenant id>` with a generated password (`:248-249`),
- schema/user creation through the tenant-master connection (`:266-273`; `UseContainedDatabase` selects the
  contained-user variant for SQL Server, `src/Ballware.Generic.Tenant.Data.SqlServer/Configuration/SqlServerTenantStorageOptions.cs:6`),
- `TenantConnection` persisted with `Provider = "mssql"` (or `"postgres"`, `PostgresSchemaProvider.cs:273`),
  the chosen `Schema`, and the built connection string (`:278-282`).

See [../concepts/multi-tenancy-and-provisioning.md](../concepts/multi-tenancy-and-provisioning.md).

## `DELETE /api/tenant/dropschemafortenant/{tenantId}`

`TenantSchemaEndpoint.cs:80-85`. Drops the tenant's schema and database user and removes the
`TenantConnection` record (`SqlServerSchemaProvider.cs:211-237`, `PostgresSchemaProvider.cs:212-237`).
It reconnects to the tenant database **with the tenant-master credentials** (`:217-228`) because the tenant
user is the object being removed. **This deletes tenant data.**

## Known gaps

- `404` is never produced by these routes: a missing `TenantConnection` is silently ignored (the SQL Server
  provider returns without action, `SqlServerSchemaProvider.cs:32,61,215`), while the **proxy** layer throws
  `KeyNotFoundException`/`ArgumentException` for unknown providers — which becomes `500`.
- `200` with an empty body tells the caller nothing about what was created, changed or dropped.
- The request body of the two POST routes is required, but a **malformed** `SerializedEntityModel` /
  `SerializedTenantModel` is only detected during deserialization in the provider
  (`:36-39`, `:81-84`) and surfaces as `500`.

## Related pages

- [README.md](README.md) · [schemas.md](schemas.md) · [examples.md](examples.md)
- [../concepts/multi-tenancy-and-provisioning.md](../concepts/multi-tenancy-and-provisioning.md)
- [../concepts/architecture.md](../concepts/architecture.md) — the master store layer
