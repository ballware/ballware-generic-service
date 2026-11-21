# Best practices: metadata-driven payloads

> **Disclaimer.** These patterns are **generalized conventions** abstracted from many metadata
> configurations. They are not taken from any specific tenant or project, contain no real identifiers, table
> names, schema names, SQL from a deployment, or right strings, and are illustrative only. The authoritative
> authoring model lives in `ballware-meta-service/docs`; what this service *does* with the metadata is
> described in [../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md).

## 1. Treat identifiers as an API contract

`identifier` is a required query parameter on every read/write route
([../api/README.md](../api/README.md#identifier-semantics)), and an undeclared identifier on a read route
**silently falls back to `primary`**. Identifier families that make that fallback harmless in practice:

| Family | Purpose | Notes |
| --- | --- | --- |
| `primary` | the default list / by-id / new / save family | the only identifier with special meaning (`edit` right instead of the identifier name, and the fallback target) |
| `<scope>count` (e.g. a count query per scope) | `count` statements | `count` resolves inside `ListQuery`, so the count statement lives next to its list query |
| `<verb>` (e.g. `advance`, `assign`, `close`) | workflow saves | the identifier **is** the right name (`GenericDataEndpoint.cs:388-389`), so one right per allowed action |
| `<verb><format>` (e.g. `exportjson`, `exportcsv`, `importjson`, `importcsv`) | export/import functions | an export needs the **same** identifier in `ListQuery` **and** in `CustomFunctions` |
| scope-specific read families (e.g. `my…`, `report…`) | filtered views for one UI context | keep them in `ListQuery`/`ByIdQuery`, never overload `primary` |

Rules worth following:

- Never rely on the fallback for correctness: an unknown identifier should be a programming error, not a
  silent `primary` read.
- Keep `primary` free of side effects that a workflow identifier should own — a workflow step becomes auditable
  and rights-controlled only when it is its own identifier.
- Give export identifiers the format in their name; the file name and media type are derived from
  `options.format` (`CommonGenericProvider.cs:534-535`).

## 2. Separate read sources from write targets

A common and workable split: `ListQuery` / `ByIdQuery` read **from views** (joining, computed columns,
filtered), `SaveStatement` writes **to tables**. Consequences:

- `original_<column>` (`CommonGenericProvider.cs:312-315`) reflects the **`ByIdQuery` result**, not the
  physical row — so a view-based `ByIdQuery` decides which "previous values" a save script sees, and its
  column names decide the `original_*` key names.
- The existence probe always binds the parameter name `id` (`:307-308`), so **every** `ByIdQuery` must use
  `@id`, in both dialects.
- `RemoveStatement` is a single SQL string with no identifier and no script influence — make it self-sufficient
  (`@id` and the tenant variable only).

## 3. Placeholder discipline

| Use | Guidance |
| --- | --- |
| `[ballwareschema]` | always, for every tenant-owned table, view, function or type — it is the only portable way to address the tenant schema (`SqlServerStorageProvider.cs:42` / `PostgresStorageProvider.cs:42`) |
| tenant variable | `@tenantId` on SQL Server, `@tenant_id` on PostgreSQL; it is a **bound `Guid`**, never a literal — do not quote it |
| user identity in SQL | `@claim_sub` (there is no `@userId`), typically cast on PostgreSQL (`@claim_sub::uuid`) |
| other claims | `@claim_<claimtype>`, `string[]` claims arrive as `\|a\|b\|` |
| query parameters | `@<parameterName>` matching the query-string key **exactly** |
| `@claims` | deprecated; only the lookup paths still bind it (comma-joined rights) — do not use it in new metadata |

Multi-value matching idiom: metadata that wants a list on `/query` tests the joined string form
(`@tag` arrives as `|a|b|`), while metadata on `/export` must be written for **list expansion**
(`Uuid in @id` / `uuid = ANY(@id::uuid[])`). A statement written for one is not portable to the other — keep a
separate export identifier instead of reusing a query identifier.

## 4. Cast everything you receive

Query strings, CSV values and ML inputs arrive as **text**; only the tenant variable and JSON-body values keep
a real type. Prefer explicit casts in dialect-appropriate form (UUID/Guid columns, numeric comparisons) rather
than relying on implicit conversion. Nested JSON members of a save body are **removed before the statement**
(`ComplexDataUtils.cs:7-14`), so a save statement must not reference a nested member — write it from a script
via `dbInsert`/`entitySave`, or flatten it in the request.

## 5. Dialect-specific write idioms

Both providers accept upsert-style single statements, and the tests for each provider document the idiomatic
shape (`SqlServerGenericProviderTest.cs:193`, `PostgresGenericProviderTest.cs:188`):

| Provider | Idiom |
| --- | --- |
| SQL Server | update first, insert when nothing changed (`UPDATE …; if @@ROWCOUNT = 0 begin insert … end`) |
| PostgreSQL | `INSERT … ON CONFLICT (<tenant column>, <id column>) DO UPDATE SET …` |

Additional dialect realities:

- Column and alias casing is preserved verbatim into JSON — pick one convention per tenant and stay
  consistent (`Id`/`Coltextline` vs `id`/`coltextline`).
- Raw-table script helpers (`getList`, `getCount`, `dbInsert`, `dbUpdate`, `dbDelete`) exist only on SQL
  Server; on PostgreSQL they throw. Metadata that must run on both providers uses the `entity*` and
  `querySingle`/`getValue` families instead.
- Column type literals are provider vocabulary (`SqlServerModels.cs:33-43`) — a tenant model is **not**
  portable between providers.

## 6. Audit columns belong to the statement

The provisioning baseline creates audit columns but never fills them
(`SqlServerDbConnectionExtensions.cs:44-62` / `PostgresDbConnectionExtensions.cs:58-76`: creator, create
stamp, last changer, last change stamp). Write them in the save statement from claims, e.g. creator id from
`@claim_sub` and timestamps from the database clock — that keeps auditing independent of script execution and
works identically for `save`, `savebatch` and `import` (which reuse the same save path).

## 7. Workflow state changes are saves with their own identifier

Because the right name equals the identifier for `save`, a state transition becomes an access-controlled
operation by giving it its own `SaveStatement` identifier. Then:

- `StateColumn` + `StateAllowedScript` decide what is *allowed* (`StateAllowedScript` is evaluated for
  `isstateallowed…` and the successor lists; **no script means no transition**),
- the dedicated save statement performs the change and any bookkeeping,
- `getProcessingStateName(state)` is available in `BeforeSaveScript`/`SaveScript`
  (`JintEntityMetadataScriptingExecutor.cs:90-94,130-134`) for messaging rather than logic,
- successor lists are metadata (`Successors` in the meta service), not code — this service only filters them
  through `StateAllowedScript` (`ProcessingStateDataEndpoint.cs:79-81`).

`StateAllowedScript` cannot reach claims, entities or SQL — only `state`, `hasRight`, `hasAnyRight`,
`getValue`. Put complex conditions in the right definition, and read row facts via `getValue("<column>")`
(resolved through `ScalarValueQuery ?? "primary"` inside `ByIdQuery`).

## 8. Association maintenance belongs in `SaveScript`

`SaveScript` is the only script with both `entitySave`/`entityRemove` **and** `dbInsert`/`dbUpdate`/`dbDelete`
(`:129`). Since scripts cannot mutate the saved item (`:84,96,123,135`), maintaining related rows means
calling those host functions explicitly — they run on the **same connection and transaction** as the save
(`JintEngineExtensions.cs:73-81`), so a failure rolls back the main statement too.

Shared helper libraries live in `Tenant.ServerScriptDefinitions`, which is prepended to every script
(`:39-40`). There are **no platform-provided JS helpers**, so anything reusable is a tenant-level decision.
Keep helpers small and side-effect explicit; every evaluation gets a fresh engine, and `ListScript` runs once
per enumerated row.

## 9. Binary payloads travel as hex text

Since all values arrive as text, binary columns are carried as hex strings and converted inside the statement
(a dialect-specific conversion function on each provider). Keep the conversion in SQL, not in a script, and
declare the corresponding metadata parameter as `string`.

## 10. No server-side paging or sorting

There is no paging, sorting or filtering parameter in any route. `ORDER BY` lives **in the query text**, and
top-N behaviour must be expressed in the statement (dialect-specific row-limit syntax). If a UI needs paging,
it needs a dedicated identifier per page/sort combination — there is no generic mechanism to add here.

## 11. Design for the responses you actually get

- `save`/`savebatch`/`remove` succeed with an **empty body** — a caller must re-read (`byid`/`query`) to learn
  the resulting state, and cannot learn the generated id from the response.
- `count` is the only typed small response (`{"count": n}`) and is the cheapest existence check.
- Import reports nothing: `201` plus a job record. If a flow must know how many rows landed, derive it from
  the data or from a subsequent `count`, not from the import call.
- Silent behaviours to design around: unknown read identifiers → `primary`; unauthorized import rows → skipped;
  missing `ByIdQuery` on a lookup → `500`; undeclared export identifier → `500`.

## Related pages

- [../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md) — exact behavior and citations
- [../concepts/tenancy-and-authorization.md](../concepts/tenancy-and-authorization.md) — rights grammar and scripts
- [../concepts/multi-tenancy-and-provisioning.md](../concepts/multi-tenancy-and-provisioning.md) — table baseline and diffing
- [../api/examples.md](../api/examples.md) — the calls these conventions serve
