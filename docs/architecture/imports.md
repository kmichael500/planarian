# Import architecture


The durable compatibility requirements live in [Import compatibility](../import-compatibility.md).

## Execution model

A supported import may contain roughly 10,000 Caves and around 15,000 Entrances.

Calling the interactive Cave mutation service once per row would:

- repeatedly query the same reference data;
- create excessive EF tracking;
- produce unnecessary round trips;
- make lock ordering difficult;
- make dry-run behavior expensive and stateful.

The branch therefore uses **parse → load planning state → pure plan → preview or execute**.

## Flow

~~~mermaid
flowchart LR
    Service["ImportService"]
    CSV["CSV file"]
    Parser["Typed parser"]
    PlanningReader["Planning reader"]
    Planner["Pure planner"]
    Plan["Immutable import plan"]
    Preview["Dry-run preview"]
    Persistence["Import persistence"]
    Snapshots["CavePublishedSnapshotReader"]
    Bulk["CaveBulkRevisionPublisher"]
    DB[("PostgreSQL")]
    Cleanup["Object storage cleanup"]

    Service -->|"read source"| CSV
    CSV -->|"stream"| Parser
    Parser -->|"typed records"| Planner
    PlanningReader -->|"account-scoped materialized state"| Planner
    Planner -->|"deterministic result"| Plan
    Plan -->|"read-only result"| Preview
    Service -->|"accepted plan"| Persistence
    Plan -->|"immutable instructions"| Persistence
    Persistence -->|"locks + set-oriented writes"| DB
    Persistence -->|"build before/after"| Snapshots
    Persistence -->|"snapshots + expected revisions"| Bulk
    Bulk -->|"revisions + current pointers"| DB
    Persistence -->|"return durable cleanup-target IDs after commit"| Service
    Service -->|"best-effort external delete"| Cleanup
~~~

## Phase 1 — Parse

CaveImportCsvParser and EntranceImportCsvParser turn the uploaded stream into typed records.

Parsing is database-free.

## Phase 2 — Load planning state

Planning readers load only the current account state required by the planner, such as:

- eligible States/Counties;
- existing Cave identities/current revisions when sync is enabled;
- used County numbers;
- eligible TagTypes;
- current Entrance state for Entrance import.

The planner receives materialized immutable records rather than IQueryable or DbContext.

## Phase 3 — Pure planning

CaveImportPlanner / EntranceImportPlanner:

- validate source records;
- resolve reference identity;
- decide inserts/updates/no-change/deletions;
- allocate stable IDs for planned new objects;
- capture expected concurrency state for existing targets;
- create immutable reference-creation intents;
- produce human-readable preview summaries.

The same plan drives both dry run and commit.

That matters because preview is not a simulation produced by mutating the database and rolling it back.

## Dry run

Dry run ends after plan creation.

No write transaction is opened and no TagType/County/AccountState/Cave rows are created merely to preview an import.

This makes preview safer and makes preview/commit equivalence testable.

## Commit execution

Import persistence components own the write transaction.

For Cave import they:

1. verify the plan belongs to the active account;
2. acquire the required County namespace/reference locks;
3. acquire TagType reference locks;
4. lock existing target Caves in deterministic order;
5. revalidate expected xmin/current revision/reference metadata;
6. when execution came from an uploaded CSV, lock and revalidate the `CaveImportSource` account, storage container, import key namespace, and expiration, then clear its expiration inside the transaction;
7. capture before snapshots;
8. create and save the CaveImportBatch, including the import-source ID and normalized filename when available;
9. persist planned new references;
10. apply set-oriented Cave/tag/Entrance changes in bounded batches;
11. queue any relational cleanup responsibility required by sync deletion;
12. capture after snapshots;
13. call CaveBulkRevisionPublisher;
14. commit;
15. return durable storage-cleanup target IDs for bounded post-commit processing.

If any relational step fails, the transaction rolls back. Import-source promotion is part of that same transaction, so an upload remains temporary when apply fails. Before parsing, dry run and apply also validate that the selected `CaveImportSource` is an available import upload; dry run never reaches promotion.

## Revision publication

CaveBulkRevisionPublisher has a different execution shape from the interactive `CaveMutationPersistence` path, but it does not
have different revision semantics. Both paths build `CavePublishedSnapshotV1` through
`CavePublishedSnapshotReader` and use `CaveRevisionDiffService` for semantic change detection. For a Cave that
already has revision history, bulk publication compares the resulting snapshot with the stored current published
revision, not merely with the live rows captured immediately before the bulk write. This preserves the same behavior for
reference-label drift and semantic no-ops as interactive publication. Every pre-existing Cave must already have its
rollout revision before import execution is available; a missing current revision on an existing Cave is an integrity
error rather than a reason to manufacture a late baseline.

