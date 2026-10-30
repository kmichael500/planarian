# Cave Revisions Completion Change Guide

**Purpose:** explain the large `feature/cave-revisions-completion` branch in human terms.
**Comparison used for this guide:** `main` at `e342691c82f1bb6549be903d3a8f94a5d2560ac2` → completion HEAD `33ace6e7afb099751de0f9df977a92c5beb9aa9a`.
**Branch distance at this comparison:** 156 commits ahead, 0 behind.
**Three-dot diff:** 440 files changed, 75,592 insertions, 5,654 deletions.

This is **not** the merge-readiness ledger. Current blockers, validation evidence, and unverified risks belong in [CAVE_REVISIONS_COMPLETION_STATUS.md](CAVE_REVISIONS_COMPLETION_STATUS.md).

This is also **not** the permanent architecture contract. Current architecture is explained from [ARCHITECTURE.md](ARCHITECTURE.md), with durable invariants in `docs/cave-revisions.md`, `docs/data-access-architecture.md`, and `docs/import-compatibility.md`.

## The shortest before/after explanation

Before this branch, Planarian primarily persisted the current Cave state. Cave edits, File changes, GeoJSON/line-plot changes, imports, and administrative mutations did not all share one durable semantic history model.

After this branch, accepted Cave state changes are designed around a revisioned aggregate:

~~~mermaid
flowchart LR
    subgraph Before["Before completion branch"]
        OldEditor["Cave edit"]
        OldFiles["File mutation"]
        OldGeo["GeoJSON mutation"]
        OldImport["Import"]
        OldDB[("Current relational state")]

        OldEditor -->|"writes"| OldDB
        OldFiles -->|"separate path"| OldDB
        OldGeo -->|"separate path"| OldDB
        OldImport -->|"large transactional mutation"| OldDB
    end

    subgraph After["Completion branch"]
        Editor["Aggregate Cave editor"]
        Proposal["Immutable proposal versions"]
        Import["Pure plan + bulk executor"]
        Publish["Shared revision semantics<br/>(single-Cave + bulk engines)"]
        Current[("Current relational state")]
        History[("Immutable Cave revisions")]

        Editor -->|"desired state"| Publish
        Proposal -->|"approved desired state"| Publish
        Import -->|"set-oriented before/after snapshots"| Publish
        Publish -->|"accepted current state"| Current
        Publish -->|"semantic snapshot"| History
    end
~~~

Under the hood, this is implemented by **two revision publication engines**: the single-Cave `CaveMutationRepository` path and the set-oriented `CaveBulkRevisionRepository` path. Proposal/review is a workflow that feeds the single-Cave publisher on approval; specialized account File import also uses the single-Cave publisher. Cave/Entrance CSV import and Tag/County remaps use the bulk publisher.

The most important change is therefore not one new screen or table. It is a new answer to:

> **What constitutes one accepted Cave change, how do we prevent stale writes, and how do we preserve what was actually published?**

## Major changes by area

