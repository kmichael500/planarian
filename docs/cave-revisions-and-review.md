# Cave revisions and review architecture

This document records the implementation boundary for Cave publication,
history, imports, and pending review.

## Data access

Planarian uses EF Core 8 with Npgsql/PostGIS as its application persistence
model. Inserts use bounded `AddRange`/`SaveChangesAsync` batches; uniform
set-based changes use `ExecuteUpdateAsync` or `ExecuteDeleteAsync`; distinct
per-row changes use tracked entities in bounded chunks. Entrance import rows
are now typed in-memory data rather than a dynamic PostgreSQL staging table.

The former linq2db and EFCore.BulkExtensions dependencies and production APIs
were removed. No replacement ORM or bulk package was added.

## Published history

Current Cave data remains normalized. Each accepted revision stores an
explicit, versioned `CavePublishedSnapshotV1` JSON document containing the
published Cave aggregate's revisionable state. It contains references and
metadata only: file bytes, SAS URLs, GeoJSON, search vectors, EF metadata, and
concurrency tokens are excluded. Historical tag references retain
`NameAtRevision`.

`Cave.CurrentRevisionId` points at the accepted snapshot matching the actual
published relational state. `CaveRevision` keeps a logical Cave ID and does
not require the live Cave row, so delete history survives hard deletion.

Reference display values are historical Cave state. A later published Cave
snapshot may capture a renamed State, County, TagType, Location Quality, or
File Type using the same stable ID. The history diff reports that separately as
an effective reference-data change, rather than claiming that the actor who
published the Cave revision performed the shared-reference rename. A Cave
revision timestamp is the snapshot-capture time, not necessarily the exact
time at which any nested shared reference changed. V1 intentionally does not
fan out revisions when shared reference data is renamed.

## Proposal and review boundary

Pending submissions are separate immutable proposal versions. A proposal has
an exact `BaseRevisionId`; it never mutates published Cave data, allocates a
CountyNumber, publishes files, or creates live taxonomy resources while
pending. Reviewer amendments append a new proposal version. Approval must
cross the same published-history boundary and create an accepted revision from
the actual resulting database state. Rejection creates no Cave revision.

## Mutation boundary

Published Cave history has one semantic boundary but two execution shapes.
Interactive manager mutations use `CaveMutationCoordinator`; high-volume CSV
imports use `ImportRevisionPublisher` inside the import executor transaction.
Both publish snapshots from actual relational state, enforce expected revision
state, create no revision for semantic no-ops, and advance history atomically
with the corresponding relational change.

## File ownership and provider validation

Every persisted `File` is account-owned. This includes temporary import files,
which have no `CaveId` but are owned by the uploading account. The
tenant-qualified `Files(AccountId, Id)` key allows PostgreSQL to enforce that a
staged change-request file belongs to the same account as its request. The
migration backfills a legacy cave file from its Cave's account and fails with a
diagnostic if any remaining legacy row has no determinable owner; it never
assigns a synthetic empty account ID.

Provider-specific tests require PostgreSQL/PostGIS through Testcontainers. On
local macOS Colima installations that reject Ryuk's socket bind, run tests with
`DOCKER_HOST=unix:///Users/michaelketzner/.colima/default/docker.sock` and
`TESTCONTAINERS_RYUK_DISABLED=true`. This is a local workaround only, not a CI
default.

Manager Cave create/edit/archive/unarchive/hard-delete and published Cave-file
upload, staged publication, and metadata edits are routed through
`CaveMutationCoordinator`. For short mutations the coordinator owns the
transaction. For larger existing service workflows it exposes prepare/publish
operations that require and participate in the caller's active EF transaction,
so permissions, tag/file work, revision insertion, pointer advancement, and
commit remain one atomic unit without nested transactions. Hard delete writes
a final tombstone revision from the last live snapshot after dependent
relational rows are removed but before commit. Pending staged-file references
are removed in the same transaction before their published File rows are
removed.

Blob storage cannot participate in the PostgreSQL transaction, so Cave-file
writes use explicit compensation. If an upload or staged-file copy succeeds in
blob storage but the relational/revision publication later fails, the unique
destination blob is deleted best-effort without replacing the original
exception. Destination compensation does not delete the staging source;
staging-session cleanup remains owned by the upload-session caller, which
cleans the committed staging blob when the import attempt finishes. Expired
temporary-file rows are removed transactionally, but their blobs are deleted
only after the database transaction commits. Cave hard-delete and import-sync
blob cleanup are likewise deferred until after commit. GeoJSON is a separate
domain in V1.

Cave and Entrance imports deliberately do not route thousands of rows through
per-Cave coordinator calls. Their executors lock and verify scoped Cave rows,
apply bounded relational batches, and use `ImportRevisionPublisher` once per
transaction to create the corresponding accepted history and import provenance.

## Import strategy

CSV parsing and planning occur outside the write transaction. Commit locks the
affected Cave rows in stable ID order and verifies each planned current
revision before applying the batch. Relational changes, revisions, and
revision pointers use bounded EF Core batches. No-change records create no
revision. Import provenance is stored in `CaveImportBatch` rather than tied to
temporary upload rows. Physical blob cleanup is deferred until after commit.
Destructive sync statements bypass permission query filters only after the
owned Cave/File/Entrance IDs are established, and each such statement restores
an explicit account predicate before mutation.

## Inventory notes

The current branch's former alternate data-access paths and replacements are
listed below. Performance numbers are intentionally kept in the benchmark
report once PostgreSQL benchmark infrastructure is available; wall-clock
performance is not a CI assertion.

| Former path | Replacement |
| --- | --- |
| RepositoryBase `BulkInsertAsync` | Explicit repository `AddRange` followed by bounded `SaveChangesAsync` |
| Cave import `BulkConfig`/bulk insert | EF `AddRange` and `SaveChangesAsync` |
| Cave import `BulkUpdateImportCaves` | Bounded tracked load, property updates, and one save |
| Account cleanup `Take(...).DeleteAsync` | EF `ExecuteDeleteAsync` (bounded query semantics retained for cleanup) |
| Account linq2db async helpers | EF `ToListAsync`, `FirstOrDefaultAsync`, `CountAsync` |
| Account linq2db uniform updates | EF `ExecuteUpdateAsync` |
| Temporary entrance linq2db table/COPY API | Typed in-memory import rows resolved through EF |
| Map linq2db namespace | Normal EF Core queries |