It receives:

- before snapshots;
- after snapshots;
- expected current revision IDs;
- operation per Cave;
- source;
- import batch ID.

It then:

- ignores semantic no-change rows;
- fails closed if an existing Cave lacks its rollout/current revision;
- creates one Import revision for each real mutation;
- advances CurrentRevisionId for non-deleted Caves;
- writes in bounded batches.

This avoids routing thousands of Caves through an interactive service loop while keeping historical semantics aligned.

## Authoring parity and identity boundaries

The bulk persistence components are required to produce the same published aggregate semantics as ordinary authoring for fields both
workflows own. Integration parity tests therefore exercise full-field create/update/clear/delete lifecycles and compare
complete `CavePublishedSnapshotV1` values plus revision predecessor/provenance. A few workflow-shape differences are tested explicitly rather than normalized away:

- interactive Cave creation requires a primary Entrance, while Cave CSV and Entrance CSV are independent imports. The
  equivalent import workflow is therefore Cave creation followed by Entrance publication, producing two Import revisions
  where direct creation produces one ManagerEdit/Create revision;
- Cave sync identity is State + County + County number. Moving any part of that identity through ordinary authoring can
  retain the Cave ID, while sync import represents the move as deleting the old Cave and creating the new identity;
- Entrance sync has no stable source Entrance ID and replaces represented Entrances. Revision history therefore records
  remove/add identity changes, while an ordinary edit retains the Entrance ID and records scalar/tag changes;
- CSV `IsArchived` reaches the same final published state as Archive/Unarchive commands, but import provenance is
  `Import/Update` rather than `ManagerEdit/Archive` or `ManagerEdit/Unarchive`;
- existing-Cave Cave sync and Entrance sync replacement canonicalize blank optional authoring text to `null` where normal
  authoring does. Characterized insert/append compatibility remains unchanged.

Account File import uses a separate path: after filename-to-Cave resolution and chunk staging it publishes
through `FileService` and the normal Cave mutation coordinator, so the resulting Cave revision is the ordinary
ManagerEdit/Update history entry and is not linked to a `CaveImportBatch`. Finalization is serialized per upload session across request-scoped import services: Azure block commitment, relational File publication, and caching the completion result are one single-flight operation. A concurrent finalize reuses the cached publication outcome rather than publishing another File, but each caller receives an independent response object so its HTTP trace ID cannot mutate shared session state. Once a session is finalizing, cancellation cannot delete its source blob; cancel-all skips finalizing sessions even when they transition to finalizing after session selection but before their gate is acquired; other active sessions are still canceled. Expiration cleanup uses the chunk-upload session gate and rechecks expiration after acquiring it, so an in-flight chunk cannot refresh its TTL after a sweep decides to delete it. Finalizing sessions are retained through publication even if the original upload TTL passes.

## Sync deletion

Sync import can plan Cave deletion.

Before delete execution, open change requests block hard deletion.

The Cave import persistence component gathers current live File object addresses, queues tenant-qualified `CaveAssetRemoval` cleanup targets in the same import transaction that removes the relational graph and publishes delete revisions, then commits. A File with only one half of its object-storage address fails the import closed rather than committing a deletion that cannot carry cleanup responsibility. After commit, ImportService processes every cleanup target created by that import in bounded target/reference-query batches; provider failures leave durable debt for later retry. Pending ordinary asset-cleanup debt is independent operational state rather than historical Cave content.

## Lock ordering

The lock order matches the other publication workflows:

1. County resources when needed;
2. TagType resources;
3. Cave rows;
4. Cave-owned child/relationship/File rows.

Stable IDs inside a class are ordered deterministically.

See [Cave revisions](../cave-revisions.md) for the exact lock contract.

## File import is a separate path

The account File-import feature does **not** use CaveImportPlanner/EntranceImportPlanner or CaveBulkRevisionPublisher.

After it resolves the target Cave from the filename/import rules, it calls `FileService.UploadCaveFile` or `FileService.PublishStagedCaveFile`. Those methods publish one target Cave through `CaveMutationCoordinator` / `CaveMutationPersistence` so the File change still appears in Cave history.

`import` names separate workflows in Planarian; account File import is not Cave/Entrance bulk import.

## Entrance import

Entrance import follows the same architectural pattern:

- typed parser;
- planning reader;
- pure EntranceImportPlanner;
- immutable plan;
- import persistence component;
- before/after Cave snapshots;
- CaveBulkRevisionPublisher.

Entrance changes publish Cave revisions because Entrances are part of the semantic Cave aggregate.
