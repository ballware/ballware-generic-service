# OpenAPI snapshots

The committed files in this folder are the source of truth the API pages were written against:

| File | Content |
| --- | --- |
| [generic.json](generic.json) | `ballware Generic API` — 13 paths, 3 component schemas, OpenAPI `3.0.4` |
| [meta.json](meta.json) | `ballware Generic Meta API` — 12 paths, 1 component schema, OpenAPI `3.0.4` |
| [service.json](service.json) | `ballware Service API` — 3 paths, 3 component schemas, OpenAPI `3.0.4` |
| [schema.json](schema.json) | `ballware Schema API` — 4 paths, 2 component schemas, OpenAPI `3.0.4` |
| [manifest.json](manifest.json) | Provenance: generator tool + version, environment, git describe reference, `fetchedAt`, per-document openapi/title/pathCount/schemaCount |
| [fetch-spec.sh](fetch-spec.sh) | Generator script |

## How the snapshot is produced

Generation runs **offline from the built assembly** — no database, no IdP. The CI pipeline uses the
same mechanism for the client build (`.github/workflows/publish-packages.yml:31-32, 50-60`); this
folder extends it from the two CI documents (`service`, `schema`) to all four.

Run from any location inside the repository (idempotent — it rebuilds and rewrites the four
documents plus the manifest):

```bash
docs/openapi/fetch-spec.sh
```

What it does:

1. `dotnet build src/Ballware.Generic.Service --configuration Release`
   — requires `dotnet restore` against the private `ballware` NuGet feed (see the repository
   [README.md](../../README.md)); without those packages the Release build cannot be completed and
   the snapshots cannot be regenerated.
2. Ensures the tool is available — global install guarded by a PATH check:
   `dotnet tool install Swashbuckle.AspNetCore.Cli --version 10.3.0 --global`
   (the CI alternative: `dotnet new tool-manifest && dotnet tool update Swashbuckle.AspNetCore.Cli --version 10.3.0`,
   invoked via `dotnet tool run swagger …`). The script warns if the installed version differs from
   the pin.
3. Exports the four documents with the Swagger environment:

   ```bash
   export ASPNETCORE_ENVIRONMENT=Swagger
   export Authorization__Authority=https://localhost
   cd src/Ballware.Generic.Service
   for doc in generic meta service schema; do
     swagger tofile --output ../../docs/openapi/$doc.json bin/Release/net10.0/Ballware.Generic.Service.dll $doc
   done
   ```

4. Validates each file (parses as JSON, `openapi` starts with `3.`, `paths` non-empty) and writes
   `manifest.json`.

The `swagger tofile` tool resolves the host from the assembly **without starting it**, so no tenant
master database, no Redis, no Quartz job store and no IdP is required.

## Why `ASPNETCORE_ENVIRONMENT=Swagger`

`appsettings.Swagger.json` (`src/Ballware.Generic.Service/appsettings.Swagger.json`) supplies
**fake** Authority/audience/clients (`https://localhost`, `fake*` clients and secrets) and disables
the tenant storages. Consequently the `oidc` security definition in every snapshot points at a
non-existent endpoint:

```json
"oidc": { "type": "openIdConnect", "openIdConnectUrl": "https://localhost/.well-known/openid-configuration" }
```

That URL is a placeholder, **not** a real IdP discovery endpoint. Do not wire clients to it.

> **Leak warning:** configuration order is
> `appsettings.json` → `appsettings.{Environment}.json` → `appsettings.local.json` → environment
> variables (`src/Ballware.Generic.Service/Program.cs:13-17`). A developer `appsettings.local.json`
> overrides the Swagger values — including the Authority — and its real IdP URL would be baked into
> the committed snapshot. The script therefore pins `Authorization__Authority=https://localhost` as
> an environment override. When regenerating by hand (or in CI), generate from a clean checkout
> without `appsettings.local.json`, and check each snapshot's
> `components.securitySchemes.oidc.openIdConnectUrl` afterwards.

## Tool version pin

Keep `Swashbuckle.AspNetCore.Cli` at **10.3.0** — it matches the service's Swashbuckle packages
(`src/Ballware.Generic.Service/Ballware.Generic.Service.csproj:33-34`) and the CI tool pin
(`.github/workflows/publish-packages.yml:32`). Other versions may reformat the documents and create
spurious diffs.

## Security block

Each document declares one `openIdConnect` scheme named `oidc`
(`src/Ballware.Generic.Service/Startup.cs:439-443`) and attaches a **per-operation** security
requirement derived from the API explorer group of the operation
(`src/Ballware.Generic.Service/Startup.cs:445`, `GroupScopeSecurityFilter` at
`src/Ballware.Generic.Service/Startup.cs:571-593`):

