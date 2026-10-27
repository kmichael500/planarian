# Files and storage

This page explains why File metadata lives in PostgreSQL while File bytes live in object storage, and how Cave-owned Files participate in the same revision semantics as line plots without forcing both payload types into the same storage model.

## Two durability systems

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

The current Azure adapter lowercases the partition before constructing the Blob container client. This abstraction is intentionally scoped; older archive/photo/trip/storage paths may still use Azure-specific APIs directly.

The application uses Azure's delete-if-exists operation for object cleanup. Planarian does not create Blob snapshots in this lifecycle, but provider policies can still affect physical deletion. For example, Azure requires snapshots to be deleted with the base blob when snapshots exist, and immutability/retention features can reject or defer deletion. Such provider failures leave the durable cleanup target pending rather than falsifying relational Cave state. Application history policy therefore means “do not deliberately retain removed payloads as Cave history,” not “bypass storage-provider retention guarantees.”

## Cave asset semantics

Files and line plots are different storage implementations of the same **Cave-owned asset lifecycle**:

- stable identity while current;
- user-facing name/metadata;
- add/change/remove through aggregate Cave publication;
- revision history records the semantic change;
- successful removal does not preserve the old payload indefinitely.

The implementation deliberately does **not** introduce a generic `CaveAsset` persistence hierarchy. File bytes live in object storage; line-plot GeoJSON currently lives in `CaveGeoJson` JSONB. Shared code is limited to behavior that is truly common, such as stable-ID revision diffing and post-commit external-storage cleanup.

Published history is lightweight:

- File revisions preserve stable File ID, historical `Name`, `Extension`, and File-type metadata;
- line-plot revisions preserve stable ID, historical name, and deterministic content hash;
- neither published-history shape contains the removed payload or a private storage locator.

Immutable proposal versions still retain the payload necessary to review exactly what was proposed. File proposals reference staged File objects; line-plot proposals currently embed exact GeoJSON. That storage-specific difference is intentional until line plots are actually migrated into the File model.

## Authoring File lifecycle

`StageAuthoringFile` allocates the File ID before upload and uses a stable application-assigned object key independent of staging/published state:

~~~text
objects/files/{fileId}
~~~

The uploaded extension is metadata, not part of object identity. The application allocates a unique address and treats it as immutable for the File lifecycle.

`IObjectStorage.CreateAsync` is intentionally **create-only**. The Azure adapter supplies an `If-None-Match: *` condition so a generated-ID collision or future retry/path bug cannot overwrite bytes already stored at that address. If object creation itself fails, relational persistence does not proceed. Compensation deletes run only after this request has positively completed object creation; a create collision never deletes the pre-existing object.

~~~mermaid
stateDiagram-v2
    [*] --> Staged: upload once (CaveId null, ExpiresOn set)
    Staged --> Published: Save / proposal approval (same File ID and object address)
    Staged --> Deleted: expires / abandoned before publication
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

The upload happens **before** the short relational insert transaction so a large external write does not hold a database transaction open. If the relational insert fails, the service best-effort deletes the newly written object.

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
7. remove the cleanup target only after deletion succeeds, the object is already absent, or the address is proven to belong to a live File and therefore must not be deleted.

If external deletion fails, the File is still correctly removed from current Cave state and revision history remains correct. The durable cleanup target stays for retry and the failure is logged as a structured warning. Ordinary `CaveAssetRemoval` debt does **not** trigger the account-wide write block used for failed Reset Account cleanup.

Cleanup is attempted immediately after the owning publication commits. Older pending asset-removal debt is also retried in a **bounded** batch when later Cave/File authoring activity occurs. There is currently no dedicated background relay for ordinary asset-removal debt, so an inactive account can retain failed cleanup debt until later activity or another explicit destructive workflow (such as Reset Account) assumes responsibility. This is durable/retryable cleanup, not a guarantee of time-bounded physical deletion.

The historical revision still knows that the File existed and retains its historical filename/type metadata. It does not promise that the old bytes remain downloadable.

## StorageCleanupTarget

`StorageCleanupTarget` is an outbox for **post-commit external cleanup**, not historical content storage.

Relevant fields:

- `AccountId`;
- `StoragePartition`;
- `StorageKey`;
- `IsContainer`;
- `Purpose`.

Current purposes:

- `CaveAssetRemoval` — ordinary removed File payload cleanup;
- `AccountReset` — stronger Reset Account cleanup debt.

The purpose distinction matters. Reset Account cleanup can block subsequent account writes until the reset's external cleanup is resolved. Ordinary asset-removal debt is retryable but does not freeze unrelated writes.

Reset Account also captures pending `CaveAssetRemoval` addresses before its relational purge and converts responsibility for them into its own reset cleanup targets. That prevents unresolved ordinary cleanup from being orphaned by account reset.

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
6. unused/discarded staged objects may be cleaned after commit.

If approval removes a currently published File, its `CaveAssetRemoval` cleanup target is created inside the approval transaction but object deletion does not run until the **outer approval transaction has committed**.

## Other destructive paths

Explicit Cave hard deletion, bulk-import deletion, account reset, abandoned staging, and archive cleanup have their own orchestration rules. They must all preserve the same fundamental ordering: do not destroy external bytes before the relational mutation that makes them unreachable has committed.

Account Reset uses durable `StorageCleanupTarget` rows with purpose `AccountReset`. Some older specialized deletion paths still use post-commit best-effort external cleanup; their validation status is tracked in the [completion status ledger](../../CAVE_REVISIONS_COMPLETION_STATUS.md).

## Read the code in this order

1. `Planarian/Planarian/Modules/Files/Controllers/FileController.cs`
2. `Planarian/Planarian/Modules/Files/Services/FileService.cs`
3. `Planarian/Planarian/Shared/Services/IObjectStorage.cs`
4. `Planarian/Planarian/Shared/Services/AzureBlobObjectStorage.cs`
5. `Planarian/Planarian.Model/Database/Entities/RidgeWalker/File.cs`
6. `Planarian/Planarian.Model/Database/Entities/RidgeWalker/StorageCleanupTarget.cs`
7. `Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs`
8. File handling inside `CaveService.SaveCaveAsync`
9. [ADR 0007 — Cave asset removal cleanup](decisions/0007-cave-asset-removal-cleanup.md)
