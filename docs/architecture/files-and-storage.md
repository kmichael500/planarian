# Files and storage

This page explains why File metadata lives in PostgreSQL while File bytes live in object storage, and how the completion branch changed the File lifecycle.

## Two durability systems

A Cave File spans:

- **PostgreSQL** — identity, account ownership, Cave association, filename metadata, File type, staging expiration;
- **object storage** — byte content that the Cave/File lifecycle treats as immutable once written to its allocated address.

Those systems cannot participate in one atomic transaction.

The design therefore chooses an ordering where relational truth remains authoritative and destructive external cleanup happens after relational commit.

## Storage boundary

For the affected Cave/File publication lifecycle:

~~~mermaid
flowchart LR
    FileService["FileService"]
    Boundary["IObjectStorage"]
    Azure["AzureBlobObjectStorage"]
    Blob["Azure Blob Storage"]

    FileService -->|"Put / Open / DeleteIfExists"| Boundary
    Boundary -->|"adapter implementation"| Azure
    Azure -->|"Azure SDK"| Blob
~~~

StorageObjectAddress contains:

- Partition — logical namespace; Azure maps it to a container;
- Key — object key within that partition.

The current Azure adapter lowercases the partition before constructing the Blob container client.

This abstraction is intentionally scoped. Some older archive/photo/trip/storage paths still use BlobService or Azure SDK types directly.

## New authoring File lifecycle

StageAuthoringFile allocates the File ID before upload and uses a stable application-assigned object key independent of staging/published state:

~~~text
objects/files/{fileId}
~~~

The uploaded extension is metadata, not part of object identity. The application allocates a unique address and treats it as immutable for the File lifecycle. `AzureBlobObjectStorage.PutAsync` does not itself expose a separate write-if-absent contract, which is why provider-level immutability/retry behavior remains an explicit Azurite validation gap.

~~~mermaid
stateDiagram-v2
    [*] --> Staged: upload once (CaveId null, ExpiresOn set)
    Staged --> Published: Save / proposal approval (same File ID and object address)
    Staged --> Deleted: expires / abandoned before publication
    Published --> RetainedLocator: ordinary revisioned removal; bytes stay
    Published --> Deleted: explicit hard purge
    RetainedLocator --> Deleted: explicit hard purge
~~~

### Staged

Relational shape:

- AccountId = current account;
- CaveId = null;
- ExpiresOn = roughly 10-day cleanup deadline;
- BlobContainer/BlobKey store the provider address;
- object already exists.

The upload happens **before** the short relational insert transaction so a large external write does not hold a database transaction open.

If the relational insert fails, the service best-effort deletes the newly written object.

### Published

Publication changes relational lifecycle:

- same File ID;
- same storage address;
- CaveId = target Cave;
- ExpiresOn = null.

There is no staging-to-published copy/rename/re-upload for the new authoring flow.

### Removed from current Cave state

Ordinary revisioned removal is not physical destruction.

Before deleting the live File row, CaveRepository records:

- AccountId;
- CaveId;
- historical FileId;
- StoragePartition;
- StorageKey.

in RetainedCaveFileObject.

The File row is then removed so current-domain FileTypeTag/Cave relationships no longer remain alive.

The object bytes remain.

### Hard purge

Explicit irreversible Cave hard purge removes both current and retained relational locators, commits that relational result, and only then best-effort deletes the de-duplicated object addresses. Account Reset follows the same commit-before-cleanup principle for captured File objects, but then also deletes the whole account container; that final container delete can currently throw after the relational reset has committed.

## Why RetainedCaveFileObject is not a File version

It deliberately has no live-domain foreign key to File, Cave, or TagType.

Its job is narrow: preserve a private provider-neutral locator from a historical stable File ID to the exact published bytes after the current File row no longer exists.

Historical user-facing metadata belongs in CavePublishedSnapshotV1.

The retained locator is not exposed in revision JSON.

## File metadata

File now separates:

- Name — editable filename stem;
- Extension — uploaded-file extension;
- FileTypeTagId — current type reference.

The persistence model enforces filename/extension constraints so malformed names cannot be persisted only because they bypassed one UI path.

## Proposal Files

A proposal version records exact File intent.

For PublishStaged Files:

1. staged File is already uploaded;
2. proposal version records stable File ID and metadata intent;
3. approval verifies the external object exists;
4. approval relationally associates that File with the Cave;
5. no copy occurs;
6. unused/discarded staged objects may be cleaned after commit.

## Cleanup behavior

Expired never-published staged Files can be physically deleted because no published revision ever depended on them.

Published File bytes are retained across ordinary removal.

External deletion is generally best-effort where relational state has already committed, because PostgreSQL cannot roll back Blob Storage.

## Validation ownership

Provider-contract tests, retry/immutability behavior, and the operational policy for abandoned staging are validation/operations concerns rather than additional architecture rules. Their current status belongs in the [completion status ledger](../../CAVE_REVISIONS_COMPLETION_STATUS.md) so this lifecycle description stays stable as tests are added.

## Read the code in this order

1. Planarian/Planarian/Modules/Files/Controllers/FileController.cs
2. Planarian/Planarian/Modules/Files/Services/FileService.cs
3. Planarian/Planarian/Shared/Services/IObjectStorage.cs
4. Planarian/Planarian/Shared/Services/AzureBlobObjectStorage.cs
5. Planarian/Planarian.Model/Database/Entities/RidgeWalker/File.cs
6. Planarian/Planarian.Model/Database/Entities/RidgeWalker/RetainedCaveFileObject.cs
7. Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs
8. File handling inside CaveService.SaveCaveAsync