| Document | Group | Scope in every operation's `security` |
| --- | --- | --- |
| `generic.json` | `generic` | `["openid", "metaApi"]` |
| `meta.json` | `meta` | `["openid", "metaApi"]` |
| `service.json` | `service` | `["openid", "serviceApi"]` |
| `schema.json` | `schema` | `["openid", "schemaApi"]` |

There is no document-level `security` array any more; the requirement is on the operation. Scope
values come from `AuthorizationOptions` (`src/Ballware.Generic.Service/Configuration/AuthorizationOptions.cs:24-26`).

## Refresh cadence and drift

The snapshot is a manual artifact: regenerate after endpoint changes (new/renamed routes, DTO
changes, Swagger configuration). A re-run should only change `manifest.json`'s `fetchedAt`; if a
`*.json` document also changes, review the diff — it is an API surface change and the API pages here
should be reviewed against it. Regenerating does not require touching any source code.

Optional follow-up (not part of this setup): extend `.github/workflows/publish-packages.yml` with
`swagger tofile` steps for the `generic` and `meta` documents and publish all four as artifacts, so
refreshes can be automated.

## Comparing against previously fetched live documents

An older copy of these documents was fetched from a **running** instance (`openapi: 3.0.1`). Compared with
the snapshots committed here (pinned `swagger tofile` 10.3.0, `openapi: 3.0.4`), the differences are exactly:

| Aspect | Live-fetched copy | Committed snapshots |
| --- | --- | --- |
| declared `openapi` version | `3.0.1` | `3.0.4` |
| `security` | one **document-level** array `[{ "oidc": ["openid", "genericApi"] }]` in every document | **per-operation** `[{ "oidc": ["openid", "<document scope>"] }]`, no document-level array |
| top-level `tags` | absent | present (`Generic`; `Lookup`, `MlModel`, `Tenant`; `Tenant`) |
| `byid` / `save` / `remove` summaries | `Query existing tenant` / `Save existing or new tenant` | `Query item by id` / `Save item` / `Remove item` |

Path sets and component-schema counts are **identical** for `generic` (13/3), `service` (3/3) and `schema`
(4/2) — the new `meta` document (12/1) is additive and absorbed nothing. Consumers that diff snapshots
across sources should expect the version/security/tags lines to differ.

## Snapshot ↔ docs mapping

| Snapshot | Documented by |
| --- | --- |
| `generic.json` paths + schemas | [../api/generic-api.md](../api/generic-api.md), generic tables in [../api/schemas.md](../api/schemas.md) |
| `meta.json` paths + schemas | [../api/meta-api.md](../api/meta-api.md), meta tables in [../api/schemas.md](../api/schemas.md) |
| `service.json` paths + schemas | [../api/service-api.md](../api/service-api.md), service tables in [../api/schemas.md](../api/schemas.md) |
| `schema.json` paths + schemas | [../api/schema-api.md](../api/schema-api.md), schema tables in [../api/schemas.md](../api/schemas.md) |
| all four | [../api/examples.md](../api/examples.md) (every example path exists in the snapshots) |

## Known spec limitations

The snapshots are honest about what they declare — and only about that. Documented deviations are
listed in [../api/generic-api.md](../api/generic-api.md#known-spec-inaccuracies): untyped
`IEnumerable<object>` query responses, the missing `400`/`403` declarations, the declared
`contentType: "application/json"` of the anonymous download route, and the `/tenent/mlmodel` path
typo (live, not fixed).

## Live-server fallback

If `swagger tofile` ever fails to resolve the entry point, export from a running instance instead
(needs a reachable tenant master database and Quartz job store to start at all; the raw documents can be
read without authentication):

```bash
ASPNETCORE_ENVIRONMENT=Swagger dotnet run --project src/Ballware.Generic.Service
for doc in generic meta service schema; do
  curl -s "http://localhost:5002/swagger/$doc/swagger.json" -o "docs/openapi/$doc.json"
done
```

Note the environment: Swagger generation and serving are only wired when a **`Swagger`** section exists
(`Startup.cs:409-447`, `Startup.cs:513-534`), and that section exists only in `appsettings.Swagger.json:29-32`.
Under `ASPNETCORE_ENVIRONMENT=Development` the documents are **not served** and the `curl` above returns
`404` — API Explorer metadata is registered unconditionally (`Startup.cs:246-247,407`), but the documents
are not. `ApiExplorer` alone is not enough.

Prefer the offline path; a live-server export inherits the developer's local configuration, and with a
developer `appsettings.local.json` present even `ASPNETCORE_ENVIRONMENT=Swagger` inherits its `Authorization`
values unless the environment override `Authorization__Authority` is applied.
