# Target architecture and invariants

This document defines the intended end state for the active Cave revisions completion work. Implementation details may
adapt to existing types, but the invariants below are not optional unless this document is deliberately revised.

## 1. Interactive Cave mutation boundary

An interactive Cave edit is one aggregate desired-state operation:

```text
Cave
├── scalar Cave fields
├── tags
├── Entrances
├── Files
└── line plots (`CaveGeoJson` for now)
```

The browser authors a desired state. The server authorizes and validates that desired state, checks the revision the
browser actually edited, computes the normalized semantic result, and publishes at most one `CaveRevision`.

```text
editor desired state
        ↓
authorization + tenant validation
        ↓
expected-current-revision validation
        ↓
normalize/validate complete aggregate
        ↓
build candidate semantic snapshot
        ↓
no-op? ── yes ──> return without revision
        ↓ no
apply normalized relational state
        ↓
create one revision + advance CurrentRevisionId
        ↓
commit relational transaction
```
Interactive manager/admin paths must not separately publish File or line-plot state. A File-only or line-plot-only
change is still a real Cave mutation and follows this same boundary.

Imports remain specialized/bulk workflows. They share snapshot, revision-chain, expected-current-revision, tenant, and
atomicity invariants, but do not get routed through an interactive DTO or per-Cave orchestration loop.

## 2. Direct-edit optimistic concurrency

For an existing Cave, the edit request carries the revision the client loaded:

```text
ExpectedRevisionId = CaveVm.CurrentRevisionId at form initialization
```

Rules:

- create-new-Cave requests use `ExpectedRevisionId = null`;
- existing-Cave direct edits require a non-null expected revision;
- the server never substitutes a freshly loaded `Cave.CurrentRevisionId` for a missing client token;
- mismatch returns the existing revision-conflict behavior/HTTP 409;
- conflict performs no aggregate mutation and creates no revision;
- the browser does not auto-retry or silently rebase a stale form;
- EF/PostgreSQL `xmin` protection remains in place for a race after the expected-revision check.

Example:

```text
A loads R10       B loads R10
A saves → R11     B saves ExpectedRevisionId=R10
                  → conflict, no R12
```

`ExpectedRevisionId` for direct editing is distinct from an immutable proposal version's `BaseRevisionId`. Do not merge
those concepts merely because both reference Cave revisions.

## 3. Relational transaction ownership
The existing dependency direction remains authoritative:

```text
Controller
    ↓
Service / workflow coordinator
    ↓
Pure planning/domain logic + feature repository capabilities
    ↓
Repository / data-access layer
    ↓
EF Core / Npgsql / PostgreSQL
```

Application services do not acquire `PlanarianDbContext`, `DbSet`, `NpgsqlConnection`, or `NpgsqlCommand` ownership.
The repository/data-access layer owns the transaction and persistence details required to make normalized Cave state,
revision state, request/reviewer state, and relational asset associations atomic.

For one interactive publication, persist the relational changes and the one resulting revision in one transaction. Do
not interleave independent commits for fields, tags, Entrances, Files, line plots, and revision publication.

External object storage is not part of that transaction. The File lifecycle below is designed to minimize external
side effects during publication.

## 4. Provider-neutral object-storage boundary

Affected Cave/File application logic depends on a provider-neutral interface such as:

```csharp
public interface IObjectStorage
{
    Task PutAsync(StorageObjectAddress address, Stream content, string? contentType,
        CancellationToken cancellationToken);
    Task<StoredObjectReadResult?> OpenReadAsync(StorageObjectAddress address,
        CancellationToken cancellationToken);
    Task<bool> DeleteIfExistsAsync(StorageObjectAddress address,
        CancellationToken cancellationToken);
}
```
Use a provider-neutral address such as:

```csharp
public sealed record StorageObjectAddress(string Partition, string Key);
```

`Partition` is a logical namespace. The Azure adapter may map it to a container; an S3/MinIO adapter may map it to a
bucket; a filesystem adapter may map it under a configured root. Application logic must not depend on that mapping.

