# Planarian Architecture

This is the **start-here map** for understanding Planarian's architecture and, in particular, the large Cave-revisions completion work.

It is intentionally a navigation and explanation document rather than a complete specification. Follow the links below when you need implementation-level detail.

## How to use these docs

The documentation is arranged by zoom level:

1. **Start here** — understand the major runtime pieces and architectural rules.
2. **Follow a workflow** — direct Cave edit, proposal approval, Files, imports, or destructive operations.
3. **Use the component catalog** — find the service/repository that owns a responsibility.
4. **Read the durable contract** — use the existing domain-specific documents for exact invariants.
5. **Read ADRs when asking "why?"** — decision records explain the major choices and tradeoffs.
6. **Use the completion change guide when asking "what changed on this branch?"** — it compares the completion branch with main.

The diagrams are C4-inspired rather than strict C4 notation. They use ordinary Mermaid so GitHub can render them directly.

## The shortest mental model

Planarian is primarily a React single-page application talking to an ASP.NET Core API. The API stores relational/geospatial data in PostgreSQL/PostGIS and stores large File bytes in object storage. Deployment-only database operations are kept outside the web host: the small `Planarian.Operations` console executable composes explicit operational commands from shared persistence libraries and a scoped database connection, without starting the API or loading its hosted-service configuration.

For Cave changes, the central idea is:

> **Current Cave state is mutable relational data, but every accepted Cave publication also produces an immutable semantic revision snapshot.**

There are **two revision publication engines**, plus workflows that feed them:

- **Single-Cave publication** — `CaveMutationCoordinator` delegates to `CaveMutationRepository`. It is used by aggregate direct edits, proposal approval, archive/unarchive, hard delete, and specialized File-import publication.
- **Bulk publication** — `CaveBulkRevisionRepository` is used when Cave/Entrance CSV imports or Tag/County administration can affect many Caves and must stay set-oriented.
- **Proposal/review is a workflow, not a third publisher.** It stores immutable proposed state; approval sends that exact state through the single-Cave publication engine.

**Diagram notation:** rectangles are workflows/components, cylinders are datastores, and every arrow says what crosses the boundary.

~~~mermaid
flowchart LR
    UI["React SPA"]
    API["ASP.NET Core API"]

    Direct["Aggregate edit / archive / delete"]
    Proposal["Proposal / review"]
    FileImport["Specialized File import"]
    BulkFlows["Cave/Entrance CSV import<br/>Tag/County remap"]

    Single["Single-Cave publisher<br/>CaveMutationRepository"]
    Bulk["Bulk publisher<br/>CaveBulkRevisionRepository"]

    DB[("PostgreSQL / PostGIS")]
    ObjectStorage["Object storage"]

    UI -->|"HTTPS requests"| API
    API -->|"interactive Cave actions"| Direct
    API -->|"suggest / review"| Proposal
    API -->|"File import"| FileImport
    API -->|"bulk/admin actions"| BulkFlows

    Direct -->|"one-Cave desired mutation"| Single
    Proposal -->|"approved immutable proposal"| Single
    FileImport -->|"revision-aware File mutation"| Single
    BulkFlows -->|"before/after Cave snapshots"| Bulk

    Single -->|"current state + one revision"| DB
    Bulk -->|"current state + per-Cave revisions"| DB
    API -->|"File bytes"| ObjectStorage
~~~

### Do not confuse these representations

| Concept | What it is | Source of truth |
| --- | --- | --- |
| Current Cave state | Mutable normalized application data | Cave + relational children in PostgreSQL |
| Published history | Immutable semantic record of accepted state | CaveRevision + CavePublishedSnapshotV1 |
| Proposed future state | Immutable version of a suggestion awaiting workflow action | CaveProposalVersion + CaveProposalSnapshotV1 |
| Proposal workflow | Mutable status/pointer around immutable proposal versions | CaveChangeRequest + review events |
| Removed Cave asset history | Lightweight semantic revision metadata; removed File bytes are post-commit cleanup | CaveRevision snapshots + StorageCleanupTarget for transient external cleanup debt |

## Architecture rules worth remembering

### 1. Dependency direction

The durable backend direction is:

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

Application services orchestrate intent. Repositories own provider-specific persistence algorithms, row locks, set-based SQL, and transaction details.

See [Data-access architecture](docs/data-access-architecture.md).

### 2. A Cave edit is an aggregate operation

One interactive Cave desired state includes:

- scalar Cave fields;
- Cave tags;
- Entrances and their tags;
- Files;
- line plots.

One accepted aggregate mutation creates **at most one** CaveRevision. A semantic no-op creates none.

See [Cave publication](docs/architecture/cave-publication.md).

### 3. Existing direct edits are optimistic

The browser edits revision Rn and sends that exact revision as ExpectedRevisionId. If the Cave has since advanced, the request conflicts rather than silently overwriting newer state.

### 4. Proposals are not published state

A CaveChangeRequest is a workflow envelope. Each CaveProposalVersion is immutable and records the exact proposed state against a specific published revision. Approval is the point where published Cave state changes.