| Area | Before on reviewed `main` | Completion branch | Why it matters |
| --- | --- | --- | --- |
| Published Cave history | No unified Cave revision chain representing accepted aggregate state | `CaveRevision` + versioned `CavePublishedSnapshotV1` + `CurrentRevisionId` | Gives one durable semantic history model |
| Direct edit concurrency | Existing edit could be based on stale browser state without a published-revision token | Existing manager edits require client-loaded `ExpectedRevisionId`; stale state conflicts | Prevents silent last-write-wins overwrite |
| Mutation boundary | Cave fields, Files, and GeoJSON had separate mutation paths | Supported target is one desired-state Cave aggregate | One save maps to at most one revision |
| Suggestions | No complete immutable multi-version proposal architecture | `CaveChangeRequest` + immutable `CaveProposalVersion` + review events | Reviewer can know exactly what was proposed |
| Proposal approval | N/A as current architecture | Approval publishes exact selected proposal through Cave aggregate publication | Avoids a second Cave-writing implementation |
| Files | `FileName` / optional `DisplayName`; storage lifecycle tied more directly to blob mechanics | `Name` + immutable `Extension`; staged authoring; upload-once publication; durable post-commit removal cleanup | Separates user metadata, current relationship, and byte lifetime |
| Object storage | Cave/File code more Azure-shaped | Cave/File publication uses `IObjectStorage` with `AzureBlobObjectStorage` adapter | Reduces provider leakage in new lifecycle |
| Removed published Files | Payload cleanup was not part of unified Cave revision semantics | Revision keeps File metadata; `StorageCleanupTarget(CaveAssetRemoval)` safely deletes bytes after commit | Files now match line plots: history records what existed without treating deleted payloads as permanent historical content |
| Line plots | `CaveGeoJson` current state; standalone replacement path | Still `CaveGeoJson` JSONB, but stable logical IDs participate in aggregate revisions/proposals | Adds history without forcing a GIS storage redesign |
| Line-plot history | No revision representation | Published revision stores ID/name/content hash; proposal stores exact GeoJSON | History stays bounded while proposal remains reproducible |
| Cave imports | One large service mixed parsing, planning, mutation, dry-run transaction behavior | Parser + planning repository + pure immutable planner + execution repository | Separates read-only preview from committed write path |
| Import dry run | Mutating transaction/rollback style | Ends after pure plan; no write transaction required | Dry run truly persists nothing |
| Import revisions | No shared accepted Cave revision model | Before/after snapshots + `CaveBulkRevisionRepository` | Import changes appear in the same Cave history |
| Import scale/concurrency | Bulk-oriented code existed but responsibilities were mixed | Explicit bounded batches, lock ordering, expected xmin/revision revalidation | Makes 10k-scale behavior testable and intentional |
| Tag/County administration | Mostly reference administration | Dedicated administration/lock repositories publish revisions where remaps change Cave semantics | Shared references cannot silently bypass history |
| Tenant safety | Existing account/query-filter model | Revision/proposal/import paths add fail-closed tenant filters and explicit account qualification | New history/workflow data stays account-scoped |
| Cave hard delete | Deleted current aggregate | Blocks unresolved proposals, writes Delete revision/tombstone, cleans external objects after commit | Preserves final history and safer cross-system ordering |
| Account reset | Relational + storage deletion existed | Large explicit reset graph includes revision/proposal/cleanup data; relational commit precedes external cleanup | New history/storage tables are included in purge semantics |
| Revision UI | No full revision/change-request UI | History, diff presentation, mine/review queues, suggest/revise/review pages | Makes architecture visible to users |
| Unsaved Cave authoring | Less complete protection around new asset-heavy forms | Shared unsaved-change protection for direct/proposal authoring | Staged asset changes are treated as real form state |
| PDF/file streaming | Existing viewer/download behavior | Request/throttle/file response work includes custom PDF stream session header and stronger response policy | Shared File serving behavior changed outside revision core |
| Test architecture | Less branch-specific integration infrastructure | Large real PostgreSQL/PostGIS integration suite, two-account adversarial tests, scale/golden/migration fixtures | The branch relies on behavioral evidence, not only unit wiring |

## 1. Revision foundation

### New concepts

The branch introduces:

- `CaveRevision`;
- `Cave.CurrentRevisionId`;
- `CavePublishedSnapshotV1`;
- `CaveRevisionSource`;
- `CaveRevisionOperation`;
- `CavePublishedSnapshotRepository`;
- `CaveRevisionQueryRepository`;
- `CaveRevisionService`;
- frontend revision history/diff models and components.

### What a revision means

A revision represents one accepted semantic published Cave state.

The snapshot includes Cave fields, tags, Entrances, Files, and line-plot fingerprints.

Shared reference labels are captured as they were at publication time so a later Tag/County rename does not rewrite old history.

### Why the current relational model still exists

