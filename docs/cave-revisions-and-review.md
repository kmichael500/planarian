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

`Cave.CurrentRevisionId` is intended to point at the accepted snapshot matching the actual
published relational state. `CaveRevision` keeps a logical Cave ID and does
not require the live Cave row, so delete history survives hard deletion.

## Proposal and review boundary

Pending submissions are separate immutable proposal versions. A proposal has
an exact `BaseRevisionId`; it never mutates published Cave data, allocates a
CountyNumber, publishes files, or creates live taxonomy resources while
pending. Reviewer amendments append a new proposal version. Approval reuses
the same publication writer and creates an accepted revision from the actual
resulting database state. Rejection creates no Cave revision.

## Mutation boundary

All revisionable publication paths—manager edits, archive/unarchive, delete,
file association changes, Cave imports, Entrance imports, and approval—must
cross `CaveMutationCoordinator`; the current branch has the coordinator
foundation but has not yet routed every existing publication path through it.
The coordinator owns the transaction,
expected revision checks, `xmin` conflict detection, snapshot creation,
revision insertion, pointer advancement, and commit. GeoJSON is a separate
domain in V1.

## Import strategy

CSV parsing and planning occur outside the write transaction. Commit locks the
affected Cave rows in stable ID order and verifies each planned current
revision before applying the batch. Relational changes, revisions, and
revision pointers use bounded EF Core batches. No-change records create no
revision. Import provenance is stored in `CaveImportBatch` rather than tied to
temporary upload rows. Physical blob cleanup is deferred until after commit.

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