See [Proposal and review architecture](docs/architecture/proposals.md).

### 5. File storage and relational state have different durability boundaries

PostgreSQL transactions cannot atomically include Azure Blob Storage. New Cave Files are therefore uploaded once to a stable application-assigned object address that this lifecycle treats as immutable, represented relationally as staged, and later associated with a Cave without copying the bytes.

Destructive object deletion is deferred until after relational commit.

See [Files and storage](docs/architecture/files-and-storage.md).

### 6. Imports are deliberately not interactive Cave edits in a loop

Large Cave/Entrance **CSV** imports parse and plan without database mutation, then execute an immutable plan in set-oriented batches with explicit locks and revision publication. Account File import is a separate workflow and uses the single-Cave publisher.

See [Import architecture](docs/architecture/imports.md).

### 7. Tenant scope is part of persistence correctness

Planarian is multi-account. Explicit query-filter bypasses must be account-qualified. Cross-account identifiers are never trusted merely because the client supplied them.

See [Authorization](docs/authorization.md) and [Data-access architecture](docs/data-access-architecture.md).

## Recommended code tour

If you only have 15 minutes, read [System overview](docs/architecture/system-overview.md), then the direct-edit sequence in [Cave publication](docs/architecture/cave-publication.md), then skim [Proposals](docs/architecture/proposals.md), [Files and storage](docs/architecture/files-and-storage.md), and [Imports](docs/architecture/imports.md). That gives you the conceptual model before you open implementation files.

If the completion branch feels overwhelming, read the source in this order.

### Tour 1 — Direct Cave edit

1. [EditCavePage.tsx](Planarian.Web/src/Modules/Caves/Pages/EditCavePage.tsx)
2. [AddCaveComponent.tsx](Planarian.Web/src/Modules/Caves/Components/AddCaveComponent.tsx)
3. [CaveFormMapper.ts](Planarian.Web/src/Modules/Caves/Helpers/CaveFormMapper.ts)
4. [frontend CaveService.ts](Planarian.Web/src/Modules/Caves/Service/CaveService.ts)
5. [CaveController.cs](Planarian/Planarian/Modules/Caves/Controllers/CaveController.cs)
6. [backend CaveService.cs](Planarian/Planarian/Modules/Caves/Services/CaveService.cs)
7. [CaveMutationWorkflow.cs](Planarian/Planarian/Modules/Caves/Revisions/CaveMutationWorkflow.cs)
8. [CaveMutationRepository.cs](Planarian/Planarian/Modules/Caves/Revisions/CaveMutationRepository.cs)
9. [CavePublishedSnapshotRepository.cs](Planarian/Planarian.Library/Caves/Revisions/CavePublishedSnapshotRepository.cs)
10. [CaveRevision.cs](Planarian/Planarian.Model/Database/Entities/RidgeWalker/CaveRevision.cs)

Read [Cave publication](docs/architecture/cave-publication.md) alongside this tour.

### Tour 2 — Proposal and approval

1. [SuggestCaveChangesPage.tsx](Planarian.Web/src/Modules/Caves/Pages/SuggestCaveChangesPage.tsx)
2. [CaveChangeRequestPage.tsx](Planarian.Web/src/Modules/Caves/Pages/CaveChangeRequestPage.tsx)
3. [CaveChangeRequestController.cs](Planarian/Planarian/Modules/Caves/Controllers/CaveChangeRequestController.cs)
4. [CaveChangeRequestService.cs](Planarian/Planarian/Modules/Caves/Services/CaveChangeRequestService.cs)
5. [CaveChangeRequestRepository.cs](Planarian/Planarian/Modules/Caves/Revisions/CaveChangeRequestRepository.cs)
6. [CaveChangeRequest.cs](Planarian/Planarian.Model/Database/Entities/RidgeWalker/CaveChangeRequest.cs)
7. [CaveProposalSnapshotV1.cs](Planarian/Planarian.Model/Database/Revisions/CaveProposalSnapshotV1.cs)
8. return to CaveService.SaveCaveAsync for approval publication.

Read [Proposals](docs/architecture/proposals.md).

### Tour 3 — File lifecycle

1. [CaveFileAuthoringEditor.tsx](Planarian.Web/src/Modules/Caves/Components/CaveFileAuthoringEditor.tsx)
2. [FileController.cs](Planarian/Planarian/Modules/Files/Controllers/FileController.cs)
3. [FileService.cs](Planarian/Planarian/Modules/Files/Services/FileService.cs)
4. [IObjectStorage.cs](Planarian/Planarian/Shared/Services/IObjectStorage.cs)
5. [AzureBlobObjectStorage.cs](Planarian/Planarian/Shared/Services/AzureBlobObjectStorage.cs)
6. [File.cs](Planarian/Planarian.Model/Database/Entities/RidgeWalker/File.cs)
7. [StorageCleanupTarget.cs](Planarian/Planarian.Model/Database/Entities/RidgeWalker/StorageCleanupTarget.cs)
8. the File portion of CaveService.SaveCaveAsync.