The revision system does not replace the live normalized model.

Current reads/maps/search/editing still use the relational Cave/Entrance/File/CaveGeoJson data. Revisions are immutable history and concurrency provenance.

See [Cave publication](docs/architecture/cave-publication.md).

## 2. Direct Cave editing became explicitly revision-aware

Reviewed `main` had `CaveService.AddCave` mutate the current aggregate and save it, but there was no Cave revision token from the browser representing the exact state being edited.

Completion adds:

- `CurrentRevisionId` on loaded Cave state;
- `ExpectedRevisionId` on authoring submission;
- explicit rejection when an existing manager edit omits the token;
- `CaveRevisionConflictException`;
- xmin protection in addition to revision identity;
- semantic no-op suppression.

A browser editing R10 cannot silently publish over R11.

## 3. Cave-owned state moved toward one aggregate editor

The target mutation boundary now includes:

- scalar Cave data;
- Cave tags;
- Entrances and Entrance tags;
- Files;
- line plots.

New frontend pieces include:

- `CaveFileAuthoringEditor`;
- `CaveLinePlotAuthoringEditor`;
- shared asset cards/helpers;
- expanded `CaveFormMapper`;
- richer Add/Edit Cave form state.

The architectural intent is that the detail page **reads** current Cave-owned state and authorized mutation occurs through the aggregate editor.

### Known pre-merge exception

At the reviewed completion HEAD, an obsolete detail-page GeoJSON import/save path still survived. It was not a second supported architecture. Whether that pre-merge defect has since been fixed belongs in the [live status ledger](CAVE_REVISIONS_COMPLETION_STATUS.md), not this historical change guide.

## 4. Suggestions became an immutable proposal/review system

New server-side pieces include:

- `CaveChangeRequest`;
- `CaveProposalVersion`;
- `CaveChangeRequestReviewEvent`;
- `CaveChangeRequestStagedFile`;
- `CaveChangeRequestService`;
- `CaveChangeRequestRepository`;
- `CaveProposalSnapshotV1`;
- state/conflict policies.

New frontend flows include:

- Suggest Cave Changes;
- My/Review change-request lists;
- request detail;
- revision/proposal diff;
- Request Changes;
- Revise;
- Resubmit;
- Approve;
- Reject;
- stale-context presentation.

The important architectural rule is:

> A new edit to a pending proposal does not mutate an old version. It creates another immutable proposal version.

Approval verifies both the expected proposal version and the published Cave base before publishing.

See [Proposals](docs/architecture/proposals.md).

## 5. File identity and byte lifecycle changed substantially

Reviewed `main` used a File model with:

- `FileName`;
- optional `DisplayName`;
- optional Account ownership.

Completion changes the current model to:

- required Account ownership;
- `Name`;
- `Extension`;
- explicit filename/extension persistence validation;
- staged authoring Files;
- stable application-assigned object address;
- upload-once publication.

For the new Cave authoring path:

~~~text
upload
  → File ID allocated
  → object written once to objects/files/{fileId}
  → staged File row (CaveId null, ExpiresOn set)
  → Save/approval associates same File/object with Cave
~~~

A File removed from current Cave state is no longer retained as historical payload. The revision preserves its ID/name/type metadata; the removal transaction queues its object address as `StorageCleanupTarget(CaveAssetRemoval)`, and physical deletion runs only after commit. A failed delete leaves retryable cleanup debt.

See [Files and storage](docs/architecture/files-and-storage.md).

## 6. A provider-neutral storage boundary was added, but deliberately not everywhere

New:

- `IObjectStorage`;
- `StorageObjectAddress`;
- `StoredObjectReadResult`;
- `AzureBlobObjectStorage`.

The important scope is **Cave/File publication lifecycle**.

Do not read this branch as a repository-wide Azure removal. Archive and some other legacy storage paths still use Azure-specific Blob APIs.

That scoping is intentional and is now documented explicitly.

