# ADR 0001 — Use immutable semantic snapshots for Cave revisions

**Status:** Accepted for the pre-release Cave-revisions target
**Scope:** Cave published history
**Decision date:** 2026 pre-merge Cave revisions work

## Context

Planarian historically stored the current Cave aggregate but did not have one durable representation of what a Cave looked like at each accepted publication.

A useful history model must survive:

- scalar edits;
- tag changes and later tag renames;
- Entrance changes;
- File metadata changes;
- line-plot changes;
- imports and administrative remaps;
- hard Cave deletion.

A history record also needs stable semantics after the software evolves.

Storing only an operation log or ad-hoc field deltas would require replaying historical business rules and resolving current reference data to reconstruct old state.

## Decision

Each accepted Cave publication stores a complete semantic snapshot in CaveRevision.SnapshotJson. Application persistence treats revision rows as append-only after creation; this is an application invariant rather than a database trigger that rejects arbitrary SQL updates.

CavePublishedSnapshotV1 is a versioned persisted schema.

A revision records:

- AccountId and CaveId;
- PreviousRevisionId;
- publication source and operation;
- SnapshotSchemaVersion;
- immutable SnapshotJson;
- optional proposal/import provenance.

Cave.CurrentRevisionId identifies the accepted current revision for a live Cave.

Snapshots capture historical user-facing reference labels alongside stable IDs so later reference renames do not rewrite historical presentation.

Snapshots include File identity/metadata and line-plot identity/name/content hash, but deliberately do not include private object-storage locators or raw line-plot GeoJSON.

Semantic no-ops do not create revisions.

## Consequences

### Positive

- A revision is self-contained enough to render meaningful history without replaying arbitrary old mutations.
- Current reference renames do not change old labels.
- Direct edits, proposals, imports, and admin changes can share one historical contract.
- Delete history can survive after the live Cave row is removed.
- Persisted schema evolution is explicit through SnapshotSchemaVersion.

### Costs

- Complete snapshots use more storage than sparse field deltas.
- Snapshot generation must remain bounded for large imports.
- V1 becomes an immutable persisted contract after release; later semantic changes require a new version and reader dispatch.
- A snapshot is historical metadata, not automatically a full restore mechanism.

## Alternatives considered

### Store only field-level change events

Rejected as the primary history model because reconstruction would depend on event replay, historical reference semantics, and evolving business rules.

### Copy the complete relational graph into version tables

Rejected for this phase because it would create a much larger persistence model and duplicate the current aggregate schema.

### Use database temporal/audit tables as the product history model

Rejected because raw row history does not directly represent one semantic Cave publication across multiple tables and external File metadata.

## Related documentation

- [Cave publication](../cave-publication.md)
- [Data model](../data-model.md)
- [Cave revision contract](../../cave-revisions.md)
