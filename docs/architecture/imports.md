# Import architecture

This page explains why **Cave and Entrance CSV imports** use a different execution path from interactive Cave editing. Account File import is a separate workflow described below.

The durable compatibility requirements live in [Import compatibility](../import-compatibility.md).

## Why imports are different

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
    PlanningRepo["Planning repository"]
    Planner["Pure planner"]
    Plan["Immutable import plan"]
    Preview["Dry-run preview"]
    Executor["Execution repository"]
    Snapshots["CavePublishedSnapshotRepository"]
    Bulk["CaveBulkRevisionRepository"]
    DB[("PostgreSQL")]
    Cleanup["Object storage cleanup"]

    Service -->|"read source"| CSV
    CSV -->|"stream"| Parser
    Parser -->|"typed records"| Planner
    PlanningRepo -->|"account-scoped materialized state"| Planner
    Planner -->|"deterministic result"| Plan
    Plan -->|"read-only result"| Preview
    Service -->|"accepted plan"| Executor
    Plan -->|"immutable instructions"| Executor
    Executor -->|"locks + set-oriented writes"| DB
    Executor -->|"build before/after"| Snapshots
    Executor -->|"snapshots + expected revisions"| Bulk
    Bulk -->|"revisions + current pointers"| DB
    Executor -->|"return deferred delete addresses after commit"| Service
    Service -->|"best-effort external delete"| Cleanup
~~~

## Phase 1 — Parse

CaveImportCsvParser and EntranceImportCsvParser turn the uploaded stream into typed records.

Parsing is database-free.

## Phase 2 — Load planning state

Planning repositories load only the current account state required by the planner, such as:

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

Execution repositories own the write transaction.

For Cave import they:

1. verify the plan belongs to the active account;
2. acquire the required County namespace/reference locks;
3. acquire TagType reference locks;
4. lock existing target Caves in deterministic order;
5. revalidate expected xmin/current revision/reference metadata;
6. persist planned new references;
7. capture before snapshots;
8. apply set-oriented Cave/tag/Entrance changes in bounded batches;
9. capture after snapshots;
10. create the CaveImportBatch;
11. call CaveBulkRevisionRepository;
12. commit;
13. return external File-object addresses that may be deleted after commit.

If any relational step fails, the transaction rolls back.

## Revision publication

CaveBulkRevisionRepository has a different execution shape from the interactive mutation repository, but it does not
have different revision semantics. Both paths build `CavePublishedSnapshotV1` through
`CavePublishedSnapshotRepository` and use `CaveRevisionDiffService` for semantic change detection. For a Cave that
already has revision history, bulk publication compares the resulting snapshot with the stored current published
revision, not merely with the live rows captured immediately before the bulk write. This preserves the same behavior for
reference-label drift and semantic no-ops as interactive publication. A legacy Cave with no revision still compares its
live before/after snapshots so a `SystemBaseline` is created only when the operation actually changes it.

It receives:

- before snapshots;
- after snapshots;
- expected current revision IDs;
- operation per Cave;
- source;
- import batch ID.

It then:

- ignores semantic no-change rows;
- creates a SystemBaseline for an existing Cave only when needed;
- creates one Import revision for each real mutation;
- advances CurrentRevisionId for non-deleted Caves;
- writes in bounded batches.

This avoids routing thousands of Caves through an interactive service loop while keeping historical semantics aligned.

## Sync deletion

Sync import can plan Cave deletion.

Before delete execution, open change requests block hard deletion.

The executor gathers current and retained File object addresses, removes the relational graph inside the import transaction, publishes delete revisions, commits, and lets ImportService perform best-effort external object cleanup afterward.

## Lock ordering

The durable order is intentionally consistent with other publication workflows:

1. County resources when needed;
2. TagType resources;
3. Cave rows;
4. Cave-owned child/relationship/File rows.

Stable IDs inside a class are ordered deterministically.

See [Cave revisions](../cave-revisions.md) for the exact lock contract.

## File import is a separate path

The account File-import feature does **not** use CaveImportPlanner/EntranceImportPlanner or CaveBulkRevisionRepository.

After it resolves the target Cave from the filename/import rules, it calls `FileService.UploadCaveFile` or `FileService.PublishStagedCaveFile`. Those methods publish one target Cave through `CaveMutationCoordinator` / `CaveMutationRepository` so the File change still appears in Cave history.

This distinction is important when tracing code: “import” in Planarian names more than one workflow.

## Entrance import

Entrance import follows the same architectural pattern:

- typed parser;
- planning repository;
- pure EntranceImportPlanner;
- immutable plan;
- execution repository;
- before/after Cave snapshots;
- CaveBulkRevisionRepository.

Entrance changes publish Cave revisions because Entrances are part of the semantic Cave aggregate.

## Read the code in this order

1. Planarian/Planarian/Modules/Account/Import/Services/ImportService.Caves.cs
2. Planarian/Planarian/Modules/Import/Parsing/ImportCsvParsers.cs
3. Planarian/Planarian/Modules/Import/Data/CaveImportPlanningRepository.cs
4. Planarian/Planarian/Modules/Import/Planning/ImportPlanningState.cs
5. Planarian/Planarian/Modules/Import/Planning/CaveImportPlanner.cs
6. Planarian/Planarian/Modules/Import/Planning/CaveImportPlan.cs
7. Planarian/Planarian/Modules/Import/Data/CaveImportExecutionRepository.cs
8. Planarian/Planarian/Modules/Caves/Revisions/CaveBulkRevisionRepository.cs

Then repeat the same tour with the Entrance equivalents.
