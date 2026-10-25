# Cave publication

This page explains the supported interactive Cave publication path: what is being mutated, where concurrency is checked, and how one accepted user action becomes one revision.

The durable normative contract remains [Cave revision and proposal architecture](../cave-revisions.md).

## Aggregate boundary

An interactive Cave save is one desired-state operation over:

~~~text
Cave
├── scalar fields
├── tags
├── Entrances
├── Files
└── line plots (CaveGeoJson)
~~~

The server does not treat these as independent publications. A File-only or line-plot-only change is still a Cave mutation.

## Main components

- frontend EditCavePage / AddCaveComponent — author desired state;
- CaveFormMapper — carries the revision the user actually loaded into ExpectedRevisionId;
- CaveController — HTTP boundary;
- CaveService — authorization, normalization, aggregate orchestration;
- CaveMutationCoordinator — thin publication boundary;
- CaveMutationRepository — transaction, baseline/revision conflict, semantic revision publication;
- CavePublishedSnapshotRepository — canonical semantic snapshot;
- CaveRevisionDiffService — semantic comparison;
- CaveRepository — current aggregate persistence and locking helpers.

## Direct edit sequence

~~~mermaid
sequenceDiagram
    actor User
    participant UI as React Cave editor
    participant API as CaveController
    participant Service as CaveService
    participant Coord as CaveMutationCoordinator
    participant Mutation as CaveMutationRepository
    participant Current as CaveRepository
    participant Snapshot as CavePublishedSnapshotRepository
    participant DB as PostgreSQL

    User->>UI: Edit Cave loaded at revision R10
    UI->>API: PUT desired state + ExpectedRevisionId R10
    API->>Service: Save Cave
    Service->>Service: Authorize + normalize desired aggregate
    Service->>Coord: ExecuteTransactionAsync(work)
    Coord->>Mutation: ExecuteTransactionAsync(work)
    Mutation->>DB: Begin transaction if none exists
    Service->>Coord: PrepareExisting(R10)
    Coord->>Mutation: PrepareExisting(R10)
    Mutation->>DB: Verify current revision / establish baseline if needed
    Mutation->>Snapshot: Build live snapshot before mutation
    Service->>Current: Apply and save Cave aggregate
    Current->>DB: Persist Cave/tags/Entrances/Files/line plots
    Service->>Coord: PublishPrepared
    Coord->>Mutation: PublishPrepared
    Mutation->>Snapshot: Build accepted semantic snapshot
    alt semantic no-op
        Mutation-->>Service: No new revision
    else semantic change
        Mutation->>DB: Insert CaveRevision R11
        Mutation->>DB: Advance Cave.CurrentRevisionId to R11
    end
    Mutation->>DB: Commit owned transaction
    API-->>UI: Success
~~~

## Optimistic concurrency

For an existing manager edit:

- ExpectedRevisionId is required;
- the browser must send the revision it actually loaded;
- the server does not substitute the Cave's current revision for a missing token;
- mismatch raises CaveRevisionConflictException;
- EF/PostgreSQL xmin still protects races after the explicit revision check.

~~~mermaid
sequenceDiagram
    participant A as Editor A
    participant B as Editor B
    participant API as API
    participant DB as Cave

    A->>DB: Load R10
    B->>DB: Load R10
    A->>API: Save Expected R10
    API->>DB: Publish R11
    B->>API: Save Expected R10
    API->>DB: Current is R11
    API-->>B: Conflict; no mutation and no R12
~~~

## Baselines

Legacy Caves may have current relational state but no CurrentRevisionId.

Authoring context may call EnsureBaselineAsync after visibility/permission checks. CaveMutationRepository locks the Cave and creates a SystemBaseline revision exactly when needed.

A baseline is not supposed to be manufactured by ordinary detail/search/history reads merely because someone viewed a Cave.

## Snapshot semantics

CavePublishedSnapshotV1 contains the semantic published state needed for history and diffs:

- Cave scalar values;
- historical State/County labels;
- Cave/Entrance tags with historical names;
- Entrances;
- File identity and user-facing metadata;
- line-plot stable ID, historical name, and deterministic content hash.

It intentionally excludes:

- raw object-storage partition/key;
- full line-plot GeoJSON;
- transient editor/UI state.

CavePublishedSnapshotRepository builds snapshots using bounded projection groups instead of loading a massive tracked graph. The same snapshot shape is used by interactive and bulk publication, and both engines use `CaveRevisionDiffService` to decide semantic equality against the appropriate published baseline.

## Files during publication

Direct authoring may stage a File before Save. Before opening the Cave publication transaction the service verifies the required external object exists. Inside the relational transaction it revalidates/locks ownership and associates the staged File with the Cave.

Removing a previously published File records a RetainedCaveFileObject locator before deleting the live File row. The underlying object is not physically deleted by ordinary revisioned removal.

See [Files and storage](files-and-storage.md).

## Line plots during publication

Current line plots remain CaveGeoJson rows.

Stable identity rules:

- same ID + same content/name → unchanged;
- same ID + renamed → rename;
- same ID + new GeoJSON → content change;
- no ID → new line plot;
- omitted current ID → removal.

The published revision stores metadata/hash, while immutable proposal versions store exact proposed GeoJSON.

## Delete publication

Hard Cave deletion is also revisioned.

CaveService:

1. acquires the hard-delete lock;
2. verifies manager permission;
3. rejects deletion while unresolved proposals exist;
4. prepares the current revision/live snapshot;
5. removes the current aggregate and gathers object addresses;
6. writes a final Delete revision using the pre-delete snapshot;
7. commits PostgreSQL;
8. best-effort deletes physical objects after commit.

The final revision is a tombstone/history event and does not require the Cave row to remain live.

## Other users of the single-Cave publication engine

The aggregate editor is the supported normal interactive authoring surface, but `CaveMutationRepository` is intentionally reusable for other one-Cave workflows:

- Cave archive/unarchive uses `PublishExistingAsync`;
- hard delete uses prepare + `PublishPreparedDeleteAsync`;
- proposal approval reconstructs exact proposed desired state and uses CaveService inside the same mutation transaction;
- account File import uses `FileService.UploadCaveFile` / `PublishStagedCaveFile`, which publish a revision for the one target Cave.

That specialized File-import path is not the same as the Cave/Entrance CSV bulk-import architecture.

## Supported boundary and pre-merge deviations

The supported architecture requires the Cave detail page to be read-only for Cave-owned Files and line plots. Branch-specific deviations from that target are intentionally tracked only in the [completion status ledger](../../CAVE_REVISIONS_COMPLETION_STATUS.md), so this evergreen page does not become stale as review fixes land.

If a pre-merge source path still mutates Cave-owned state outside the aggregate editor, do not infer from its temporary existence that it is a second supported architecture.

## Read the code in this order

1. Planarian.Web/src/Modules/Caves/Pages/EditCavePage.tsx
2. Planarian.Web/src/Modules/Caves/Helpers/CaveFormMapper.ts
3. Planarian/Planarian/Modules/Caves/Controllers/CaveController.cs
4. Planarian/Planarian/Modules/Caves/Services/CaveService.cs
5. Planarian/Planarian/Modules/Caves/Revisions/CaveMutationWorkflow.cs
6. Planarian/Planarian/Modules/Caves/Revisions/CaveMutationRepository.cs
7. Planarian/Planarian/Modules/Caves/Revisions/CavePublishedSnapshotRepository.cs
8. Planarian/Planarian.Model/Database/Revisions/CavePublishedSnapshotV1.cs
