# Files and storage


## Durability boundary

A Cave File spans:

- **PostgreSQL** — identity, account ownership, Cave association, filename metadata, File type, staging expiration;
- **object storage** — byte content that the Cave/File lifecycle treats as immutable once written to its allocated address.

Those systems cannot participate in one atomic transaction. Relational Cave state and its revision therefore commit first; destructive external cleanup happens only afterward.

## Storage boundary

For the affected Cave/File lifecycle:

~~~mermaid
flowchart LR
    FileService["FileService"]
    Boundary["IObjectStorage"]
    Azure["AzureBlobObjectStorage"]
    Blob["Azure Blob Storage"]

    FileService -->|"Create / Open / DeleteIfExists"| Boundary
    Boundary -->|"adapter implementation"| Azure
    Azure -->|"Azure SDK"| Blob
~~~

`StorageObjectAddress` contains:

- `Partition` — logical namespace; Azure maps it to a container;
- `Key` — object key within that partition.

The Azure adapter normalizes container names and scopes container-initialization state by storage endpoint and container. Provider retention or immutability policy can still delay or reject physical deletion; such failures leave cleanup responsibility pending without changing the committed Cave state.

## Cave asset semantics

Files and line plots are different storage implementations of the same **Cave-owned asset lifecycle**:

- stable identity while current;
- user-facing name/metadata;
- add/change/remove through aggregate Cave publication;
- revision history records the semantic change;
- successful removal does not preserve the old payload indefinitely.

File bytes remain in object storage and line-plot GeoJSON remains in `CaveGeoJson` JSONB; there is no generic `CaveAsset` persistence hierarchy.

Published history is lightweight:

- File revisions preserve stable File ID, historical `Name`, `Extension`, and File-type metadata;
- line-plot revisions preserve stable ID, historical name, and deterministic content hash;
- neither published-history shape contains the removed payload or a private storage locator.

Immutable proposal versions still retain the payload necessary to review exactly what was proposed. File proposals reference staged File objects; line-plot proposals embed exact GeoJSON. That storage-specific difference remains until line plots are migrated into the File model.

## Authoring File lifecycle

`StageAuthoringFile` allocates the File ID before upload and uses a stable application-assigned object key independent of staging/published state:

~~~text
objects/files/{fileId}
~~~

The uploaded extension is metadata, not part of object identity. The application allocates a unique address and treats it as immutable for the File lifecycle.

`IObjectStorage.CreateAsync` is **create-only**. The Azure adapter supplies an `If-None-Match: *` condition so a generated-ID collision or future retry/path bug cannot overwrite bytes already stored at that address. If object creation itself fails, relational persistence does not proceed. A known create collision never grants deletion ownership of the pre-existing object; uncertain write/commit outcomes are reconciled through live File references and durable cleanup responsibility.

~~~mermaid
stateDiagram-v2
    [*] --> Staged: upload once (CaveId null, ExpiresOn set)
    Staged --> Published: Save / proposal approval (same File ID and object address)
    Staged --> CleanupQueued: expires / abandoned / proposal discard
    Published --> CleanupQueued: ordinary revisioned removal
    CleanupQueued --> Deleted: post-commit object cleanup succeeds
    CleanupQueued --> CleanupQueued: external delete fails; durable retry remains
~~~

### Staged

Relational shape:

- `AccountId` = current account;
- `CaveId` = null;
- `ExpiresOn` = roughly 10-day cleanup deadline;
- `BlobContainer`/`BlobKey` store the provider address;
- object already exists.

The upload happens **before** the short relational insert transaction so a large external write does not hold a database transaction open. If the object write or relational insert has an uncertain outcome, reconciliation checks live File references and records durable unpublished-file cleanup responsibility rather than directly deleting an address that might already be live.

### Cave/Entrance CSV import sources

Cave and Entrance CSV uploads are **import provenance, not Cave Files**. They live in the dedicated
`CaveImportSources` table with an account, normalized `FileName`, immutable object-storage address,
and nullable `ExpiresOn`. New uploads allocate the final import-source ID before object creation and
use the extension-independent `temp/import/caves/{sourceId}` identity. Authoring Cave Files use the
separate `objects/files/{fileId}` namespace.

The upload happens before the short relational insert transaction, using the same create-only
`IObjectStorage` contract as Cave Files. An address collision cannot overwrite an existing object.
If object creation has an uncertain outcome, durable cleanup reconciliation first checks both live
Cave File addresses and live import-source addresses before deleting anything.

A successful non-dry-run import promotes the **same `CaveImportSource` row and object** to durable
provenance by clearing `ExpiresOn` in the same PostgreSQL transaction that creates the
`CaveImportBatch` and applies the import. The object address and bytes do not change. A retained
source (`ExpiresOn = null`) may deliberately be reused for another apply. Dry runs, parse/planning
failures, concurrency failures, and rolled-back executions never promote a temporary source.

Promotion locks the import-source row and revalidates account ownership, the active account storage
container, and the import key namespace. When a temporary source is promoted, the final update gates
on `ExpiresOn > clock_timestamp()` so expiration is checked against PostgreSQL wall-clock time at the
mutation itself rather than transaction-start time.

Expired-source cleanup is independent from Cave File cleanup. Each opportunistic sweep claims at
most 16 unretained `CaveImportSources` in stable ID order under `FOR UPDATE`, excludes any source
already referenced by an import batch, queues durable `ImportSourceRemoval` cleanup debt, and removes
the relational rows in the same transaction. Because promotion and expiration cleanup lock the same
source row, whichever lifecycle transition commits first is observed by the other.

Import-source access is workflow-specific; generic Cave File endpoints cannot read, delete, claim,
publish, or FileType-administer an import source. Account Reset is the intentional destructive
boundary that removes both Cave Files and import provenance for the account.

