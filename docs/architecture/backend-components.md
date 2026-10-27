# Backend component catalog

This is a curated architecture-level catalog of the components that matter most when reading the Cave-revisions completion branch.

It is **not** a list of every service or repository in the application. Components are grouped by responsibility so this page stays useful as a map.

## Dependency rule

~~~text
Controller
    ↓
Service / workflow coordinator
    ↓
Pure planning/domain logic + feature repository capabilities
    ↓
Repository / data-access layer
    ↓
EF Core / Npgsql / PostgreSQL
~~~

See [Data-access architecture](../data-access-architecture.md) for the normative rule.

## Publication engines versus workflows

The most useful distinction is between the **engine that publishes history** and the **workflow that decides when to call it**.

| Publication engine | Used by | Why it exists |
| --- | --- | --- |
| CaveMutationCoordinator → CaveMutationRepository | Aggregate Cave save, proposal approval, archive/unarchive, hard delete, specialized File import | One-Cave transaction/concurrency semantics |
| CaveBulkRevisionRepository | Cave/Entrance CSV execution and Tag/County remaps | Bounded set-oriented revision publication |

CaveChangeRequestService is a workflow coordinator. It does not define a third revision engine; approval eventually uses the single-Cave publisher.

## Cave publication and history

| Component | Responsibility | Transaction / persistence role | Branch impact |
| --- | --- | --- | --- |
| CaveService | Interactive Cave aggregate authoring, validation, authorization, File/line-plot/Entrance/tag desired state | Orchestrates through repositories and CaveMutationCoordinator; does not own raw DbContext | **Major change** |
| CaveMutationCoordinator | Thin application boundary for Cave publication primitives | Delegates transaction/publish mechanics to CaveMutationRepository | **New** |
| CaveMutationRepository | Single-Cave publication: conflict checks, prepare/publish/delete, revision pointer advancement | Owns transactional publication algorithm used by CaveService/FileService workflows; existing Caves must already have rollout history | **New** |
| CavePublishedSnapshotRepository | Build canonical semantic Cave snapshots using bounded projections | Reads PostgreSQL directly in repository layer | **New** |
| CaveRevisionRolloutRepository | One-time initialization of revision history for Caves that predate the feature | Deployment-only, bounded/set-oriented, resumable; normal startup verifies completion | **New** |
| CaveRevisionService | Permission-aware revision/history use cases | Read orchestration only | **New** |
| CaveRevisionQueryRepository | Revision history and comparison persistence queries | Repository-level read model | **New** |
| CaveBulkRevisionRepository | Publish many Cave revisions for import/admin workflows | Joins caller-owned transaction; batched/set-oriented | **New** |
| CaveRepository | Current Cave data access plus aggregate persistence/locking helpers | Existing repository, significantly extended | **Major change** |

### Why both mutation repositories exist

CaveMutationRepository and CaveBulkRevisionRepository solve different execution problems.

- Interactive publication needs one-Cave optimistic concurrency, aggregate validation, and a user-facing conflict boundary.
- Imports/admin changes may touch thousands of Caves and must remain set-oriented.

They deliberately share `CavePublishedSnapshotRepository` and `CaveRevisionDiffService`, so the execution engines do not
define separate meanings for a published Cave state or a semantic no-op.

They intentionally share the same **snapshot semantics, revision chain, current revision pointer, tenant scope, and transaction atomicity** rather than sharing the same execution loop.

## Proposal and review workflow

| Component | Responsibility | Transaction / persistence role | Branch impact |
| --- | --- | --- | --- |
| CaveChangeRequestService | Authoring context, preview, create version, review, approve, reject, request changes, resubmit | Workflow coordinator; approval composes request persistence with Cave publication | **New** |
| CaveChangeRequestRepository | Change-request/version/review-event persistence and workflow locking | Owns EF/SQL details for proposal workflow | **New** |
| CaveProposalSnapshotV1 | Persisted immutable proposed desired state | Versioned JSON contract, not a service | **New** |
| CaveChangeRequestStatePolicy | Central state-machine rules for open/review/publish/delete behavior | Pure policy | **New** |
| CaveRevisionDiffService | Compare semantic published snapshots | Pure model/domain helper | **New** |

Approval intentionally calls the same Cave publication semantics used by direct edits rather than independently rewriting Cave tables.

## Files and object storage

| Component | Responsibility | Transaction / persistence role | Branch impact |
| --- | --- | --- | --- |
| FileService | Stage authoring Files, stream/view Files, verify object availability, cleanup external objects, and publish specialized account File imports | Coordinates FileRepository/object storage and uses the single-Cave publisher when a direct File import changes a Cave | **Major change** |
| FileRepository | File metadata, staged ownership, lookup/mutation persistence | Repository layer | **Major change** |
| IObjectStorage | Provider-neutral immutable Create/Open/Delete interface | Infrastructure boundary | **New** |
| AzureBlobObjectStorage | Azure Blob implementation of IObjectStorage | Infrastructure adapter | **New** |
| StorageCleanupTarget | Durable post-commit external-storage cleanup debt, purpose-scoped for Cave asset removal and Account Reset | Persistence entity deliberately independent of live Cave/File FKs | **New** |
| StagedCaveFilePublication | Validated publication intent for a staged File during proposal approval | Small immutable workflow value | **New** |

