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

The current Azure adapter lowercases the partition before constructing the Blob container client. Container-initialization caches are scoped by the normalized Blob endpoint plus container path, not by container name alone, so separate storage endpoints in the same process cannot reuse another endpoint's successful initialization state. Query credentials are deliberately excluded from that cache key. This abstraction is intentionally scoped; older archive/photo/trip/storage paths may still use Azure-specific APIs directly.

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

Cleanup is attempted immediately after the owning relational mutation commits. Explicit targets created by that mutation are processed to completion in bounded target/reference-query batches; this matters for supported large sync imports and hard deletes with many Files. Older pending ordinary File-cleanup debt is retried separately in a **bounded** opportunistic batch when later Cave/File authoring activity occurs. There is currently no dedicated background relay for ordinary File cleanup, so an inactive account can retain failed cleanup debt until later activity or another explicit destructive workflow (such as Reset Account) assumes responsibility. This is durable/retryable cleanup, not a guarantee of time-bounded physical deletion.

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

If approval removes a currently published File, its `CaveAssetRemoval` cleanup target is created inside the approval transaction but object deletion does not run until the **outer approval transaction has committed**. Rejection similarly commits request state, staged-File row removal, and unpublished cleanup responsibility before attempting object deletion.

## Other destructive paths

Explicit Cave hard deletion and sync-import deletion commit `CaveAssetRemoval` targets with their relational/revision result, then process those targets after commit. Abandoned staging and proposal cleanup use `UnpublishedFileRemoval`. Account Reset folds both ordinary File-cleanup purposes into durable `AccountReset` responsibility before purging their original targets.

Archive cleanup remains Azure-specific rather than using this File outbox, but follows the same safety direction: a completed final archive is not falsified by later temp/prune deletion failure. Archive generation bounds in-memory File prefetch to eight payloads of at most 8 MiB each; larger File payloads use one ETag-pinned seekable Azure read stream at a time directly into the tar, so very large account archives do not require equivalent backend RAM or local spool capacity. Archive list reads do not create a missing account container. Current archive operations use job-scoped temp/final keys. Publication reads the completed temp blob's length/ETag, stages Azure-to-Azure ranges into the final block blob with `StageBlockFromUriAsync`, pins every source range to that ETag, and publishes only by a final `CommitBlockListAsync` guarded with `If-None-Match: *`. Normal publication therefore does not relay archive bytes through Planarian and cannot overwrite an existing final object; uncommitted destination blocks are not a published archive. Source authorization is a backend-only, blob-scoped, read-only service SAS generated fresh for each block with no more than one hour of validity. It is never returned to the browser and does not restore the old client-facing SAS download model. This currently depends on the File storage connection string supplying shared-key credentials, which is the existing deployment model; if File storage later moves to Microsoft Entra credentials, archive source authorization must move with it rather than reintroducing client-facing SAS URLs. If publication fails after the final commit may have reached storage, the coordinator reconciles the completed temp and final bytes before deciding whether publication succeeded, definitely failed, or remains uncertain. New-version replicas serialize per-account archive jobs, and archive deletion takes that same PostgreSQL lease so another replica cannot remove a final object while its generating job is still active. Cleanup of another job's abandoned temp object is age-gated for mixed-version deployment safety and will not remove the source of a genuine pending legacy Blob Copy; later jobs retry stale temp and prune cleanup. All destructive paths preserve the fundamental ordering: do not destroy external bytes before the relational mutation that makes them unreachable has committed.

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
