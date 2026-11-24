# Data integration and jobs

Import and export are **asynchronous, file-based** flows that combine three external services: the storage
service (files), the meta service (job and export records) and the tenant database (the actual data). Quartz
owns only the trigger and execution; **all state lives in the meta service.**

`GenericDataEndpoint.cs` means `src/Ballware.Generic.Api/Endpoints/GenericDataEndpoint.cs`.

## Import sequence

```text
POST /generic/<application>/<entity>/import?identifier=<importFunctionId>
  multipart field "files" (1..n files)
      |
      | for EACH file:
      v
 1. temporaryId = new Guid                                    GenericDataEndpoint.cs:498
 2. upload to the storage service                             :510
 3. job record in the meta service                            :512-519
 4. Quartz trigger JobKey("import","generic")                 :523
      v
 201 Created, empty body                                      :526

  Quartz job GenericImportJob.Execute
      |
 5. read JobDataMap, set state InProgress                     GenericImportJob.cs:30-54
 6. resolve tenant + entity metadata                          :55-61
 7. fetch the temp file stream from the storage service       :63
 8. provider ImportAsync: parse rows, per-row rights, save    :65-71
 9. delete the temp file (SUCCESS ONLY)                       :73
10. state Finished  /  Error                                  :75-87
```

Endpoint: `GenericDataEndpoint.cs:130-139` (route, `201`, name `GenericImport`); handler `:485-527`.

### `JobDataMap` keys

| Key | Value | Set at |
| --- | --- | --- |
| `tenantId` | `Guid` | `GenericDataEndpoint.cs:502` |
| `userId` | `Guid` | `:503` |
| `application` | route segment | `:504` |
| `entity` | route segment | `:505` |
| `identifier` | the **import function** identifier | `:506` |
| `claims` | **JSON string** of the user's claim dictionary | `:507` |
| `file` | the client-side `temporaryId` (`Guid`) | `:508` |
| `jobId` | the id returned by the meta service | `:521` — **after** serialization |

Asymmetry worth knowing: `JobCreatePayload.Options` is serialized at `:516`, **before** `jobData["jobId"]` is
added at `:521`. The JSON stored in the meta service therefore contains
`tenantId, userId, application, entity, identifier, claims, file` but **not** `jobId`; only the in-memory map
handed to Quartz has it. `JobCreatePayload` itself (`src/Ballware.Generic.Metadata/Job.cs:5-12`) carries only
`Scheduler = "generic"`, `Identifier = "import"`, `Options` (`GenericDataEndpoint.cs:512-517`) — both
`Identifier` and `Scheduler` are **constants**, independent of the route `identifier`.

### Job states

`JobStates` (`src/Ballware.Generic.Metadata/Job.cs:21-37`): `Unknown`, `Queued`, `InProgress`, `Finished`,
`Error` (serialized with those exact strings).

| Transition | Where |
| --- | --- |
| → `Queued` | the meta service, on job creation (external) |
| → `InProgress` | `src/Ballware.Generic.Jobs/Internal/GenericImportJob.cs:40-54` — written **before** any work, so a failed metadata lookup still leaves `InProgress` → `Error` |
| → `Finished` | `:75-77` |
| → `Error` | `:79-87`, with `Result = JsonConvert.SerializeObject(ex)` (`:82`) and `JobExecutionException(refireImmediately: false)` (`:86-87`) |