Then, if you need to understand account File import, inspect `FileService.UploadCaveFile` / `PublishStagedCaveFile`; those are specialized one-Cave publication paths rather than the aggregate editor.

Read [Files and storage](docs/architecture/files-and-storage.md).

### Tour 4 — Imports

1. [ImportService.Caves.cs](Planarian/Planarian/Modules/Account/Import/Services/ImportService.Caves.cs)
2. [ImportCsvParsers.cs](Planarian/Planarian/Modules/Import/Parsing/ImportCsvParsers.cs)
3. [CaveImportPlanningRepository.cs](Planarian/Planarian/Modules/Import/Data/CaveImportPlanningRepository.cs)
4. [CaveImportPlanner.cs](Planarian/Planarian/Modules/Import/Planning/CaveImportPlanner.cs)
5. [CaveImportPlan.cs](Planarian/Planarian/Modules/Import/Planning/CaveImportPlan.cs)
6. [CaveImportExecutionRepository.cs](Planarian/Planarian/Modules/Import/Data/CaveImportExecutionRepository.cs)
7. [CaveBulkRevisionRepository.cs](Planarian/Planarian/Modules/Caves/Revisions/CaveBulkRevisionRepository.cs)

Read [Imports](docs/architecture/imports.md).

### Tour 5 — Destructive operations

Read:

- `DeleteCave` in [CaveService.cs](Planarian/Planarian/Modules/Caves/Services/CaveService.cs);
- `DeleteAggregateForHardDeleteAsync` in [CaveRepository.cs](Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs);
- `ResetAccount` in [AccountService.cs](Planarian/Planarian/Modules/Account/Services/AccountService.cs);
- `ExecuteResetAsync` in [AccountRepository.cs](Planarian/Planarian/Modules/Account/Repositories/AccountRepository.cs);
- [ArchiveJobCoordinator.cs](Planarian/Planarian/Modules/Account/Archive/Services/ArchiveJobCoordinator.cs).

Then read [Destructive operations](docs/architecture/destructive-operations.md).

## Architecture document map

- [System overview](docs/architecture/system-overview.md) — runtime/container view.
- [Backend components](docs/architecture/backend-components.md) — service and repository catalog.
- [Cave publication](docs/architecture/cave-publication.md) — direct-edit transaction and revision sequence.
- [Proposals](docs/architecture/proposals.md) — proposal state and approval sequence.
- [Files and storage](docs/architecture/files-and-storage.md) — staged/published/retained lifecycle.
- [Imports](docs/architecture/imports.md) — parser/planner/executor/bulk revision model.
- [Destructive operations](docs/architecture/destructive-operations.md) — hard delete, reset, and external cleanup.
- [Data model](docs/architecture/data-model.md) — important revision/proposal/File relationships.
- [Completion branch change guide](CAVE_REVISIONS_COMPLETION_CHANGE_GUIDE.md) — what changed relative to main.
- [Completion status ledger](CAVE_REVISIONS_COMPLETION_STATUS.md) — what has been validated and what still needs work.

## Durable contracts

These documents remain the canonical detailed contracts for their subjects:

- [Authorization](docs/authorization.md)
- [Data-access architecture](docs/data-access-architecture.md)
- [Cave revisions and proposals](docs/cave-revisions.md)
- [Import compatibility](docs/import-compatibility.md)
- [Testing](docs/testing.md)

The documents under docs/architecture/ explain and navigate those contracts; they do not override them.

## Architecture decision records

The decision records capture major choices that would otherwise be easy to rediscover incorrectly:

- [ADR 0001 — Cave revision snapshots](docs/architecture/decisions/0001-cave-revision-snapshots.md)
- [ADR 0002 — Aggregate Cave publication](docs/architecture/decisions/0002-aggregate-cave-publication.md)
- [ADR 0003 — Immutable proposal versions](docs/architecture/decisions/0003-immutable-proposal-versions.md)
- [ADR 0004 — Upload-once File storage](docs/architecture/decisions/0004-upload-once-file-storage.md)
- [ADR 0005 — Retained File objects (superseded)](docs/architecture/decisions/0005-retained-file-objects.md)
- [ADR 0006 — Bulk import publication](docs/architecture/decisions/0006-bulk-import-publication.md)
- [ADR 0007 — Cave asset removal cleanup](docs/architecture/decisions/0007-cave-asset-removal-cleanup.md)

ADRs describe why a decision was made. If a decision changes later, preserve the old record and supersede it rather than rewriting history.

## Documentation maintenance rules

To keep this package useful:

- diagrams should show architecture-level responsibilities, not every class;
- every relationship arrow should state what flows across it;
- source paths should be used instead of fragile line numbers;
- current-state docs should describe what the code does now;
- branch-specific before/after history belongs in the completion change guide, not in evergreen architecture docs;
- detailed invariants should live in one canonical durable contract and be linked rather than copied repeatedly;
- component diagrams should be updated when responsibilities move substantially;
- code-level diagrams should generally be generated on demand rather than maintained by hand.

These conventions follow the spirit of the C4 model, Architecture Decision Records, docs-as-code, and the separation between explanation and reference described by Diátaxis.