## 7. Line plots gained revision/proposal semantics without a storage rewrite

This branch deliberately keeps:

~~~text
CaveGeoJson
    Id
    CaveId
    Name
    GeoJson jsonb
~~~

What changes is lifecycle and history:

- IDs are stable across rename/content replacement;
- aggregate Cave save can add/update/remove line plots;
- published revisions record ID/name/SHA-256 content fingerprint;
- proposal versions preserve exact proposed normalized GeoJSON;
- line-plot-only changes count as real Cave changes.

What this branch **does not** do:

- PostGIS feature-row redesign;
- MVT line-plot rendering;
- immutable LinePlotVersion table;
- GeoJSON recovery/rollback subsystem.

## 8. Import architecture was substantially decomposed

Reviewed `main` Cave import had one large service flow that:

- parsed CSV;
- resolved/created references;
- mutated relational state;
- used a transaction for the whole flow;
- used rollback mechanics for dry run.

Completion replaces that architecture with:

~~~text
CSV
 ↓
typed parser
 ↓
planning repository loads account state
 ↓
pure planner
 ↓
immutable import plan
 ├─→ dry-run preview (stop)
 └─→ execution repository
        ↓
      lock + revalidate
        ↓
      set-oriented writes
        ↓
      before/after snapshots
        ↓
      bulk revision publication
        ↓
      commit
        ↓
      post-commit external cleanup
~~~

This is one of the largest backend architectural changes on the branch. It applies specifically to Cave/Entrance CSV import. Account File import remains a separate per-Cave workflow that calls FileService and the single-Cave mutation publisher.

See [Imports](docs/architecture/imports.md).

## 9. Bulk publication is separate on purpose

The branch introduces `CaveBulkRevisionRepository`.

It is not a duplicate accidental revision service.

Interactive publication and bulk publication share:

- CavePublishedSnapshotV1;
- CurrentRevisionId semantics;
- expected current revision;
- tenant scope;
- semantic no-op behavior;
- relational/revision atomicity.

They differ in execution because imports/admin operations may affect thousands of Caves.

## 10. Tag and County administration now participates in Cave-history correctness

New dedicated repositories include:

- `TagTypeAdministrationRepository`;
- `TagReferenceLockRepository`;
- `CountyAdministrationRepository`;
- `CountyReferenceLockRepository`;
- `EligiblePeopleTagLookup`.

Why these exist:

A TagType or County operation can race with a Cave publication or change shared reference metadata used by many Caves. The branch makes those operations participate in a deterministic lock hierarchy and publish revisions whenever the serialized published state of affected Caves changes.

Shared TagType and County label changes publish normal `ManagerEdit` revisions for every affected Cave in the same transaction; stable reference IDs remain unchanged, and older revisions retain their historical labels.

## 11. Tenant and authorization behavior was hardened around the new workflow

Revision/proposal/import entities add account-qualified relationships and query filtering.

Important branch rules include:

- guessed foreign-account IDs do not grant access;
- explicit `IgnoreQueryFilters()` paths require account qualification;
- current Cave visibility remains required for proposal/revision workflow access;
- current View permission is sufficient to propose;
- current Manager permission is required to review/publish;
- platform PlanarianAdmin status does not automatically imply account/Cave permissions.

The branch adds two-account adversarial integration coverage around these rules.

## 12. Hard deletion now has revision/workflow/storage semantics

A Cave hard delete now:

1. locks the Cave;
2. checks Manager authority;
3. refuses deletion while an unresolved request exists;
4. captures current revision/live snapshot;
5. deletes current aggregate state;
6. writes a Delete revision;
7. commits PostgreSQL;
8. deletes current external objects after commit.

Sync-import deletion follows equivalent principles in a bulk executor.

See [Destructive operations](docs/architecture/destructive-operations.md).

## 13. Account reset had to learn about the new graph

The completion branch adds durable data that Reset Account must remove:

- revision history;
- change requests;
- proposal versions;
- review events;
- request staged-file associations;
- storage-cleanup debt;
- import revision provenance.

`AccountRepository.ExecuteResetAsync` now explicitly removes that graph inside the relational reset transaction.

External File cleanup is after commit.

### Reset behaviors that deserve special attention

At the compared implementation state, two source behaviors are important when reasoning about reset safety:

- relational reset commits before the final account-container cleanup, so a later storage failure can produce a partial-success result;
- deleting the whole account container also removes recent server-side archives stored under its `archives/` prefix.

These are not intended as generic architecture rules. Their current validation/product-decision status belongs in the [live status ledger](CAVE_REVISIONS_COMPLETION_STATUS.md).

## 14. Revision/proposal UI is a new product surface

The completion branch adds or substantially changes:

- history modal;
- text/structured revision diff;
- line-plot/File change presentation;
- Suggest Cave Changes page;
- Revise Change Request page;
- request detail/review page;
- Mine/Review queues;
- stale proposal context;
- File preview/download within proposal review;
- unsaved authoring guards.

This UI is backed by immutable server-side history rather than client-only change descriptions.

## 15. File viewing / PDF / throttling changed outside the revision core

The branch also reaches shared File delivery and throttling:

- `FileResponsePolicy`;
- `RequestThrottleService`;
- `ThrottleEventLogRepository`;
- PDF viewer request handling;
- custom `X-Planarian-File-Stream-Session` header;
- authenticated File range/stream response behavior.

These are architecturally adjacent because proposal/current File viewing depends on the same serving infrastructure, but they are not part of Cave revision persistence itself.

The status ledger recommends a small real-browser PDF/range/CORS smoke test because unit tests cannot prove the entire browser/API chain.

## 16. Cross-cutting EF ownership checks were refactored

The global SaveChanges ownership/tenant validation path now batch-validates current and original Cave/County/Entrance parent ownership when those relationships are introduced, changed, or independently carry ownership, rather than performing unbounded per-association lookups. Modified Cave rows whose `CountyId` is unchanged rely on their separately validated current/original `AccountId` and do not issue a redundant County ownership query; an actual County move still validates both relationship endpoints. Validation is side-effect-free: it does not repopulate EF navigation properties while `SaveChanges` is running. A mechanical coverage contract classifies every `EntityBase` family so current Cave/account-domain entities cannot silently fall through the interceptor switch.

This is shared infrastructure, not merely Cave-revision code. The compatibility audit found and fixed missing tenant validation for Cave change-request review events, Cave GeoJSON, Cave permissions, Favorites, and User permissions; it also narrowed Added `AccountUser` handling so cross-account membership creation requires either a verified pending invitation for the current user or platform-admin creation of a brand-new Account. Verified invitation claims are correlated by Account, normalized email, and the copied invitation-sent timestamp; if a successfully submitted invitation could not persist that optional timestamp, a null timestamp is accepted only when the pending Account/email/null-timestamp match resolves to exactly one temporary invitee. Ambiguous matches fail closed. Permission transfer is allowed only from that exact temporary invitee. Invitation cleanup remains deletion-only. Global Permission definitions, global `UserPermission` and `FeatureSetting` rows, and global/default `TagType` mutations now require PlanarianAdmin. An authenticated request with no selected Account also no longer gains blanket authority to mutate arbitrary `User` rows; self, same-selected-Account, and verified temporary-invitation transitions remain the explicit allowed cases.

Real PostgreSQL coverage now exercises foreign-tenant Added/Modified/Deleted states, pre-populated and missing parent navigations, mixed-batch atomicity, direct AccountId/Cave/County/Entrance/File/staged-file ownership, current-vs-original AccountId and parent-FK laundering, special user lifecycle paths, verified invitation/permission-transfer boundaries, Permission-definition and global UserPermission/FeatureSetting/default-TagType protections, relationship-ID smuggling, and bounded ownership-query counts.

## 17. Migration surface is large

