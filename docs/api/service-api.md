# Service API (document `service`)

3 operations intended for **other platform services** (reporting, ML) rather than for end users.
Snapshot: [../openapi/service.json](../openapi/service.json) (OpenAPI `3.0.4`, 3 paths, 3 component
schemas: `ReportDatasourceDefinition`, `ReportDatasourceTable`, `ReportDatasourceRelation`).

Sources: `src/Ballware.Generic.Api/Endpoints/LookupDataEndpoint.cs:99-116`,
`src/Ballware.Generic.Api/Endpoints/MlModelDataEndpoint.cs:20-30`,
`src/Ballware.Generic.Api/Endpoints/TenantDataEndpoint.cs:21-31`, mounted at `/tenant/lookup`,
`/tenent/mlmodel`, `/tenant/tenant` (`src/Ballware.Generic.Service/Startup.cs:491-492,495`).

The tenant is taken from the **`{tenantId}` path segment**, not from a claim — that is what makes these
routes callable by a service account. All three require the `serviceApi` scope; none of them performs a
tenant membership or entity right check.

> The route `/tenent/mlmodel` contains a typo that is part of the live API surface and of the generated
> clients. It is not corrected here.

## `GET /tenant/lookup/columnvaluebytenantandidforlookup/{tenantId}/{lookupId}/{id}/{column}`

Handler `LookupDataEndpoint.cs:302-327`. Returns **one scalar**: the value of `{column}` from the lookup
row.

- The lookup is resolved by **id** only (there is no by-identifier or with-param variant in this
  document).
- The provider is called with an **empty rights collection** (`ImmutableArray<string>.Empty`, `:319`), so a
  lookup whose SQL depends on `@claims` sees an empty string.
- `{id}` is a string; `{column}` is matched **case-sensitively** against the row keys
  (`:321`). A wrong case, or a lookup whose SELECT does not include that column, yields
  `404` with the body `"Column '<column>' not found in the result."` (`:326`).
- Other `404` bodies: `"Tenant with ID <tenantId> not found."` (`:309`),
  `"Lookup with ID <lookupId> not found for tenant <tenantId>."` (`:314`).
- Response is the bare value, not an object:

```json
"sample value"
```

## `POST /tenent/mlmodel/trainingdatabytenantandplainquery/{tenantId}`

Handler `MlModelDataEndpoint.cs:32-47`. The request body is a **raw JSON string containing SQL** —
`[FromBody] string query` (`:32`), declared as `{"content":{"application/json":{"schema":{"type":"string"}}}}`:

```json
"select uuid as id, name, additional_param from [ballwareschema].<entity> where tenant_id=@tenant_id"
```

- Empty body / empty string → `400 "Query is empty."` (`:41-44`).
- The SQL runs **verbatim** through the tenant provider with `[ballwareschema]` replaced and the tenant
  variable bound (`src/Ballware.Generic.Tenant.Data.Commons/Provider/CommonMlModelProvider.cs:24-33`); no
  metadata lookup, no identifier, no scripts, no parameter dictionary beyond the tenant variable.
- Response: array of rows (untyped in the snapshot), keys = SELECT aliases.
- `404` body: `"Tenant with ID <tenantId> not found."` (`:38`).
- Security note: this endpoint executes **caller-supplied SQL** in the tenant database. It is guarded only
  by the `serviceApi` scope — treat it as a privileged endpoint.

## `GET /tenant/tenant/reportdatasourcesfortenant/{tenantId}`

Handler `TenantDataEndpoint.cs:33-100`. Builds report datasources for a reporting tool.

Response shape (`src/Ballware.Generic.Metadata/ReportDatasource.cs`;
[ReportDatasourceDefinition](schemas.md#reportdatasourcedefinition)):

```json
[
  {
    "Provider": "mssql",
    "Name": "Lookups",
    "ConnectionString": "<tenant connection string>",
    "Tables": [
      { "Name": "<lookup identifier>", "Query": "<lookup ListQuery SQL>", "Relations": [] }
    ]
  },
  {
    "Provider": "mssql",
    "Name": "<datasource name>",
    "ConnectionString": "<tenant connection string>",
    "Tables": [
      {
        "Name": "<table name>",
        "Entity": "<entity identifier>",
        "Query": "<resolved entity SQL>",
        "Relations": [
          { "Name": "<relation name>", "ChildTable": "<child table>", "MasterColumn": "<column>", "ChildColumn": "<column>" }
        ]
      }
    ]
  }
]
```

Behavior:

- The tenant metadata is fetched but **not required** — a missing tenant still produces a payload built
  from the lookups (`:36,75`).
- If the storage provider cannot resolve the tenant's provider (`ArgumentException` from
  `GetProviderAsync`, `src/Ballware.Generic.Tenant.Data/Internal/TenantStorageProviderProxy.cs`), the
  response is `200` with an **empty array** (`:40-47`).
- The synthetic `Lookups` datasource (`:53-71`) contains every lookup **whose `HasParam` is false**
  (`:59`), named by its identifier, with the lookup's `ListQuery` as table query. Its SQL has
  `@tenantId`/`@tenant_id` **and** the deprecated `@claims` replaced **textually**
  (`WithReplaceTenantId().WithReplaceClaims()`, `:62`) — i.e. the returned SQL is no longer parameterized
  and `@claims` becomes `''`.
- Every datasource from tenant metadata gets its `Provider` and `ConnectionString` **overwritten** with the
  tenant's real values (`:77-78`).
- Only tables that declare an `Entity` survive (`:80,94`); each table's `Query` is replaced by the SQL of
  the entity `ListQuery` whose identifier matches the table's own `Query` value, falling back to
  `"primary"` (`:88-90`). Note the latent quirk: when no matching `ListQuery` exists the literal string
  `"primary"` becomes the table's query (`:89`).

> **Security note:** the response embeds the tenant's **database connection string** (including
> credentials) in `ConnectionString` (`:49,57,78`). Only a client that is trusted with tenant credentials
> may call this endpoint.

## Known gaps

- `404` and `400` are not declared for any of the three operations (only `200`/`401`).
- `…/trainingdatabytenantandplainquery` declares its body as a plain JSON string, which is correct but
  easy to mistake for a JSON object.
- `…/columnvalue…` returns a scalar with `Produces<object>` (`LookupDataEndpoint.cs:108`) — untyped.

## Related pages

- [README.md](README.md) · [meta-api.md](meta-api.md) · [schemas.md](schemas.md) · [examples.md](examples.md)
- [../concepts/multi-tenancy-and-provisioning.md](../concepts/multi-tenancy-and-provisioning.md) — provider
  selection and `TenantConnection`
- [../concepts/architecture.md](../concepts/architecture.md) — the client-credentials wiring service
  callers need
