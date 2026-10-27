# ADR 0007 — Preserve Cave asset history without retaining removed payloads indefinitely

**Status:** Accepted for the pre-release Cave-revisions target
**Scope:** Published File and line-plot removal lifecycle
**Supersedes:** [ADR 0005 — Retained File objects](0005-retained-file-objects.md)

## Context

Files and line plots are both Cave-owned assets. They use different physical storage today, but users expect the same revision semantics: stable identity while current, add/change/remove through the Cave aggregate, and durable history describing what changed.

Published revision history does not need to be a permanent payload archive. Keeping every removed File byte indefinitely increases storage and sensitive-data retention, while line-plot history already keeps only stable identity, historical name, and a content fingerprint rather than the old GeoJSON body.

PostgreSQL and object storage cannot participate in one atomic transaction. A File object therefore cannot be safely deleted until the relational Cave mutation and its revision have committed.

## Decision

Files and line plots share one **semantic asset lifecycle** without introducing a generic asset entity/repository hierarchy.

For published history:

- File snapshots preserve stable File ID plus historical filename/type metadata, not object-storage locators or removed bytes;
- line-plot snapshots preserve stable ID, historical name, and deterministic content hash, not removed GeoJSON bodies;
- the Cave revision supplies actor and timestamp, so history can explain that an asset was removed without retaining its payload.

For ordinary published removal:

- a `CaveGeoJson` row is deleted inside the same PostgreSQL transaction that publishes the Cave revision;
- a File's live row is deleted inside that transaction and its object address is queued as `StorageCleanupTarget` with purpose `CaveAssetRemoval`;
- only after commit does Planarian attempt object deletion;
- successful/already-absent objects clear the cleanup target;
- failed external deletion leaves durable, observable cleanup debt for retry;
- ordinary asset-cleanup debt does not block unrelated account writes.

`StorageCleanupTarget` is shared infrastructure for post-commit external cleanup, not a historical File locator. Immediate cleanup is followed by bounded opportunistic retries on later Cave/File authoring activity; ordinary asset-removal debt does not currently have a dedicated background relay. Account Reset uses the same table with purpose `AccountReset` and keeps its stronger write-block/retry semantics. Reset absorbs any pending asset-removal addresses before purging those targets.

Proposal persistence remains storage-specific for now: staged Files reference their exact staged object, while immutable proposal versions retain exact proposed line-plot GeoJSON. This is intentional; proposal payload convergence is deferred until line plots actually move into the File model.

## Consequences

### Positive

- Files and line plots have the same user-facing removal/history contract.
- Planarian does not deliberately retain removed payloads as historical Cave content merely because they were once published.
- File creation/removal remains crash/failure tolerant across the PostgreSQL/object-storage boundary; immutable addresses are create-only and removal uses durable post-commit cleanup.
- Revision JSON stays provider-neutral and does not expose storage addresses.
- The eventual line-plot-as-File migration can reuse the same semantic lifecycle rather than redesigning revisions again.
- Shared code is limited to real commonality: stable-ID diffing/presentation semantics and durable post-commit storage cleanup.

### Costs

- Old published payloads are not recoverable from Planarian after successful removal cleanup.
- External storage failures can leave cleanup debt until later authoring activity or another destructive workflow retries/assumes it; ordinary asset cleanup does not yet have a dedicated background relay.
- File and line-plot proposal persistence remains intentionally asymmetric until a future storage migration.

## Alternatives considered

### Retain every published File byte until hard purge

Superseded because the storage/privacy cost is not required for useful revision history and differs from line-plot history semantics.

### Delete File bytes before relational commit

Rejected because a failed/rolled-back Cave publication could leave current relational state pointing at missing bytes.

### Build a generic CaveAsset framework now

Rejected as premature. File and `CaveGeoJson` storage behavior is genuinely different today. A small shared stable-ID diff helper and the generic storage-cleanup outbox capture the useful commonality without hiding those differences.

### Move line plots into File storage immediately

Deferred. The current `CaveGeoJson` JSONB/rendering path remains valid; this decision aligns semantics now so that a later storage migration is smaller.

## Related documentation

- [Files and storage](../files-and-storage.md)
- [Cave publication](../cave-publication.md)
- [Data model](../data-model.md)
- [Upload-once File ADR](0004-upload-once-file-storage.md)
