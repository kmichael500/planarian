# ADR 0002 — Publish interactive Cave changes as one aggregate desired state

**Status:** Accepted for the pre-release Cave-revisions target
**Scope:** Interactive Cave mutation

## Context

A Cave is not only the Cave row. User-visible Cave state includes tags, Entrances, Files, and line plots.

Independent mutation endpoints for each child area create several problems:

- one user action can produce multiple inconsistent history events;
- partial success can leave the Cave in an intermediate state;
- concurrency tokens can protect one table while another writer changes related state;
- no-op detection becomes fragmented;
- proposal approval would need to coordinate many publication mechanisms.

## Decision

Treat one interactive Cave edit as one aggregate desired-state operation containing:

- Cave scalar fields;
- Cave tags;
- Entrances and Entrance tags;
- Files;
- line plots.

For existing direct edits, the browser sends the exact revision it loaded as ExpectedRevisionId.

The service validates and normalizes the complete desired state. The relational mutation and resulting CaveRevision are committed in one PostgreSQL transaction.

One accepted aggregate operation creates at most one revision. A semantic no-op creates none.

Approval of an immutable proposal version routes through the same aggregate publication semantics rather than writing Cave tables independently.

## Consequences

### Positive

- User intent and history event boundaries align.
- A save cannot publish Cave fields while silently failing to publish associated File/line-plot intent inside the relational transaction.
- One concurrency check covers the published aggregate.
- Proposal approval reuses the same publication semantics.
- Revision diffs are easier to understand.

### Costs

- CaveService coordinates a broad use case and must remain disciplined about persistence boundaries.
- The editor DTO is a complete desired-state contract rather than a tiny patch.
- Reference locking/normalization must account for all child collections.
- Large bulk operations should not use this interactive path one Cave at a time.

## Alternatives considered

### Keep separate File and GeoJSON publication endpoints

Rejected as a supported architecture because they create parallel publication boundaries and history ambiguity.

### Last-write-wins direct editing

Rejected because an old browser form could overwrite a newer accepted Cave state.

### Route imports through the interactive aggregate service

Rejected because supported import scale requires set-oriented planning/execution.

## Related documentation

- [Cave publication](../cave-publication.md)
- [Bulk import publication ADR](0006-bulk-import-publication.md)
- [Cave revision contract](../../cave-revisions.md)