Do not leak Azure types (`BlobClient`, `BlobContainerClient`, Azure response/exception types) through `IObjectStorage`.
The concrete Azure implementation belongs at the infrastructure edge and is selected through DI.

Only add storage operations actually required by the final workflow. In particular, do not preserve `CopyAsync` just
because the old staged-publication workflow copied blobs; upload-once publication should make that operation unnecessary
for new Cave Files.

The existing `File.BlobKey`/`BlobContainer` properties and physical columns may remain as legacy persistence naming in
this task. Provider neutrality is required at the application/storage interface, not as a cosmetic rename across every
repository projection. Translate persisted values into `StorageObjectAddress` at the affected boundary. Rename the
entity properties only if it is genuinely localized and reduces code; do not create broad churn or a migration for names.

This task does not require converting unrelated storage consumers across the repository. Refactor the shared boundary
only as far as the affected Cave/File paths and compilation/cohesion genuinely require.

## 5. File identity and upload-once lifecycle

For newly uploaded Cave/staged Files, allocate the `File` identity before writing bytes and use a key independent of
staging/published state, for example:

```text
objects/files/{fileId}
```

Do not require the original extension in the storage key. Original filename and content type are metadata, not object
identity.
Lifecycle is represented relationally:

```text
staged File
    AccountId = current account
    CaveId = null
    ExpiresOn = cleanup deadline
    storage address = immutable

published File
    AccountId = current account
    CaveId = target Cave
    ExpiresOn = null
    same File ID and same storage address
```

Publication associates the existing File/object with the Cave and clears staging expiration. It does not copy, rename,
or re-upload the object simply because the lifecycle changed.

If relational publication fails, the File remains staged and expiring; the Cave stays unchanged. This is the expected
failure path, not something that requires compensating deletion of a newly copied destination blob.

A staged File must not fundamentally require either a `CaveId` or `CaveChangeRequestId`. Initial proposal authoring may
happen before a request exists, and new-Cave authoring may happen before published association exists.

All staged File lookup/download/delete operations remain account-qualified and permission-checked. A browser-provided
File ID is never proof of ownership.

Existing legacy object keys remain valid. Do not migrate all previously stored bytes merely to adopt the new key scheme.

## 6. Published File retention and removal

A File that has been successfully published in a Cave revision has historical byte content worth retaining, but the
live `File` row is the wrong place to keep that historical content locator after removal. `File` carries current-domain
relationships such as `FileTypeTagId`; retaining a detached `File` row would keep foreign-key dependencies alive and can
block later TagType administration even though the File is no longer current.

Use one narrow internal entity for removed published content, named `RetainedCaveFileObject` unless current naming
conventions strongly favor an equivalent. It contains only durable content-location identity:

```text
Id                     // ordinary entity/audit identity
AccountId              // required tenant owner
CaveId                 // Cave that owned the published File; scalar for lookup/purge, no Cave FK
FileId                 // stable historical File ID from Cave snapshots
StoragePartition       // provider-neutral internal object namespace
StorageKey             // provider-neutral internal object key
```

Required relational rules:

- unique `(AccountId, FileId)`;
- index `(AccountId, CaveId)` for Cave/account hard-purge lookup;
- no FK to `File`, `Cave`, or `TagType`; those live-domain rows may legitimately be deleted later;
- an Account relationship may follow the repository's normal tenant-owned-entity convention, but hard account cleanup
  must explicitly remove these rows and their objects;
- this table is internal persistence and is never serialized into `CavePublishedSnapshotV1`.

Ordinary removal of a current published File happens in the same relational transaction as Cave publication:

```text
current File exists
    ↓
capture/verify RetainedCaveFileObject(AccountId, CaveId, FileId, storage address)
    ↓
delete/detach the live File row and current Cave membership
    ↓
publish the new Cave snapshot/revision without that File
    ↓
commit
    ↓
do NOT delete the underlying object
```

If a retention row for the same `(AccountId, FileId)` already exists (for example after a future restore/re-remove), its
Cave and object address must agree. Treat a mismatch as an invariant violation; never silently repoint historical File
identity to different bytes.