The branch adds revision/proposal/import/File-lifecycle schema after current-main v32. The published v32 migration remains unchanged and intentional data transformations are explicit. The completion migration fails closed when a Cave-owned File's account disagrees with its owning Cave, and it keeps a small migration-owned compatibility table only for legacy `FileName` values that cannot be reconstructed from `Name + Extension`. The Entrance Other backfill separately records the exact rows it inserts so downgrade cannot mistake a preexisting deterministic-ID setting for migration-owned data. PostgreSQL migration tests are grouped by behavior and cover current-main upgrade, normalized v32 schema-contract round-trip, populated-new-schema downgrade, filename transformation/rollback branches, File ownership states, FeatureSetting provenance, historical compatibility, and downgrade back to the v32 schema contract. Deliberately removed legacy reporter-user values are not treated as reversible data.

Schema migration and revision-history initialization are intentionally separate. After migrations, a one-time bounded `Planarian.Operations` bootstrap creates a `SystemBaseline/Baseline` revision for every Cave that already exists, using the canonical snapshot builder. All rollout entries share one boundary timestamp and no user actor. The coordinated deployment keeps the old API quiesced and will not deploy/start the new API until that operation succeeds; normal request/import paths contain no rollout logic and cannot create a late baseline.

See the migration section of the status ledger for current validation evidence. This change guide records the compared branch delta and intentionally does not own live finding status.

## 18. The test suite changed almost as much as the implementation

New coverage includes:

- revision V1 contract/diff unit tests;
- proposal state/version/concurrency integration tests;
- staged File publication/cleanup tests;
- direct-edit tag/People/reference-lock tests;
- two-account tenant adversarial suites;
- import planner/parser unit tests;
- import dry-run/preview/commit/rollback tests;
- golden compatibility fixtures;
- 10k Cave import scale validation;
- migration upgrade infrastructure;
- frontend authoring/revision/review tests.

At the reviewed HEAD, the previously completed broad validation recorded:

- 406 backend unit tests passing;
- 322 PostgreSQL/PostGIS integration tests passing;
- 326 frontend tests across 71 suites passing.

Those numbers are evidence for that reviewed SHA, not a promise that later working-tree changes are green. Use the status ledger for exact validation provenance.

## Important explicit non-goals

This branch intentionally does **not** introduce:

- PostGIS/MVT line-plot storage/rendering redesign;
- immutable line-plot version tables;
- a generic historical Cave rollback engine;
- a GeoJSON recovery archive;
- a generic CRUD repository/unit-of-work framework;
- a repository-wide Azure Blob abstraction rewrite;
- per-Cave interactive orchestration for imports;
- the assumption that account Archive is automatically a full disaster-recovery backup.

## New architecture-level components to recognize

If you see these names, they are central to the branch rather than incidental helpers:

### Published revisions

- CaveRevision
- CavePublishedSnapshotV1
- CaveMutationCoordinator
- CaveMutationRepository
- CavePublishedSnapshotRepository
- CaveRevisionService
- CaveRevisionQueryRepository
- CaveBulkRevisionRepository

### Proposals

- CaveChangeRequest
- CaveProposalVersion
- CaveChangeRequestReviewEvent
- CaveProposalSnapshotV1
- CaveChangeRequestService
- CaveChangeRequestRepository

### Files

- IObjectStorage
- AzureBlobObjectStorage
- StorageCleanupTarget
- StagedCaveFilePublication

### Imports

- CaveImportPlanningRepository / EntranceImportPlanningRepository
- CaveImportPlanner / EntranceImportPlanner
- CaveImportPlan / EntranceImportPlan
- CaveImportExecutionRepository / EntranceImportExecutionRepository

### Reference administration

- TagTypeAdministrationRepository
- TagReferenceLockRepository
- CountyAdministrationRepository
- CountyReferenceLockRepository
- EligiblePeopleTagLookup

For responsibility details, use [the backend component catalog](docs/architecture/backend-components.md).

