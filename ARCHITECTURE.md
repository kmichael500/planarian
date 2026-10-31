# Planarian Architecture

Planarian is a React/TypeScript single-page application backed by an ASP.NET Core API, PostgreSQL/PostGIS, and object storage. Cave publication, proposals, imports, and destructive operations share explicit persistence and concurrency boundaries so current state, history, and external File bytes remain consistent.

## System overview

~~~mermaid
flowchart LR
    UI["React SPA"]
    API["ASP.NET Core API"]
    Ops["Planarian.Operations"]

    Direct["Cave authoring / archive / delete"]
    Proposal["Proposal / review"]
    FileImport["File import"]
    BulkFlows["Cave/Entrance CSV import<br/>Tag/County administration"]

    Single["Single-Cave publication<br/>CaveMutationRepository"]
    Bulk["Bulk publication<br/>CaveBulkRevisionRepository"]

    DB[("PostgreSQL / PostGIS")]
    ObjectStorage["Object storage"]

    UI -->|"HTTPS"| API
    API -->|"interactive Cave mutation"| Direct
    API -->|"proposal workflow"| Proposal
    API -->|"File import"| FileImport
    API -->|"bulk/admin mutation"| BulkFlows

    Direct -->|"desired Cave state"| Single
    Proposal -->|"approved proposal version"| Single
    FileImport -->|"one-Cave File mutation"| Single
    BulkFlows -->|"before/after Cave state"| Bulk

    Single -->|"current state + revision"| DB
    Bulk -->|"current state + revisions"| DB
    API -->|"File bytes"| ObjectStorage
    Ops -->|"deployment-only database operations"| DB
~~~

## Cave state and history

| Concern | Authoritative representation |
| --- | --- |
| Current Cave state | `Cave` and current relational children |
| Published history | `CaveRevision` + `CavePublishedSnapshotV1` |
| Proposed state | `CaveProposalVersion` + `CaveProposalSnapshotV1` |
| Proposal workflow | `CaveChangeRequest` + review events |
| Current File metadata | `File` |
| External cleanup debt | `StorageCleanupTarget` |

Current Cave state is mutable relational data. Every accepted Cave publication also records an immutable semantic revision snapshot. Proposal versions are immutable future intent and do not become published state until approval.

## Core architecture rules

### Dependency direction

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

Application services orchestrate use cases. Repositories own provider-specific persistence, row locks, set-based SQL, and transaction mechanics. See [Data-access architecture](docs/data-access-architecture.md).

### Cave publication is aggregate publication

Interactive Cave authoring treats scalar Cave fields, Cave tags, Entrances and their tags, Files, and line plots as one desired state. One accepted aggregate mutation creates at most one `CaveRevision`; a semantic no-op creates none.

Direct edits use optimistic concurrency. The browser submits the revision it edited as `ExpectedRevisionId`; publication conflicts if the Cave has advanced.

See [Cave publication](docs/architecture/cave-publication.md).

### Proposals remain separate until approval

`CaveChangeRequest` owns workflow state. Each `CaveProposalVersion` stores immutable proposed state against a specific published revision, or no base revision for a new-Cave proposal. Approval publishes that exact proposal through the normal single-Cave publication boundary.

See [Proposal and review architecture](docs/architecture/proposals.md).

### Relational state and object storage have separate durability boundaries

PostgreSQL transactions cannot atomically include object storage. New Cave Files are written once to a stable application-assigned object address, represented relationally as staged, and later associated with a Cave without copying bytes. External deletion occurs only after the relational mutation that makes the object unreachable commits.

See [Files and storage](docs/architecture/files-and-storage.md).

### Bulk imports use a separate execution path

Cave and Entrance CSV imports parse and plan without mutating the database, then apply immutable plans with explicit locks and set-oriented writes. They publish revisions through `CaveBulkRevisionRepository` rather than invoking interactive Cave publication once per row.

Account File import is a one-Cave workflow and uses the single-Cave publication boundary.

See [Import architecture](docs/architecture/imports.md).

### Tenant scope is part of persistence correctness

Planarian is multi-account. Query-filter bypasses must remain explicitly account-qualified, and client-supplied identifiers never establish ownership or authorization by themselves.

See [Authorization](docs/authorization.md) and [Data-access architecture](docs/data-access-architecture.md).

### Destructive operations commit database intent before external cleanup

Published File removal, hard Cave deletion, sync-import deletion, and Account Reset persist the relational result and cleanup responsibility before deleting external bytes. A storage failure after commit leaves retryable cleanup debt rather than falsifying the database outcome.

See [Destructive operations](docs/architecture/destructive-operations.md).

## Architecture documents

- [Backend components](docs/architecture/backend-components.md)
- [Cave publication](docs/architecture/cave-publication.md)
- [Proposals](docs/architecture/proposals.md)
- [Files and storage](docs/architecture/files-and-storage.md)
- [Imports](docs/architecture/imports.md)
- [Destructive operations](docs/architecture/destructive-operations.md)
- [Data model](docs/architecture/data-model.md)
- [Authorization](docs/authorization.md)
- [Data-access architecture](docs/data-access-architecture.md)
- [Cave revisions and proposals](docs/cave-revisions.md)
- [Import compatibility](docs/import-compatibility.md)
- [Deployment](docs/deployment.md)
- [Testing](docs/testing.md)
- [Cave Revisions release status](CAVE_REVISIONS_COMPLETION_STATUS.md)

## Architecture decisions

- [ADR 0001 — Cave revision snapshots](docs/architecture/decisions/0001-cave-revision-snapshots.md)
- [ADR 0002 — Aggregate Cave publication](docs/architecture/decisions/0002-aggregate-cave-publication.md)
- [ADR 0003 — Immutable proposal versions](docs/architecture/decisions/0003-immutable-proposal-versions.md)
- [ADR 0004 — Upload-once File storage](docs/architecture/decisions/0004-upload-once-file-storage.md)
- [ADR 0005 — Bulk import publication](docs/architecture/decisions/0005-bulk-import-publication.md)
- [ADR 0006 — Cave asset removal cleanup](docs/architecture/decisions/0006-cave-asset-removal-cleanup.md)
