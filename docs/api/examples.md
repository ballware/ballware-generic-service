# Examples

Copy-pasteable sequences against a development instance on `http://localhost:5002`
(`src/Ballware.Generic.Service/appsettings.Development.json:8-14`). Every path below exists in the committed
snapshots ([../openapi/generic.json](../openapi/generic.json), [meta.json](../openapi/meta.json),
[service.json](../openapi/service.json), [schema.json](../openapi/schema.json)).

All identifiers, GUIDs and values are **synthetic** placeholders: `<application>`, `<entity>`,
`<queryIdentifier>`, `<guid>`, `<token>`. Response payloads illustrate the *shape* only — the actual columns
come from the tenant's metadata (see
[generic-api.md](generic-api.md#the-item-shape)).

```bash
BASE=http://localhost:5002
TOKEN=<token with the metaApi scope>          # generic + meta documents
SVC_TOKEN=<token with the serviceApi scope>   # service document
SCHEMA_TOKEN=<token with the schemaApi scope> # schema document
```

## Generic API

### Query with parameters

```bash
curl -s "$BASE/generic/<application>/<entity>/query?identifier=<queryIdentifier>&status=1&tag=a&tag=b" \
  -H "Authorization: Bearer $TOKEN"
```

Two values for the same key become the **single string** `|a|b|` in the statement
(`src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs:649-666`); a metadata query written for
multi-values expects exactly that form.

```json
[ { "Id": "<guid>", "Coltextline": "sample text", "Colnumber": 7 } ]
```

### All / count

```bash
curl -s "$BASE/generic/<application>/<entity>/all?identifier=primary" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/generic/<application>/<entity>/count?identifier=<countQueryIdentifier>" -H "Authorization: Bearer $TOKEN"
```

```json
{ "count": 42 }
```

### New template

```bash
curl -s "$BASE/generic/<application>/<entity>/new?identifier=primary" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/generic/<application>/<entity>/newquery?identifier=primary&parentId=<guid>" -H "Authorization: Bearer $TOKEN"
```

### Item by id (`id` is a **query** parameter)

```bash
curl -s "$BASE/generic/<application>/<entity>/byid?identifier=primary&id=<guid>" -H "Authorization: Bearer $TOKEN"
```

Missing row → `404` with body `"Item not found"` (`GenericDataEndpoint.cs:347-350`); existing row the caller
may not read → `401` empty.

### Save (insert and update)

```bash
# insert: no id member, or an id that does not match a row -> insert
curl -s -X POST "$BASE/generic/<application>/<entity>/save?identifier=primary" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{ "Coltextline": "sample text", "Colnumber": 7 }'

# update: provider probes ByIdQuery with @id and injects original_<column>
curl -s -X POST "$BASE/generic/<application>/<entity>/save?identifier=primary" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{ "Id": "<guid>", "Coltextline": "changed text", "Colnumber": 8 }'
```

Body is the **bare item** — no wrapper. Success is `200` with an **empty body**: re-read with `byid`.
On SQL Server the id member is `Id`; on PostgreSQL it is `id`
([../concepts/metadata-driven-execution.md](../concepts/metadata-driven-execution.md#provider-differences-visible-in-payloads)).

A dedicated save identifier also changes the **right name** that is checked (`edit` only for `primary`):

```bash
curl -s -X POST "$BASE/generic/<application>/<entity>/save?identifier=advance" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{ "Id": "<guid>" }'
```

### Save batch

```bash
curl -s -X POST "$BASE/generic/<application>/<entity>/savebatch?identifier=primary" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '[ { "Id": "<guid>", "Colnumber": 1 }, { "Coltextline": "new row" } ]'
```

Bare JSON **array**. Rights are evaluated for **every** item before **any** save
(`GenericDataEndpoint.cs:426-435`); each save is its own transaction.

### Remove

```bash
curl -s -X DELETE "$BASE/generic/<application>/<entity>/remove/<guid>" -H "Authorization: Bearer $TOKEN"
```

`200` empty on success. A refused `RemovePreliminaryCheckScript` yields `400` whose body is a serialized
exception whose `Message` joins the script messages with `\r\n`
(`GenericDataEndpoint.cs:476-479`).

### Import (multipart, one job per file)

```bash
# CSV — headers must match the save statement's parameter names
curl -s -X POST "$BASE/generic/<application>/<entity>/import?identifier=importcsv" \
  -H "Authorization: Bearer $TOKEN" \
  -F "files=@./rows.csv;type=text/csv"

curl -s -X POST "$BASE/generic/<application>/<entity>/import?identifier=importjson" \
  -H "Authorization: Bearer $TOKEN" \
  -F "files=@./rows.json;type=application/json"
```

`201 Created` with an empty body — no job id, no row count. Format, delimiter and column mapping come from
the entity's `CustomFunctions` entry with `Type = import` and `Id` equal to `identifier`.

`rows.csv` (column names are placeholders for the metadata's `@param` names):

```csv
Id,Coltextline,Colnumber
<guid>,sample text,7
,sample text 2,8
```

`rows.json` must be an **array of objects**:

```json
[ { "Id": "<guid>", "Coltextline": "sample text", "Colnumber": 7 } ]
```

Track the outcome through the meta service job record (`scheduler: "generic"`, `identifier: "import"`).
See [../concepts/data-integration-and-jobs.md](../concepts/data-integration-and-jobs.md).

### Export inline

```bash
curl -s "$BASE/generic/<application>/<entity>/export?identifier=exportcsv&id=<guid>" -H "Authorization: Bearer $TOKEN"
```

Unlike `query`, a repeated key here stays a **list** and is expanded by Dapper (`id=<guid>&id=<guid2>`).
Media type comes from the export function's `options.format`. An identifier that is missing from
`ListQuery` **or** from `CustomFunctions` results in `500 application/problem+json`.

### Export to a download URL, then download

```bash
# parameters go into the FORM BODY, not the query string
curl -s -X POST "$BASE/generic/<application>/<entity>/exporturl?identifier=exportjson" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/x-www-form-urlencoded" \
  --data-urlencode "id=<guid>"
```

```json
{ "TenantId": "<guid>", "Id": "<guid>" }
```

```bash
# anonymous: no Authorization header, valid for 1 day after exporturl
curl -s -O -J "$BASE/generic/<application>/<entity>/download/<tenantId>/<exportId>"
```

Served file name: `<exportQuery>_<yyyyMMdd_HHmmss><ext>` — the timestamp is the **download** time
(`GenericDataEndpoint.cs:646`). Expired or unknown export → `404` empty.

## Meta API

### Lookup select list / by id / with param / autocomplete

```bash
curl -s "$BASE/tenant/lookup/selectlistforlookup/<lookupId>" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/tenant/lookup/selectlistforlookupidentifier/<lookupIdentifier>" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/tenant/lookup/selectbyidforlookup/<lookupId>/<id>" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/tenant/lookup/selectbyidforlookupidentifier/<lookupIdentifier>/<id>" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/tenant/lookup/selectlistforlookupwithparam/<lookupId>/<param>" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/tenant/lookup/selectbyidforlookupwithparam/<lookupId>/<param>/<id>" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/tenant/lookup/autocompleteforlookup/<lookupId>" -H "Authorization: Bearer $TOKEN"
curl -s "$BASE/tenant/lookup/autocompleteforlookupwithparam/<lookupId>/<param>" -H "Authorization: Bearer $TOKEN"
```

Note the order in the by-id-with-param route: **lookupId, then param, then id**. The `withparam` variants
bind the path segment as `@param` in the lookup SQL. Autocomplete returns plain strings
(`[ "sample label" ]`) and requires the lookup's `ListQuery` to select one column. A lookup without
`ByIdQuery` fails with `500`, not `404`.

## Service API

### Single column value of a lookup row

```bash
curl -s "$BASE/tenant/lookup/columnvaluebytenantandidforlookup/<tenantId>/<lookupId>/<id>/<column>" \
  -H "Authorization: Bearer $SVC_TOKEN"
```

```json
"sample value"
```

Column names are matched case-sensitively → otherwise `404 "Column '<column>' not found in the result."`

### ML training data (raw SQL as a JSON string)

```bash
curl -s -X POST "$BASE/tenent/mlmodel/trainingdatabytenantandplainquery/<tenantId>" \
  -H "Authorization: Bearer $SVC_TOKEN" -H "Content-Type: application/json" \
  -d '"select uuid as id, name from [ballwareschema].<entity> where tenant_id=@tenant_id"'
```

Empty string → `400 "Query is empty."`. The SQL runs verbatim against the tenant database
([../api/service-api.md](../api/service-api.md), *ML training data* section).

### Report datasources

```bash
curl -s "$BASE/tenant/tenant/reportdatasourcesfortenant/<tenantId>" -H "Authorization: Bearer $SVC_TOKEN"
```

The response embeds the tenant database **connection string** — privileged endpoint.

## Meta API: processing state and statistic

### Allowed successor states (one instance)

```bash
curl -s "$BASE/tenant/processingstate/selectlistallowedsuccessorsforentity/<entity>/<guid>" \
  -H "Authorization: Bearer $TOKEN"
```

```json
[ { "Id": "<guid>", "Name": "In progress", "State": 2 } ]
```

An entity without `StateColumn`, or without `StateAllowedScript`, returns `404` or `[]` respectively.

### Allowed successor states (several instances → intersection)

```bash
curl -s "$BASE/tenant/processingstate/selectlistallowedsuccessorsforentities/<entity>?id=<guid>&id=<guid2>" \
  -H "Authorization: Bearer $TOKEN"
```

Ids are **query** parameters; no `id` at all gives `200` with an empty body.

### Is a state allowed

```bash
curl -s -o /dev/null -w "%{http_code}\n" \
  "$BASE/tenant/processingstate/isstateallowedforentity/<entity>/<state>/<guid>" -H "Authorization: Bearer $TOKEN"
```

`200` (empty) when allowed, `403` (empty) when not.

### Statistic payload with parameters

```bash
curl -s "$BASE/tenant/statistic/dataforidentifier/<statisticIdentifier>?period=2026-01&tag=a&tag=b" \
  -H "Authorization: Bearer $TOKEN"
```

## Schema API

### Create or update the table of one entity

```bash
curl -s -X POST "$BASE/api/tenant/createorupdateentityschemafortenant/<tenantId>" \
  -H "Authorization: Bearer $SCHEMA_TOKEN" -H "Content-Type: application/json" \
  -d '{
    "UserId": "<guid>",
    "SerializedEntityModel": "{\"tableName\":\"<table>\",\"noIdentity\":false,\"customColumns\":[{\"columnName\":\"name\",\"columnType\":\"nvarchar\",\"maxLength\":100,\"nullable\":true}],\"customIndexes\":[]}"
  }'
```

### Align the tenant database with a tenant model

```bash
curl -s -X POST "$BASE/api/tenant/createorupdateschemafortenant/<tenantId>" \
  -H "Authorization: Bearer $SCHEMA_TOKEN" -H "Content-Type: application/json" \
  -d '{
    "UserId": "<guid>",
    "Provider": "mssql",
    "SerializedTenantModel": "{\"schema\":\"<schema>\",\"databaseObjects\":[{\"name\":\"<view name>\",\"type\":\"view\",\"sql\":\"create or alter view …\",\"execute\":true}]}"
  }'
```

The payload is the JSON **string** form of the provider tenant model
([../api/schema-api.md](../api/schema-api.md), *create or update schema* section).

### Drop one entity table / the whole tenant schema

```bash
curl -s -X DELETE "$BASE/api/tenant/dropentityschemafortenant/<tenantId>/<table identifier>?userId=<guid>" \
  -H "Authorization: Bearer $SCHEMA_TOKEN"

# destructive: removes the tenant schema, its user, all tenant data and the connection record
curl -s -X DELETE "$BASE/api/tenant/dropschemafortenant/<tenantId>?userId=<guid>" \
  -H "Authorization: Bearer $SCHEMA_TOKEN"
```

`userId` is an optional query parameter used for master-store auditing only.

## Error shapes

| Call | Result |
| --- | --- |
| no / wrong scope token | `401` empty |
| `…/query?identifier=<unknown>` | `200` with the `primary` query result (silent fallback) |
| `…/export?identifier=<undeclared>` | `500 application/problem+json` |
| `…/byid` with a non-existent id | `404 "Item not found"` |
| `…/save?identifier=primary` with an empty body | `400 "No value provided"` |
| `…/savebatch` with an empty body | `400 "No values provided"` |
| `…/remove/<guid>` refused by the metadata | `400` with a serialized exception |
| `…/tenant/lookup/...` with an unknown lookup | `404 "Lookup with ID <lookupId> not found for tenant <tenantId>."` |
| unknown tenant/entity on a generic route | `404 "Unknown tenant or entity"` |

## Related pages

- [README.md](README.md) — conventions, token acquisition, the two multi-value encodings
- [generic-api.md](generic-api.md) · [meta-api.md](meta-api.md) · [service-api.md](service-api.md) ·
  [schema-api.md](schema-api.md)