## Easiest walkthrough of the branch

Do **not** start with the 440-file diff.

Use these six slices.

### Slice A — Direct publication

Goal: understand the new core primitive.

Read:

1. [Cave publication architecture](docs/architecture/cave-publication.md)
2. CaveFormMapper
3. CaveService.SaveCaveAsync
4. CaveMutationCoordinator
5. CaveMutationRepository
6. CavePublishedSnapshotRepository
7. CavePublishedSnapshotV1

Once this makes sense, most of the branch becomes easier.

### Slice B — Proposals

Goal: understand why suggestions are not just another Cave DTO.

Read:

1. [Proposal architecture](docs/architecture/proposals.md)
2. CaveChangeRequest entity/state policy
3. CaveProposalSnapshotV1
4. CaveChangeRequestService.ApproveAsync
5. CaveChangeRequestRepository

### Slice C — Files

Goal: understand external byte lifetime separately from relational state.

Read:

1. [Files and storage](docs/architecture/files-and-storage.md)
2. FileService.StageAuthoringFile
3. IObjectStorage / AzureBlobObjectStorage
4. File entity
5. StorageCleanupTarget
6. File portion of CaveService.SaveCaveAsync

### Slice D — Line plots

Goal: understand what changed without assuming a GIS rewrite.

Read:

1. line-plot sections in [Cave publication](docs/architecture/cave-publication.md)
2. CaveGeoJson entity
3. CaveJsonContent
4. CaveLinePlotAuthoringEditor
5. line-plot fields in published/proposal snapshot V1.

### Slice E — Imports

Goal: understand the bulk publication engine used by Cave/Entrance CSV import.

Read:

1. [Import architecture](docs/architecture/imports.md)
2. parser;
3. planning repository;
4. pure planner;
5. immutable plan;
6. execution repository;
7. CaveBulkRevisionRepository.

### Slice F — Destruction/admin

Goal: understand data-loss boundaries.

Read:

1. [Destructive operations](docs/architecture/destructive-operations.md)
2. CaveService.DeleteCave;
3. AccountService.ResetAccount;
4. AccountRepository.ExecuteResetAsync;
5. TagType/County administration repositories;
6. status-ledger P0/P1 destructive validation gaps.

## What to read when a design choice feels strange

Use the ADRs:

- [Why snapshots instead of a mutation log?](docs/architecture/decisions/0001-cave-revision-snapshots.md)
- [Why one aggregate Cave save?](docs/architecture/decisions/0002-aggregate-cave-publication.md)
- [Why immutable proposal versions?](docs/architecture/decisions/0003-immutable-proposal-versions.md)
- [Why upload Files once?](docs/architecture/decisions/0004-upload-once-file-storage.md)
- [Why was retained-File storage superseded?](docs/architecture/decisions/0005-retained-file-objects.md)
- [Why remove Cave asset payloads after publication?](docs/architecture/decisions/0007-cave-asset-removal-cleanup.md)
- [Why do imports have a separate publisher?](docs/architecture/decisions/0006-bulk-import-publication.md)

## What remains unfinished or not fully validated

Do not infer “documented architecture” means “branch is merge-ready.”

This change guide deliberately does **not** duplicate the live blocker/gap list, because that list changes as review fixes land and would create a second status source that can become stale. Use [CAVE_REVISIONS_COMPLETION_STATUS.md](CAVE_REVISIONS_COMPLETION_STATUS.md) as the authoritative SHA-pinned review ledger for current blockers, resolved findings, validation gaps, and test evidence.

## Keeping this guide useful

This file is intentionally branch/historical documentation.

After the feature merges:

- keep the evergreen architecture under `ARCHITECTURE.md` and `docs/architecture/` current;
- keep ADRs as historical decision records;
- either retain this guide as the historical migration/change explanation or archive it with other completed implementation material;
- do not copy future current-state rules into this document and let them diverge from the durable architecture docs.
