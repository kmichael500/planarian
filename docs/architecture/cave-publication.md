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
    Mutation->>DB: Verify required current revision
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

## Revision-history rollout boundary

Every Cave that already exists when revision history is enabled receives exactly one system-generated rollout revision before the new API serves requests. That revision uses `Source = SystemBaseline` and `Operation = Baseline`; its canonical snapshot records the Cave exactly as it existed at the rollout boundary. It does **not** mean the Cave was created at that time or that no earlier changes occurred.

The rollout is a deliberate deployment step after the schema migrations. It is account-aware, bounded, resumable, idempotent, and uses the same `CavePublishedSnapshotRepository` projection as normal publication. All rollout revisions share one history-boundary timestamp and have no user actor. The coordinated deployment keeps the API stopped until the operation completes and verifies that no live Cave has `CurrentRevisionId = null`; normal web startup contains no deployment/rollout command path.

After rollout, an existing Cave without `CurrentRevisionId` is an integrity/deployment error. Ordinary reads, authoring-context reads, interactive writes, and bulk writes never manufacture a late baseline. A Cave created after rollout instead receives its ordinary `Create` revision atomically with creation.

Recovered pre-revision history may later be reconstructed from older durable sources and presented before this boundary. Such recovered history is distinct from the authoritative `CaveRevision` chain and must not rewrite the rollout revision's predecessor pointer.

### Rollout deployment sequence

The rollout boundary assumes Cave-owned writes are quiesced while initialization runs. The supported deployment order is:

1. stop or otherwise quiesce the old API's Cave/Entrance/File/line-plot/import/admin writes;
2. apply the Cave-revisions EF migrations;
3. run the separately published operations artifact with `initialize-cave-revision-history`;
4. allow that command to verify the rollout is complete;
5. deploy/start the new API only after the operation succeeds.

The deployment-only executable runs the bootstrap with:

```bash
ConnectionStrings__default="<scoped database connection>" \
  dotnet Planarian.Operations.dll initialize-cave-revision-history
```

`Planarian.Operations` is a small console host, not the web application. Its `Program.cs` is generic and only dispatches registered operation names. The temporary Cave operation and its `CaveRevisionRolloutRepository` live entirely in the Operations project; that repository owns the EF/Npgsql setup and rollout mechanics and uses the permanent shared snapshot repository only for canonical snapshot construction. It does not start Kestrel or load Azure App Configuration. A successful rerun is a no-op and reports the existing history boundary.

The API runtime has no rollout command, readiness check, or deployment-history knowledge. The coordinated deployment owns the safety boundary: it keeps the old API quiesced, requires the Operations command to complete successfully, and only then deploys/starts the new API.

Do not let the old writer remain active during step 3. The rollout intentionally commits bounded batches for resumability rather than holding one database-wide transaction, so quiescing writers is what makes the shared timestamp represent one authoritative rollout state. Concurrent rollout commands themselves are serialized with a PostgreSQL advisory lock.

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

Removing a previously published File queues its exact object address as a `StorageCleanupTarget` with purpose `CaveAssetRemoval` before deleting the live File row. The Cave revision and cleanup target commit atomically; physical object deletion is attempted only after that transaction commits. A failed external delete leaves durable cleanup debt rather than retaining the File payload as historical content.

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

## Supported interactive boundary

The Cave detail page is read-only for Cave-owned Files and line plots. Normal interactive mutation of those assets occurs through the aggregate Cave editor; the detail page continues to provide read/view behavior only.

## Read the code in this order

1. Planarian.Web/src/Modules/Caves/Pages/EditCavePage.tsx
2. Planarian.Web/src/Modules/Caves/Helpers/CaveFormMapper.ts
3. Planarian/Planarian/Modules/Caves/Controllers/CaveController.cs
4. Planarian/Planarian/Modules/Caves/Services/CaveService.cs
5. Planarian/Planarian/Modules/Caves/Revisions/CaveMutationWorkflow.cs
6. Planarian/Planarian/Modules/Caves/Revisions/CaveMutationRepository.cs
7. Planarian/Planarian.Library/Caves/Revisions/CavePublishedSnapshotRepository.cs
8. Planarian/Planarian.Operations/OperationsCatalog.cs
9. Planarian/Planarian.Operations/CaveRevisionHistory/InitializeCaveRevisionHistoryOperation.cs
10. Planarian/Planarian.Operations/CaveRevisionHistory/CaveRevisionRolloutRepository.cs
11. Planarian/Planarian.Model/Database/Revisions/CavePublishedSnapshotV1.cs
