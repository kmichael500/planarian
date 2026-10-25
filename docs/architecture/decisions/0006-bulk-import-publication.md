# ADR 0006 — Use pure import planning and set-oriented bulk revision publication

**Status:** Accepted for the pre-release Cave-revisions target
**Scope:** Cave and Entrance CSV import

## Context

Planarian supports imports on the order of 10,000 Caves and roughly 15,000 Entrances.

The import feature needs:

- dry-run preview;
- deterministic synchronization decisions;
- reference creation;
- concurrency protection;
- tenant isolation;
- Cave revision history;
- acceptable command/tracking/memory behavior.

Running the interactive Cave service once per row would not meet those goals cleanly.

A write-and-rollback dry run would also create unnecessary side effects and make preview dependent on a mutation path.

## Decision

Imports use four distinct phases:

1. **Parse** source into typed records with no database access.
2. **Load planning state** using account-scoped read repositories.
3. **Plan** using pure planners that produce immutable CaveImportPlan / EntranceImportPlan objects.
4. **Execute** accepted plans using set-oriented execution repositories.

Dry run stops after planning.

Commit execution:

- opens the write transaction only after the plan is complete;
- locks/revalidates referenced Counties/TagTypes/Caves in durable order;
- applies set-oriented changes in bounded batches;
- captures before/after semantic snapshots;
- uses CaveBulkRevisionRepository to create revisions and advance current pointers;
- commits relational state and revision history atomically;
- performs destructive external object cleanup after commit.

Interactive and bulk publication share snapshot construction and semantic comparison (`CavePublishedSnapshotV1` +
`CaveRevisionDiffService`) but not one per-Cave execution loop. Existing Caves compare the resulting state to the
stored current published revision. Pre-existing Caves are initialized during the controlled revision-history rollout;
bulk publication fails closed if an existing target lacks that current revision instead of creating a late baseline.

## Consequences

### Positive

- Preview is read-only and can be compared directly with the plan that will execute.
- Large imports stay bounded and set-oriented.
- Pure planning logic is easy to test without a database.
- Concurrency assumptions captured during planning are revalidated before writes.
- Revision history remains consistent with interactive Cave semantics.

### Costs

- There are intentionally separate interactive and bulk publication implementations.
- Planner/executor contracts are more explicit than a simple CRUD import loop.
- Changes to the semantic snapshot shape must be considered by both publication paths.
- Lock ordering is part of the architecture and must remain consistent with admin/direct workflows.

## Alternatives considered

### Call CaveService for every row

Rejected because it would multiply queries, tracking, lock overhead, and orchestration cost at supported scale.

### Perform dry run by executing writes and rolling back

Rejected because preview should not depend on temporary database mutation or external side effects.

### Give imports an unrelated history format

Rejected because users should not need different revision semantics based on how a Cave was changed.

## Related documentation

- [Import architecture](../imports.md)
- [Import compatibility](../../import-compatibility.md)
- [Cave revision contract](../../cave-revisions.md)