Important boundary: older archive/photo/trip/storage code may still use Azure SDK types. Do not infer that IObjectStorage currently fronts all storage in the repository.

## Import architecture

| Component | Responsibility | Transaction / persistence role | Branch impact |
| --- | --- | --- | --- |
| CaveImportCsvParser / EntranceImportCsvParser | Convert CSV stream to typed records | Database-free | **New architecture** |
| planning repositories | Preload only account-scoped materialized state needed to plan | Read-only repository layer | **New** |
| CaveImportPlanner / EntranceImportPlanner | Convert records + planning state into immutable plans | Pure; no database access | **New** |
| execution repositories | Lock/revalidate plan assumptions and apply set-oriented writes | Own import transaction and batched persistence | **New** |
| CaveBulkRevisionRepository | Publish revisions for the accepted bulk result | Joins executor transaction | **New** |
| ImportService | Parse → plan → preview or execute; post-commit external cleanup | Application coordinator | **Major change** |

The Cave/Entrance CSV dry-run path ends after planning; it is not “execute and roll back”. Account File import is a separate workflow and does not use these pure planners or CaveBulkRevisionRepository.

## Reference administration

| Component | Responsibility | Why it matters to revisions | Branch impact |
| --- | --- | --- | --- |
| TagTypeAdministrationRepository | Rename/merge/delete TagTypes with correct locks and affected-Cave behavior | Merge/remap can semantically change many Caves and therefore publish revisions | **New** |
| TagReferenceLockRepository | Stable ordered locks for existing TagType references | Prevents reference changes racing Cave publication | **New** |
| CountyAdministrationRepository | County merge/delete/update behavior | County moves can affect published Cave snapshots | **New** |
| CountyReferenceLockRepository | Stable County reference/mutation locks | Enforces lock hierarchy shared with Cave writes/imports | **New** |
| EligiblePeopleTagLookup | Resolve People tag identity under the shared comparison policy | Keeps direct/proposal/import identity semantics aligned | **New** |

## Account and destructive workflows

| Component | Responsibility | Important boundary | Branch impact |
| --- | --- | --- | --- |
| AccountService | Account settings/admin operations, reset orchestration, archive orchestration | Reset commits relational purge before external storage cleanup | **Major change** |
| AccountRepository | Account reset graph and archive projections | Large destructive repository capability | **Major change** |
| ArchiveJobCoordinator | Start/cancel/archive publication/pruning | Still uses Azure-specific storage APIs through legacy path | Existing, affected |
| ExportService | Build account archive payload | Current-state export behavior | **Changed** |

## Cross-cutting components changed on this branch

| Component | Responsibility | Branch change |
| --- | --- | --- |
| SaveChangesInterceptor + ownership batch loader | Enforce tenant/ownership constraints across EF writes | **Major shared-infrastructure refactor** |
| RequestThrottleService / ThrottleEventLogRepository | Request/file-stream throttling and event logging | **Changed / repository split** |
| FileResponsePolicy | Authenticated file response/range/security decisions | **Changed** |
| UserRepository, PermissionRepository, AppService | Permission/default-account/platform-admin behavior | **Changed** |
| FeatureSettingRepository and settings services | Feature/default settings | **Changed** |

## Frontend components worth knowing

| Component | Responsibility | Branch impact |
| --- | --- | --- |
| AddCaveComponent | Shared desired-state Cave form | **Major change** |
| CaveFormMapper | Maps loaded Cave/authoring context into submit DTO, including expected revision | **Major change** |
| CaveFileAuthoringEditor | Staged File authoring inside Cave form | **New** |
| CaveLinePlotAuthoringEditor | Add/rename/replace/remove line plots in aggregate form | **New** |
| CaveRevisionDiff and presentation helpers | Render revision/proposal differences | **New** |
| CaveHistoryModal | Revision history UI | **New** |
| SuggestCaveChangesPage | Initial proposal authoring | **New** |
| ReviseCaveChangeRequestPage | New immutable proposal version authoring | **New** |
| CaveChangeRequestPage | Review/approve/reject/request changes | **New** |
| frontend CaveService | Cave/revision/proposal API client | **Major change** |

## Completion-branch backend file inventory