The historical snapshot continues to contain only stable File ID and historical user-facing metadata. Revision and
proposal APIs currently expose `CavePublishedSnapshotV1`, so storage locators must not become V1 wire data. A future
restore/download operation can authorize the Cave/revision, then resolve its File ID to either a current File or the
internal retained-object record server-side. No historical-download or rollback endpoint is added in this task.

Physical object deletion remains correct for:

- never-published staged Files after expiration;
- failed/orphaned uploads that never became valid persisted assets;
- explicit irreversible Cave/account hard purge, which must also delete matching retained-object rows and de-duplicate
  object addresses before external deletion.

This small retention table is intentionally **not** a generic blob archive, File versioning system, or GeoJSON recovery
system. It exists only because File bytes live outside PostgreSQL and a historical stable File ID otherwise has nowhere
provider-neutral and private to resolve after the current `File` row is deleted.

## 7. File editor semantics

The Cave editor owns File mutation intent. Adding a file performs an immediate **staging upload**, not an immediate Cave
publication. The returned staged `File.Id` becomes part of the editor's desired state.

Existing File metadata edits and removals also remain form intent until Save/Submit.
Cancellation semantics:

```text
upload staged File
close/cancel editor
→ published Cave unchanged
→ staged File remains expiring
→ cleanup eventually deletes never-published asset
```

If a user uploads a File and then removes it from the form before Save, it is absent from desired state and must not
make the aggregate mutation non-empty. It expires normally.

Synchronous delete-on-cancel is optional optimization, never a correctness requirement.

## 8. Read-only Cave detail page

The Cave detail page may:

- display current File metadata;
- download current Files;
- render current line plots;
- show revision/history links;
- navigate an authorized user to the Cave editor.

It must not directly:

- add a published File;
- edit published File metadata;
- remove a published File;
- persist uploaded GeoJSON;
- replace or delete a line plot.

There must be one supported interactive mutation surface: the aggregate Cave editor.

## 9. Transitional line-plot model

Keep the current persistence representation in this task:

```text
CaveGeoJson
    Id
    CaveId
    Name
    GeoJson jsonb
```
Do not introduce PostGIS feature rows, MVT rendering, an immutable line-plot version table, or recovery payload storage.
The existing current-state map query/rendering path remains intact except that the detail page no longer persists edits.

The aggregate editor includes desired line plots with stable logical identity, name, and GeoJSON content. Do not add
full line-plot bodies to ordinary `CaveVm`/normal Cave detail responses. Use an authoring-context read for direct edit and
the existing proposal-authoring context for proposal creation; both may load the exact current line-plot bodies needed by
the form after their respective authorization checks. Revision/history APIs continue to use metadata/fingerprints only.

Stable identity rules:

- unchanged existing line plot: keep the same ID and write nothing unnecessary;
- rename: keep the same ID, update `Name`;
- content replacement: keep the same ID, update `GeoJson`;
- addition: server allocates a new ID;
- removal: remove that current `CaveGeoJson` from normalized current state;
- every supplied existing ID must belong to the target Cave and current account;
- duplicate IDs in one desired-state request are invalid.

Do not repeat the current delete-all/recreate-all behavior. Stable IDs are required for meaningful revision diffs and
make the later `LinePlot`/`LinePlotVersion` architecture easier to introduce without changing logical identity again.

## 10. Published line-plot revision semantics

`CavePublishedSnapshotV1` gains deterministic line-plot metadata but **not full GeoJSON bodies**:

```csharp
public sealed record CaveLinePlotSnapshotV1
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string ContentHash { get; init; }
}
```

The feature is still pre-release, so V1 may be corrected now. Once landed, this shape becomes part of the V1 persisted
contract and future semantic changes follow the normal versioning rule.

Line plots are ordered by stable ID with `StringComparer.Ordinal` when building snapshots.
## 11. Deterministic line-plot content fingerprint

Use one centralized server helper to canonicalize valid JSON and hash it with SHA-256. The canonicalization contract is:

- parse with `System.Text.Json`; invalid JSON is rejected before persistence;
- write compact UTF-8 JSON with no insignificant whitespace;
- object properties are emitted in `StringComparer.Ordinal` name order recursively;
- array element order is preserved;
- primitive values are emitted through `Utf8JsonWriter`; do not round coordinates or perform GIS normalization;
- hash the resulting UTF-8 bytes with SHA-256 and store lowercase hexadecimal text.

The goal is deterministic content identity, not geometric/topological equivalence. Reordered features/array elements or
numerically different coordinate values may legitimately count as content changes. Do not add an RFC-JCS library or GIS
canonicalizer merely for this task.

The same helper must be used for:

- published snapshot construction;
- semantic no-op comparison;
- revision diff construction;
- proposal/base comparison where a hash is needed;
- tests.

Do not create separate frontend/backend hash definitions for persisted history. The server is authoritative.

## 12. Line-plot revision diff

Diff line plots by stable ID and support at least:

```text
old absent, new present      → Added
old present, new absent      → Removed
same ID, Name changed        → Renamed
same ID, ContentHash changed → ContentChanged
```

A rename and content change may both be reported for one logical line plot. Do not dump raw GeoJSON into ordinary
revision history UI; present a readable line-plot section using the same nested diff conventions as the rest of Cave
history.

## 13. Complete semantic no-op definition
A candidate is a semantic no-op only when all authoritative aggregate parts are unchanged:

```text
Cave scalar fields
tags
Entrances
Files
line plots
```

For line plots, compare stable ID, name, and authoritative content hash. Collection ordering alone must never create a
revision.

Examples:

- line-plot rename only → real change;
- line-plot content replacement only → real change;
- File display-name/type change only → real change;
- File removal only → real change;
- line-plot/File collection reorder only → no-op;
- upload staged File then remove it before Save → staged orphan does not make desired state non-empty;
- one request changing field + Entrance + File + line plot → exactly one revision.

The proposal version no-op rule uses the same authoritative aggregate semantics against its immutable base revision.

## 14. Proposal authoring boundary

Current View access remains sufficient to submit a proposal. Direct publication still requires the existing manager/admin
permission. Do not change those authorization rules while unifying the editor model.

The proposer should author substantially the same desired aggregate as a direct editor, but Submit persists an immutable
proposal version rather than published Cave state.

A proposal may stage Files before a `CaveChangeRequest` exists. The staging lifecycle therefore cannot require request
ownership as its fundamental File ownership model.
## 15. Immutable proposal File meaning

`CaveProposalSnapshotV1.Files` already expresses complete File dispositions. Preserve that complete-desired-state model,
but make each proposal version authoritative for the exact File identities/content it proposes.

Do not allow mutable request-level staging to make an older proposal version mean different bytes later.

Required property:

```text
ProposalVersion V1
    references exact staged/published File IDs/content
new ProposalVersion V2
    may reference a different File set
V1 remains unchanged and reviewable as originally authored
```

If request-level staging links remain as lifecycle/cleanup indexes, they are not the semantic source of truth for an
immutable proposal version. The proposal snapshot/version must be sufficient to determine its exact File intent.

A File-only proposal remains valid. Rejected proposal assets that were never published may eventually expire/clean up;
that cleanup must not mutate the historical meaning of proposal metadata.

## 16. Immutable proposal line-plot meaning

Published line-plot history does not store full GeoJSON yet, but a pending proposal version **must** preserve the exact
line-plot content proposed. A mutable current `CaveGeoJson` row cannot be the meaning of an old proposal version.

For this transitional model, include exact desired line-plot payloads in `CaveProposalSnapshotV1`, conceptually:

```text
Id / logical identity
Name
GeoJson exact proposed payload
```

This is intentional duplication in proposal JSON, not a general historical payload archive. When a future immutable
line-plot-version model lands, proposal versions can reference immutable version IDs instead.
## 17. Proposal approval

Approval reconstructs the exact selected immutable proposal version and publishes it through the same aggregate Cave
publication semantics used by direct manager edits. Do not maintain a second, independently evolving Cave mutation
implementation for proposal approval.

Approval must:

1. load the selected immutable proposal version;
2. verify current request/reviewer authorization and current Cave visibility;
3. validate stale-base/current-revision rules;
4. reconstruct the exact desired aggregate, including exact File identities and line-plot bodies;
5. revalidate current reference/business invariants;
6. publish normalized Cave state and exactly one `CaveRevision` transactionally;
7. persist request status/reviewer/provenance linkage in that transaction;
8. never substitute another proposal version or mutable current line-plot content.

Rejection preserves request/version/reviewer audit data but never mutates published Cave state or creates a revision.

If a proposal becomes stale, do not silently rebase it. Explicit re-review creates another immutable version against the
current published revision under the existing architecture.

## 18. Tenant and permission invariants

Every new path preserves the existing rules:

- current View access is enough to propose;
- current Manager/admin authorization is required for direct published edits and review as currently defined;
- losing current Cave visibility removes later access to Cave proposal/history/staged resources under the existing model;
- staged File IDs are always account-qualified;
- existing line-plot IDs are validated against both current account and target Cave;
- `IgnoreQueryFilters()` always has an explicit account predicate and tenant-isolation coverage;
- browser IDs never authorize cross-account/cross-Cave reassignment.
## 19. Existing historical invariants that remain unchanged

This work does not reopen previously settled semantics:

- persisted snapshot/proposal enum values remain string member names, not numeric tokens;
- only People fields support interactive free-form tag creation;
- Biology, Archeology, geology/status/reference fields continue to use eligible existing database tags;
- Alternate Names remain canonicalized strings, not TagType records;
- existing People identity/matching behavior remains invariant-culture case-insensitive as documented;
- a server-allocated proposed Entrance ID remains that Entrance's identity after publication;
- multiple pending requests for one Cave remain supported;
- hard deletion remains blocked while unresolved proposals exist;
- current reviewer authorization is evaluated at review time, not inferred from historical proposal metadata.

Do not broaden this task into enum, tag, People, or permission model redesign.

## 20. Import and archive compatibility

High-volume import remains set-oriented/bounded and must not be routed through the interactive editor service.

If shared snapshot construction changes from five bounded projection groups per chunk to include line plots, preserve
the bounded projection design and supported 10,000-Cave/~15,000-Entrance workload. Add only the necessary projection;
do not materialize tracked aggregate graphs for snapshot generation. Retained File object locators are not snapshot
input and must not add another per-chunk projection.

Archive export continues to represent **current** Cave state and current published assets. It must not suddenly include:

- every retained historical File object;
- expired/abandoned staging;
- rejected proposal assets;
- every historical proposal line-plot body.

Archive import should produce current state compatible with the new published snapshot fields. Change archive format
only if the new authoritative current-state contract truly requires it.

## 21. Hard deletion versus revisioned removal
Keep these concepts separate:

```text
revisioned remove File from current Cave
    ≠
explicit hard purge of historical data
```

Ordinary removal retains published immutable bytes through `RetainedCaveFileObject`. Never-published expired staging is
physically deleted. The existing explicit Cave/account hard-purge/reset path must be extended to remove matching retained
locator rows and delete their object addresses after the relational purge commits; de-duplicate addresses before external
delete so a current and retained reference cannot cause duplicate destructive calls. Do not invent any other purge mode.

For current `CaveGeoJson`, retain existing hard-delete behavior. There is no new historical GeoJSON backup system in
this plan.

## 22. Durable-document transition

The current `docs/cave-revisions.md` still describes branch mechanics that this plan intentionally replaces, including
approval-attempt blob copying and historical File binaries being disposable after removal.

During implementation, do not partially rewrite that durable document after every phase. Once the target behavior is
actually implemented and validated, update it coherently to describe the final truth:

- aggregate editor/publication includes Files and line plots;
- stale direct edits require the client-loaded revision;
- File staging is upload-once and lifecycle is relational;
- published File bytes are retained across ordinary revisioned removal;
- line-plot snapshot/diff/proposal semantics are explicit;
- current line-plot payload storage remains `CaveGeoJson` `jsonb` pending a later redesign.

Then remove the temporary active-plan warning/routing if the implementation plan is no longer needed for future agents.