Job **records** are created/updated in the meta service through
`IMetadataAdapter.CreateJobForTenantBehalfOfUserAsync` / `UpdateJobForTenantBehalfOfUserAsync`
(`src/Ballware.Generic.Metadata/IMetadataAdapter.cs:25-29` →
`src/Ballware.Generic.Service/Adapter/MetaServiceMetadataAdapter.cs:72-94`); `JobUpdatePayload` is
`{ Id, State, Result? }` (`Job.cs:14-19`). `JobStates` never appears in this service's own HTTP contracts —
see [../api/schemas.md](../api/schemas.md#jobstates).

### Quartz wiring

```text
Services.Configure<QuartzOptions>(GetSection("Quartz"))          Startup.cs:251
Services.AddBallwareGenericBackgroundJobs()                       Startup.cs:252
    -> AddQuartz + AddJob<GenericImportJob>(Key("import","generic"), StoreDurably)
    -> AddQuartzServer(WaitForJobsToComplete = true)             src/Ballware.Generic.Jobs/ServiceCollectionExtensions.cs:12-20
```

- Job key `name: "import"`, `group: "generic"` (`GenericImportJob.cs:11`), stored durably; triggered with a
  one-shot trigger (`GenericDataEndpoint.cs:523`).
- Committed configuration (`src/Ballware.Generic.Service/appsettings.json:32-38`) is a **clustered**,
  persistent ADO job store (`JobStoreTX` + `PostgreSQLDelegate` + `quartz.serializer.type: json` +
  `instanceId: AUTO`) so several instances share one scheduler. **No `quartz.dataSource` key is committed** —
  the job-store datasource must come from environment/`appsettings.local.json`.
- `appsettings.Development.json` does **not** override Quartz (the file only contains `Logging` and
  `Kestrel`), so a development run also needs a real job store. Only `appsettings.Swagger.json:42-44` switches
  to `Quartz.Simpl.RAMJobStore`, which is why snapshot generation needs no database
  ([../openapi/README.md](../openapi/README.md)).
- No retry policy, no `@DisallowConcurrentExecution`, no scheduler listener in this repository.

### Import formats and the header mapping

`ProcessImportAsync` (`src/Ballware.Generic.Tenant.Data.Commons/Provider/CommonGenericProvider.cs:425-468`):

- The `CustomFunctions` entry with `Type == import` and `Id == identifier` selects the format; without it the
  import throws `ArgumentException "Unsupported function identifier …"` (`:433-439`).
- `options.format`: `csv` (`:443-453`) or `json` (`:454-464`); anything else →
  `ArgumentException "Unsupported import format …"` (`:465-467`).
- `options.delimiter` defaults to `,` (`:470-475`).
- **CSV**: each header name becomes the item key verbatim, empty cells become `null`, all values arrive as
  `string` (`:480-486`). Headers therefore have to match the `SaveStatement`'s `@param` names.
- **JSON**: the body must be an **array of objects** (`:493`), normalized to CLR values (`:499`).
- Rows are saved through the **same save path** as `save`, with the import identifier as save identifier
  (fallback to `primary`, `:449` → `:291-296`).
- One transaction for the whole file (`:142-157`): one failing row rolls back everything.
- Rows that fail the rights callback are **skipped silently** — no counter, no partial-failure report
  (`:447-451,457-462`; `ImportAsync` returns `Task`).

### Per-row rights

Supplied by the job, not by the endpoint (`GenericImportJob.cs:65-71`):

```csharp
tenantAuthorized = tenantRightsChecker.HasRightAsync(tenant, application, entity, claims, identifier);
authorized       = entityRightsChecker.HasRightAsync(tenantId, entityMetadata, claims, identifier, item, tenantAuthorized);
```

The **right name is the route `identifier`** (e.g. `import`, `importjson`), and the row payload is available
to the entity-level check. The endpoint injects both checkers but never calls them
(`GenericDataEndpoint.cs:486-487`) — `201 Created` is not an authorization statement.

### Failure handling

`GenericImportJob.cs:79-88`: state `Error`, `Result` = the serialized exception, then
`JobExecutionException(cause: ex, refireImmediately: false)`. The uploaded temporary file is removed **only on
the success path** (`:73`), so a failed import leaves the file in the storage service (asserted by
`test/Ballware.Generic.Jobs.Tests/GenericImportJobTest.cs:298-299,404-405`).

## Export sequence

Two flavours; see [../api/generic-api.md](../api/generic-api.md#get-export) for the request contracts.

```text
A) inline          GET .../export?identifier=<exportId>&param=…
                      -> provider ExportAsync -> raw body (CSV/JSON), no record, no file

B) file            POST .../exporturl  (form fields = parameters)
                      -> provider ExportAsync
                      -> ExportCreatePayload in the meta service   GenericDataEndpoint.cs:610-619
                      -> temp file under key = export id           :621
                      -> 200 { "TenantId": …, "Id": … }            :623-627

                 GET .../download/{tenantId}/{id}   (anonymous, no token)
                      -> FetchExportForTenantByIdAsync             :637
                      -> null OR ExpirationStamp <= now  => 404    :639-642
                      -> TemporaryFileByIdAsync(tenantId, export.Id) :644
                      -> file stream, name <Query>_<yyyyMMdd_HHmmss><ext>  :646
```

- Expiration is computed **locally** as `DateTime.Now.AddDays(1)` (`:616`) and stored in the nullable
  `ExportCreatePayload.ExpirationStamp` (`src/Ballware.Generic.Metadata/Export.cs:3-14`); the meta service
  owns the record. There is **no delete/cancel API** for export records in `IMetadataAdapter`.
- Storage key = export id, stored file name = `<exportId><ext>` (`:621`); the provider's own
  `GenericExport.FileName` (`<identifier>.<format>`, `CommonGenericProvider.cs:534`) is **not** used for the
  upload — the served download name is built at `:646` from the export **query identifier** and the
  **download** timestamp.
- `MediaType` comes from `MimeTypeMap.GetMimeType(format)` (`CommonGenericProvider.cs:535`).
- An unknown export identifier is an unhandled `ArgumentException` → `500`, not `404`
  (`CommonGenericProvider.cs:514-526`, `Startup.cs:458-483`).
- Multi-value parameters are **not** pipe-joined on either export route (`GenericDataEndpoint.cs:543-548,585-590`)
  — export SQL must be written for Dapper list expansion.

## File and client plumbing

| Contract | Implementation | External calls |
| --- | --- | --- |
| `IGenericFileStorageAdapter` (`src/Ballware.Generic.Api/IGenericFileStorageAdapter.cs:5-10`) and `IJobsFileStorageAdapter` (`src/Ballware.Generic.Jobs/IJobsFileStorageAdapter.cs:3-7`) | single class `StorageServiceFileStorageAdapter`, registered scoped twice (`Startup.cs:329-330`) | `ballware.storage.service.client`: temporary upload / download / drop (`src/Ballware.Generic.Service/Adapter/StorageServiceFileStorageAdapter.cs:14-31`) |
| `IMetadataAdapter` | `MetaServiceMetadataAdapter` (`Startup.cs:327`) | `ballware.meta.service.client`: entity/tenant/lookup/statistic metadata, job create+update, export create+fetch |
| `IMlAdapter` | `MlServiceMlAdapter` (`Startup.cs:328`) | `Ballware.Ml.Service.Client`: model consumption (used by `mlPredict` in statistic scripts) |

All three HTTP clients authenticate with **client credentials** via
`Duende.AccessTokenManagement` (`Startup.cs:254-281` for the client registrations, `:283-317` for
`AddHttpClient<…>().AddClientCredentialsTokenHandler(…)`), driven by the option sections
`MetaClient`, `StorageClient`, `MlClient` — each requiring `ServiceUrl`, `TokenEndpoint`, `ClientId`,
`ClientSecret`, `Scopes` (`src/Ballware.Generic.Service/Configuration/ServiceClientOptions.cs:5-20`) and
failing startup when a section is missing (`Startup.cs:143-156`). In `DEBUG` builds these clients accept any
server certificate (`#if DEBUG` blocks at `:287-292,299-304,311-316`).

Note the request-size ceiling that applies to imports in every environment:
`Kestrel.Limits.MaxRequestBodySize = 40000000` (~40 MB, `appsettings.json:2-6`).

## Monitoring imports

Because `POST …/import` returns `201` with an empty body and reports nothing about rows, use the job record
in the meta service: `scheduler = "generic"`, `identifier = "import"`, `state`, and `result` (the serialized
exception on failure). Correlate by tenant/user and the file name recorded in `options`. A silent partial
import (all rows skipped by rights) is indistinguishable from a successful one — the only reliable signal is
the row count in the tenant data.

## Related pages

- [../api/generic-api.md](../api/generic-api.md) — `import`, `export`, `exporturl`, `download`
- [metadata-driven-execution.md](metadata-driven-execution.md) — parse/save/export internals
- [tenancy-and-authorization.md](tenancy-and-authorization.md) — where import rights are enforced
- [architecture.md](architecture.md) — Quartz and the client-credentials wiring