The completion migration predates dedicated import provenance in production data. It therefore moves
existing account-level `Files` whose object key is under the legacy `temp/import/caves/` prefix into
`CaveImportSources`, conservatively treating them as durable because historical data cannot reliably
distinguish applied imports from abandoned uploads. Older upload code could build the object key
before the SaveChanges interceptor assigned the persisted 10-character File ID, so historical keys
may contain a temporary GUID; validation intentionally preserves any prefix-valid historical key
rather than requiring the current source ID in the key. Migration-only state preserves the old File metadata needed to reconstruct the v32 representation on downgrade. An original File type may legitimately be deleted after the upgrade because CSV provenance no longer references it. Downgrade restores the original File-type ID if it is still a valid account/default File type; otherwise it selects the account's `Other` File type (or a global default `Other`). If no compatible File type exists, downgrade fails explicitly and atomically rather than recreating a dangling foreign key.

### Published

Publication changes relational lifecycle only:

- same File ID;
- same storage address;
- `CaveId` = target Cave;
- `ExpiresOn` = null.

There is no staging-to-published copy/rename/re-upload for the aggregate authoring flow.

### Removed from current Cave state

Ordinary revisioned removal is payload-destructive, but the order is fail-safe:

1. validate the current File belongs to the current account/Cave;
2. inside the same relational transaction, create a `StorageCleanupTarget` with purpose `CaveAssetRemoval` for the exact object address;
3. delete the live File row/current Cave membership;
4. publish the Cave revision without that File;
5. commit PostgreSQL;
6. only after commit, attempt `DeleteIfExists` on the object;
7. remove the cleanup target only after deletion succeeds, the object is already absent, or the address is proven to belong to a live File or import source and therefore must not be deleted.

If external deletion fails, the File is still correctly removed from current Cave state and revision history remains correct. The durable cleanup target stays for retry and the failure is logged as a structured warning. Ordinary `CaveAssetRemoval` debt does **not** trigger the account-wide write block used for failed Reset Account cleanup.

Cleanup is attempted after the owning relational mutation commits. Targets created by that mutation are processed in bounded batches; older ordinary cleanup debt is retried opportunistically during later Cave/File authoring. Failed deletion attempts update the target's `ModifiedOn` retry-order timestamp so permanently failing addresses cannot starve other work in the 16-target opportunistic batches. No periodic inactive-account sweeper exists, so inactive accounts can retain failed cleanup debt or expired unlinked staging until later activity or an explicit destructive workflow assumes responsibility.

Never-published staged Files use the same ordering with purpose `UnpublishedFileRemoval`. Explicit discard, expiration, proposal rejection, and unused staged Files discarded during approval queue the exact object address and remove the File row in the same relational commit; object deletion follows afterward. Provider failure leaves the cleanup target pending instead of losing responsibility for the object.

The historical revision still knows that a published File existed and retains its historical filename/type metadata. It does not promise that the old bytes remain downloadable.

## StorageCleanupTarget

`StorageCleanupTarget` is an outbox for **post-commit external cleanup**, not historical content storage.

Relevant fields:

- `AccountId`;
- `StoragePartition`;
- `StorageKey`;
- `IsContainer`;
- `Purpose`.

Current purposes:

- `CaveAssetRemoval` — published File payload cleanup, including hard Cave and sync-import deletion;
- `UnpublishedFileRemoval` — never-published staged File cleanup after discard, expiration, or proposal cleanup;
- `ImportSourceRemoval` — expired, unretained CSV import-source cleanup;
- `AccountReset` — stronger Reset Account cleanup debt.

The purpose distinction matters. Reset Account cleanup can block subsequent account writes until the reset's external cleanup is resolved. Ordinary File cleanup debt is retryable but does not freeze unrelated writes.

Reset Account captures pending non-reset File-cleanup addresses before its relational purge and converts responsibility for them into its own reset cleanup targets. That prevents unresolved published or unpublished cleanup from being orphaned by account reset.

## Line-plot payload deletion

Line plots do not need the external cleanup outbox because `CaveGeoJson` lives in the same PostgreSQL transaction as Cave publication. Removing a line plot deletes its current `CaveGeoJson` row and publishes the revision atomically.

This is a storage-level difference, not a user-facing lifecycle difference: both Files and line plots disappear from current state after publication while historical revisions retain only lightweight semantic metadata.

## File metadata

File separates:

- `Name` — editable filename stem;
- `Extension` — uploaded-file extension;
- `FileTypeTagId` — current type reference.

The persistence model enforces filename/extension constraints so malformed names cannot be persisted only because they bypassed one UI path.

## Proposal Files

A proposal version records exact File intent. For `PublishStaged` Files:

1. the staged File is already uploaded;
2. the proposal version records stable File ID and metadata intent;
3. approval verifies the external object exists;
4. approval relationally associates that File with the Cave;
5. no copy occurs;
6. unused/discarded staged objects are queued as `UnpublishedFileRemoval` cleanup inside the approval transaction and processed after commit.

If approval removes a currently published File, its `CaveAssetRemoval` cleanup target is created inside the approval transaction but object deletion does not run until the **outer approval transaction has committed**. Rejection similarly commits request state, staged-File row removal, and unpublished cleanup responsibility before attempting object deletion. Approval and rejection lock affected File rows in stable ID order before releasing proposal claims, preventing a concurrent proposal claim from racing with unreferenced-File deletion.

## Other destructive paths

Hard Cave deletion and sync-import deletion use `CaveAssetRemoval`; abandoned staging and proposal cleanup use `UnpublishedFileRemoval`; Account Reset assumes unresolved File-cleanup responsibility before purging the account. Archive storage uses a separate Azure-specific lifecycle. See [Destructive operations](destructive-operations.md).
