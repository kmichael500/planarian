# ADR 0005 — Retain published File bytes outside the live File row after removal

**Status:** Accepted for the pre-release Cave-revisions target
**Scope:** Historical published File content

## Context

A published Cave revision records stable File identity and user-facing metadata.

If a File is later removed from current Cave state, historical revision metadata may still refer to bytes that were genuinely published.

Keeping the live File row indefinitely is undesirable because File carries current-domain relationships such as CaveId and FileTypeTagId. A removed historical File should not keep current TagType relationships alive or block future administration.

Deleting the bytes immediately would make the historical stable File ID unable to resolve to the original content.

## Decision

Before ordinary revisioned removal of a previously published File:

1. record a RetainedCaveFileObject containing AccountId, CaveId, FileId, StoragePartition, and StorageKey;
2. delete the live File row/current Cave membership;
3. publish the new Cave revision;
4. keep the external object bytes.

RetainedCaveFileObject deliberately has no live-domain foreign key to File, Cave, or TagType.

It is unique by AccountId + FileId and indexed by AccountId + CaveId for purge lookup.

Explicit Cave/account hard purge removes retained locators and deletes the de-duplicated objects after relational commit.

## Consequences

### Positive

- Historical published bytes can remain resolvable after current File removal.
- Removed Files no longer keep FileTypeTag/Cave relationships active.
- Revision JSON does not leak private storage locators.
- The retention model is provider-neutral and small.

### Costs

- Historical bytes consume storage until explicit purge.
- A future historical-download/restore feature must authorize history and resolve either a current File or retained locator.
- Retention cleanup becomes part of hard-delete/reset correctness.

## Alternatives considered

### Keep detached File rows forever

Rejected because live File relationships would continue to affect current-domain administration.

### Delete bytes when File leaves current Cave state

Rejected because revision history would preserve File identity without the content that identity represented.

### Build a generic File-version/archive subsystem

Rejected as unnecessary for the current requirement; the narrow locator table solves the external-byte problem without creating a second generalized versioning system.

## Related documentation

- [Files and storage](../files-and-storage.md)
- [Data model](../data-model.md)
- [Upload-once File ADR](0004-upload-once-file-storage.md)