The catalog above is responsibility-oriented, so it intentionally groups partial classes, planners, and support records under the component they serve. For completeness, the following collapsed inventory lists every changed C# file under the backend `Services`, `Repositories`, `Revisions`, import `Data`, and import `Planning` areas in the reviewed `main...HEAD` comparison (`main` `e342691c...` → completion `33ace6e7...`). It is a **change inventory**, not a claim that every file is a separate architectural service.

<details>
<summary>Show the exhaustive changed backend service/repository/planning inventory (54 files)</summary>

| Change | File |
| --- | --- |
| Modified | [`Planarian/Planarian/Modules/Account/Archive/Services/ExportService.cs`](../../Planarian/Planarian/Modules/Account/Archive/Services/ExportService.cs) |
| Modified | [`Planarian/Planarian/Modules/Account/Import/Services/ImportService.Caves.cs`](../../Planarian/Planarian/Modules/Account/Import/Services/ImportService.Caves.cs) |
| Modified | [`Planarian/Planarian/Modules/Account/Import/Services/ImportService.Entrance.cs`](../../Planarian/Planarian/Modules/Account/Import/Services/ImportService.Entrance.cs) |
| Modified | [`Planarian/Planarian/Modules/Account/Import/Services/ImportService.FIles.cs`](../../Planarian/Planarian/Modules/Account/Import/Services/ImportService.FIles.cs) |
| Modified | [`Planarian/Planarian/Modules/Account/Import/Services/ImportService.cs`](../../Planarian/Planarian/Modules/Account/Import/Services/ImportService.cs) |
| Modified | [`Planarian/Planarian/Modules/Account/Repositories/AccountRepository.cs`](../../Planarian/Planarian/Modules/Account/Repositories/AccountRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Account/Repositories/CountyAdministrationRepository.cs`](../../Planarian/Planarian/Modules/Account/Repositories/CountyAdministrationRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Account/Repositories/CountyReferenceLockRepository.cs`](../../Planarian/Planarian/Modules/Account/Repositories/CountyReferenceLockRepository.cs) |
| Modified | [`Planarian/Planarian/Modules/Account/Repositories/FeatureSettingRepository.cs`](../../Planarian/Planarian/Modules/Account/Repositories/FeatureSettingRepository.cs) |
| Modified | [`Planarian/Planarian/Modules/Account/Repositories/PermissionRepository.cs`](../../Planarian/Planarian/Modules/Account/Repositories/PermissionRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Account/Repositories/TagTypeAdministrationRepository.cs`](../../Planarian/Planarian/Modules/Account/Repositories/TagTypeAdministrationRepository.cs) |
| Modified | [`Planarian/Planarian/Modules/Account/Services/AccountService.cs`](../../Planarian/Planarian/Modules/Account/Services/AccountService.cs) |
| Modified | [`Planarian/Planarian/Modules/App/Services/AppService.cs`](../../Planarian/Planarian/Modules/App/Services/AppService.cs) |
| Modified | [`Planarian/Planarian/Modules/Authentication/Services/RequestThrottleService.cs`](../../Planarian/Planarian/Modules/Authentication/Services/RequestThrottleService.cs) |
| Modified | [`Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs`](../../Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CaveBulkRevisionRepository.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CaveBulkRevisionRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CaveChangeRequestRepository.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CaveChangeRequestRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CaveMutationRepository.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CaveMutationRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CaveMutationValidation.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CaveMutationValidation.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CaveMutationWorkflow.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CaveMutationWorkflow.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CavePublishedSnapshotRepository.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CavePublishedSnapshotRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CaveRevisionRolloutRepository.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CaveRevisionRolloutRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CaveRevisionQueryRepository.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CaveRevisionQueryRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/CaveTagReferencePolicy.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/CaveTagReferencePolicy.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/StagedCaveFilePublication.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/StagedCaveFilePublication.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Revisions/StagedFileObjectDeleteTarget.cs`](../../Planarian/Planarian/Modules/Caves/Revisions/StagedFileObjectDeleteTarget.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Services/CaveChangeRequestService.cs`](../../Planarian/Planarian/Modules/Caves/Services/CaveChangeRequestService.cs) |
| Added | [`Planarian/Planarian/Modules/Caves/Services/CaveRevisionService.cs`](../../Planarian/Planarian/Modules/Caves/Services/CaveRevisionService.cs) |
| Modified | [`Planarian/Planarian/Modules/Caves/Services/CaveService.cs`](../../Planarian/Planarian/Modules/Caves/Services/CaveService.cs) |
| Modified | [`Planarian/Planarian/Modules/Files/Repositories/FileRepository.cs`](../../Planarian/Planarian/Modules/Files/Repositories/FileRepository.cs) |
| Modified | [`Planarian/Planarian/Modules/Files/Services/FileService.cs`](../../Planarian/Planarian/Modules/Files/Services/FileService.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Data/CaveImportExecutionRepository.cs`](../../Planarian/Planarian/Modules/Import/Data/CaveImportExecutionRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Data/CaveImportPlanningRepository.cs`](../../Planarian/Planarian/Modules/Import/Data/CaveImportPlanningRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Data/EntranceImportExecutionRepository.cs`](../../Planarian/Planarian/Modules/Import/Data/EntranceImportExecutionRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Data/EntranceImportPlanningRepository.cs`](../../Planarian/Planarian/Modules/Import/Data/EntranceImportPlanningRepository.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Planning/CaveImportPlan.cs`](../../Planarian/Planarian/Modules/Import/Planning/CaveImportPlan.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Planning/CaveImportPlanner.cs`](../../Planarian/Planarian/Modules/Import/Planning/CaveImportPlanner.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Planning/EntranceImportPlan.cs`](../../Planarian/Planarian/Modules/Import/Planning/EntranceImportPlan.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Planning/EntranceImportPlanner.cs`](../../Planarian/Planarian/Modules/Import/Planning/EntranceImportPlanner.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Planning/ImportPlanConcurrencyException.cs`](../../Planarian/Planarian/Modules/Import/Planning/ImportPlanConcurrencyException.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Planning/ImportPlanningState.cs`](../../Planarian/Planarian/Modules/Import/Planning/ImportPlanningState.cs) |
| Added | [`Planarian/Planarian/Modules/Import/Planning/ImportTagResolver.cs`](../../Planarian/Planarian/Modules/Import/Planning/ImportTagResolver.cs) |
| Removed | `Planarian/Planarian/Modules/Import/Repositories/TemporaryEntranceRepository.cs` |
| Modified | [`Planarian/Planarian/Modules/PlanarianSettings/Repositories/PlanarianSettingsRepository.cs`](../../Planarian/Planarian/Modules/PlanarianSettings/Repositories/PlanarianSettingsRepository.cs) |
| Added | [`Planarian/Planarian/Modules/PlanarianSettings/Services/AccountFeatureSettingDefaults.cs`](../../Planarian/Planarian/Modules/PlanarianSettings/Services/AccountFeatureSettingDefaults.cs) |
| Modified | [`Planarian/Planarian/Modules/PlanarianSettings/Services/PlanarianSettingsService.cs`](../../Planarian/Planarian/Modules/PlanarianSettings/Services/PlanarianSettingsService.cs) |
| Added | [`Planarian/Planarian/Modules/Tags/Repositories/EligiblePeopleTagLookup.cs`](../../Planarian/Planarian/Modules/Tags/Repositories/EligiblePeopleTagLookup.cs) |
| Added | [`Planarian/Planarian/Modules/Tags/Repositories/TagReferenceLockRepository.cs`](../../Planarian/Planarian/Modules/Tags/Repositories/TagReferenceLockRepository.cs) |
| Modified | [`Planarian/Planarian/Modules/Tags/Repositories/TagRepository.cs`](../../Planarian/Planarian/Modules/Tags/Repositories/TagRepository.cs) |
| Modified | [`Planarian/Planarian/Modules/Users/Repositories/UserRepository.cs`](../../Planarian/Planarian/Modules/Users/Repositories/UserRepository.cs) |
| Added | [`Planarian/Planarian/Shared/Repositories/ThrottleEventLogRepository.cs`](../../Planarian/Planarian/Shared/Repositories/ThrottleEventLogRepository.cs) |
| Added | [`Planarian/Planarian/Shared/Services/AzureBlobObjectStorage.cs`](../../Planarian/Planarian/Shared/Services/AzureBlobObjectStorage.cs) |
| Modified | [`Planarian/Planarian/Shared/Services/FileResponsePolicy.cs`](../../Planarian/Planarian/Shared/Services/FileResponsePolicy.cs) |
| Added | [`Planarian/Planarian/Shared/Services/IObjectStorage.cs`](../../Planarian/Planarian/Shared/Services/IObjectStorage.cs) |
| Modified | [`Planarian/Planarian/Shared/Services/ThrottleEventLogService.cs`](../../Planarian/Planarian/Shared/Services/ThrottleEventLogService.cs) |

</details>

The inventory is pinned to the comparison SHA. Uncommitted or later branch work can legitimately add further files; use Git for the live delta.

## How to decide where code belongs

A useful rule when reading or adding code:

- **Controller**: HTTP translation only.
- **Service/coordinator**: authorize, orchestrate, choose use-case flow.
- **Pure planner/policy/model helper**: deterministic business transformation without persistence.
- **Repository**: materialize data, locks, transactions, set-based writes, provider-specific persistence.
- **Infrastructure adapter**: talk to Azure or another external provider.

If new service code begins owning DbContext, NpgsqlConnection, or provider-specific storage types, pause and compare it to the durable data-access contract. Existing File/archive code still contains legacy Azure-specific methods; that exception is documented, not a precedent for new Cave/File persistence work.
