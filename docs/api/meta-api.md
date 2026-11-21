# Meta API (document `meta`)

12 operations added by the fourth Swagger document: lookup data for the calling user (8), processing
state (3), statistics (1). Snapshot: [../openapi/meta.json](../openapi/meta.json) (OpenAPI `3.0.4`,
12 paths, 1 component schema: `ProcessingStateSelectListEntry`).

Sources: `src/Ballware.Generic.Api/Endpoints/LookupDataEndpoint.cs` (`:24-97`),
`src/Ballware.Generic.Api/Endpoints/ProcessingStateDataEndpoint.cs` (`:26-55`),
`src/Ballware.Generic.Api/Endpoints/StatisticDataEndpoint.cs` (`:20-30`). Mounted at `/tenant/lookup`,
`/tenant/processingstate`, `/tenant/statistic` (`src/Ballware.Generic.Service/Startup.cs:490,493-494`).

> **No tenant/entity right check.** These routes require the `metaApi` **scope** policy and resolve
> metadata, but they never call `ITenantRightsChecker` / `IEntityRightsChecker`. The user's **rights**
> are handed to the provider as an SQL value instead (see below). Do not read a `200` here as
> "authorized for this entity".

## Lookup operations (8)

Lookup metadata is `{ Identifier, ListQuery, ByIdQuery?, HasParam }`
(`src/Ballware.Generic.Metadata/Lookup.cs`). `{lookupId}` is a `Guid`, `{identifier}` is the lookup
identifier string, and `{id}` is a **string** (not a `Guid`) — whatever the lookup's key column holds.

| Verb + path | Row source | Response |
| --- | --- | --- |
| `GET /tenant/lookup/selectlistforlookup/{lookupId}` | `lookup.ListQuery` | array of rows |
| `GET /tenant/lookup/selectbyidforlookup/{lookupId}/{id}` | `lookup.ByIdQuery` | single row (or `null`) |
| `GET /tenant/lookup/selectlistforlookupidentifier/{identifier}` | `lookup.ListQuery` | array of rows |
| `GET /tenant/lookup/selectbyidforlookupidentifier/{identifier}/{id}` | `lookup.ByIdQuery` | single row (or `null`) |
| `GET /tenant/lookup/selectlistforlookupwithparam/{lookupId}/{param}` | `lookup.ListQuery` with `@param` | array of rows |
| `GET /tenant/lookup/selectbyidforlookupwithparam/{lookupId}/{param}/{id}` | `lookup.ByIdQuery` with `@param` and `@id` | single row (or `null`) |
| `GET /tenant/lookup/autocompleteforlookup/{lookupId}` | `lookup.ListQuery` read as `string` | array of strings |
| `GET /tenant/lookup/autocompleteforlookupwithparam/{lookupId}/{param}` | `lookup.ListQuery` read as `string` | array of strings |

Payload notes:

- Rows are Dapper dynamics: keys are the SELECT aliases, untyped `array`/`{}` in OpenAPI.
- **Autocomplete reuses `ListQuery`** and reads it as a single `string` column
  (`src/Ballware.Generic.Tenant.Data.Commons/Provider/CommonLookupProvider.cs:228-249`), so a lookup used
  for autocomplete must select exactly one column:

```json
["sample label", "another label"]
```

- `selectbyid…` returns `QuerySingleOrDefaultAsync` — **no match produces `200` with a `null` body**, not
  `404` (`CommonLookupProvider.cs:130-161`). If the lookup defines no `ByIdQuery`, the provider throws
  `ArgumentException` → `500 application/problem+json` (`src/Ballware.Generic.Service/Startup.cs:458-483`).
- With-`param` variants bind the path segment as `@param` (`CommonLookupProvider.cs:164-226`).
- Because the endpoints pass `principalUtils.GetUserRights(user)` (`LookupDataEndpoint.cs:121,144,…`),
  the **rights overloads** run: the tenant variable is bound and the rights collection is bound to the
  **deprecated** `@claims` variable as a comma-joined string
  (`DeprecatedRightVariableName = "claims"`, `CommonLookupProvider.cs:13`, assigned at `:123,157,181,220,244,267`).
  Claims are *not* expanded into `@claim_*` on these routes (that happens only on the `claims`
  overloads, used by the report-datasource flow), and `@tenantId` / `@tenant_id` is a bound `Guid`.
- `@tenantId`/`@tenant_id` is always bound here (`TenantPlaceholderOptions.Create()`,
  `CommonLookupProvider.cs:114-115,126-127,…`).

`404` bodies are JSON strings, exactly:

| Case | Body |
| --- | --- |
| tenant metadata missing | `"Tenant with ID <tenantId> not found."` (`LookupDataEndpoint.cs:128`) |
| lookup by id missing | `"Lookup with ID <lookupId> not found for tenant <tenantId>."` (`:133`) |
| lookup by identifier missing | `"Lookup with identifier <identifier> not found for tenant <tenantId>."` (`:179`) |

## Processing state operations (3)

State support requires `Entity.StateColumn`
(`src/Ballware.Generic.Metadata/Entity.cs`; checked at `ProcessingStateDataEndpoint.cs:72,101,160`).
The current state of an instance is read with `GetScalarValueAsync` (`:77,112`), i.e. through the entity's
`ByIdQuery` + `ScalarValueQuery` (`src/Ballware.Generic.Tenant.Data.Commons/Provider/CommonGenericProvider.cs:388-393`).

`ProcessingStateSelectListEntry` ([schemas.md](schemas.md#processingstateselectlistentry)) is the only typed
payload of this document:

```json
[ { "Id": "<guid>", "Name": "In progress", "State": 2 } ]
```

### `GET /tenant/processingstate/selectlistallowedsuccessorsforentity/{entity}/{id}`

`:57-84`. Successors come from the meta service
(`SelectListPossibleSuccessorsForEntityAsync`, `:79`) and each is filtered by
`StateAllowedAsync(…)`, i.e. the entity's `StateAllowedScript`
(`src/Ballware.Generic.Scripting.Jint/Internal/JintEntityMetadataScriptingExecutor.cs:199-215`).
With no `StateAllowedScript` the result is an **empty array** (the executor returns `false`, `:214`).

### `GET /tenant/processingstate/selectlistallowedsuccessorsforentities/{entity}`

`:86-143`. The instance ids are **free query parameters** named `id` (repeatable), read from
`QueryValueBag` (`:106`), not path parameters — so the snapshot shows only `entity`.

```text
GET /tenant/processingstate/selectlistallowedsuccessorsforentities/<entity>?id=<guid>&id=<guid>
```

- Each id's allowed successors are computed, then the result is the **intersection** over all ids,
  compared by `Id` only (`:127-134`, comparer at `:11-15`).
- Exactly one `id` → that id's list (`:136-139`).
- **No `id` at all → `200` with an empty body** (`:142`) — not `400`.

### `GET /tenant/processingstate/isstateallowedforentity/{entity}/{state}/{id}`

`:145-171`. `{state}` is an `int`. Allowed → `200` **empty body**; not allowed → `403` **empty body**
(`Results.Forbid()`, `:170`). This is the only operation across all four documents that declares `403`.

`404` bodies: `"Tenant with ID <tenantId> not found."` (`:69,98,157`) and
`"Entity with identifier <entity> not found or has no state support for tenant <tenantId>."`
(`:74,103,162`) — the second one also covers entities without a `StateColumn`.

## Statistic operation (1)

`GET /tenant/statistic/dataforidentifier/{identifier}` — `StatisticDataEndpoint.cs:20-30`, handler `:32-65`.
Statistic metadata is `{ Identifier, FetchSql, FetchScript? }` (`src/Ballware.Generic.Metadata/Statistic.cs`).

- Response: array of rows (`FetchSql` aliases), enriched per row by `FetchScript`
  (`CommonStatisticProvider.cs:38-44`; script contract `JintStatisticScriptingExecutor.cs:16-41`).
- Free query parameters are accepted and pipe-joined for repeated keys exactly like `/query`
  (`StatisticDataEndpoint.cs:39-49`): `?tag=a&tag=b` → the string `|a|b|`.
- The tenant variable is bound (`CommonStatisticProvider.cs:31-32`), but **claims are not expanded into
  `@claim_*`** here — only `@param` bindings and the tenant variable exist in a statistic statement.
- `mlPredict(model, input)` is available **only** in `FetchScript`
  (`JintEngineExtensions.cs:10`, `JintStatisticScriptingExecutor.cs:27`), and every input value is
  stringified (`JintEngineExtensions.cs:16-19`); the prediction itself is fetched from the external ML
  service (`src/Ballware.Generic.Service/Adapter/MlServiceMlAdapter.cs`).
- `404` bodies: `"Tenant with ID <tenantId> not found."` (`:56`),
  `"Statistic with identifier <identifier> not found for tenant <tenantId>."` (`:61`).

## Spec gaps in this document

- `404` is **not declared** for any of the 12 operations even though four distinct `404` messages exist;
  the declared set is `200` + `401` (+ `403` for `isstateallowed`).
- `selectlistallowedsuccessorsforentities` accepts `id` query parameters that the snapshot does not
  declare.
- Lookup row payloads are untyped (`array` with no `items`, or `{}`); only `ProcessingStateSelectListEntry`
  is typed.
- The `null` result of a lookup by id is serialized as `null` with status `200`.

## Related pages

- [README.md](README.md) · [generic-api.md](generic-api.md) · [schemas.md](schemas.md) ·
  [examples.md](examples.md)
- [../concepts/tenancy-and-authorization.md](../concepts/tenancy-and-authorization.md) — scope policy vs.
  rights checks
- [../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md) — `StateAllowedScript`,
  `ScalarValueQuery`, `FetchScript`
