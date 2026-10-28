# Destructive operations

Destructive workflows deserve separate documentation because the safety question is not merely "did the database transaction succeed?" They often cross PostgreSQL and object storage.

## General rule

When deletion affects both relational state and external objects:

> **Commit the authoritative relational result first, then delete external bytes.**

This avoids the worst failure mode: deleting bytes first and then rolling back the database, leaving relational history pointing to content that no longer exists.

The tradeoff is that post-commit external cleanup can fail and leave cleanup debt. Such failures must be safe, observable enough to understand, and retryable where appropriate.

## Ordinary published File removal

Removing a File from current Cave state:

- queues `StorageCleanupTarget` with purpose `CaveAssetRemoval` in the relational publication transaction;
- removes the live File row;
- publishes the new Cave revision;
- after commit, deletes the external bytes and clears the target;
- if external deletion fails, logs the failure and keeps durable cleanup debt for retry without rolling back the already-published revision.

The owning request tries the new cleanup target immediately after commit. Later Cave/File authoring activity processes a bounded batch of older ordinary File cleanup targets. Published/hard-delete payloads use `CaveAssetRemoval`; never-published staged payloads use `UnpublishedFileRemoval`. There is no dedicated background relay for these purposes today, so “retryable” does not mean “guaranteed to clear while an account is inactive.” Reset Account explicitly absorbs both ordinary File-cleanup purposes into its stronger cleanup workflow.

Never-published Files follow the same ordering: explicit discard, expiration, proposal rejection, and proposal-approval cleanup commit the File-row removal plus `UnpublishedFileRemoval` target before attempting object deletion. A provider failure therefore leaves durable retryable debt instead of an untracked orphan.

This matches line-plot semantics at the revision level: removed payload is not part of historical state. The storage mechanics differ because `CaveGeoJson` can be deleted transactionally in PostgreSQL while File bytes cannot.

See [Files and storage](files-and-storage.md).

## Hard Cave delete

~~~mermaid
sequenceDiagram
    actor Manager
    participant Cave as CaveService
    participant Repo as CaveRepository
    participant Mutation as Cave mutation boundary
    participant DB as PostgreSQL
    participant Storage as Object storage

    Manager->>Cave: Delete Cave
    Cave->>Repo: Lock Cave for hard delete
    Cave->>Repo: Verify no open change requests
    Cave->>Mutation: Prepare current revision/live snapshot
    Cave->>Repo: Remove aggregate and queue CaveAssetRemoval targets
    Cave->>Mutation: Publish Delete revision
    Mutation->>DB: Persist tombstone history + cleanup debt
    DB-->>Cave: Commit
    loop each committed cleanup target
        Cave->>Storage: Delete object
        alt delete succeeds or object already absent
            Cave->>DB: Clear cleanup target
        else provider failure
            Note over DB,Storage: Keep durable target for retry
        end
    end
~~~

Open CaveChangeRequests block hard deletion.

The “Cave mutation boundary” above is `CaveMutationCoordinator` delegating transaction/history work to `CaveMutationRepository`. The delete revision preserves the final semantic Cave snapshot even after the live Cave row is gone.

## Sync-import delete

Cave import with sync enabled can also delete Caves.

The execution repository performs equivalent safety checks in a set-oriented way:

- verify current account;
- reject open proposal conflicts;
- collect current live File object addresses and queue `CaveAssetRemoval` targets;
- remove relational data;
- publish bulk delete revisions;
- commit cleanup responsibility with the import;
- let ImportService process those exact cleanup-target IDs after commit; provider failures leave the targets for retry.

## Account reset

Account Reset is broader than Cave hard delete.

Current sequence:

~~~mermaid
sequenceDiagram
    actor Admin
    participant Service as AccountService
    participant Repo as AccountRepository
    participant DB as PostgreSQL
    participant Storage as File/object storage

    Admin->>Service: Reset Account
    Service->>Repo: ExecuteResetAsync
    Repo->>DB: Begin transaction
    Service->>DB: Acquire exclusive account maintenance lease
    Repo->>DB: Capture live File + pending asset-cleanup addresses
    Repo->>DB: Persist cleanup targets; purge Cave/account graph
    Repo->>DB: Commit
    loop persisted cleanup targets
        Service->>Storage: Delete object or account container
        Service->>DB: Remove successfully cleaned target
    end
    Service-->>Admin: Relational reset complete; storage cleanup status
~~~

The relational reset is committed **before** external cleanup.

Before the commit, the repository rejects cleanup addresses that would delete bytes also referenced by another
tenant's live relational File. It also fails closed if a live File has only one half of its object-storage address,
because purging that row would otherwise lose the information needed to prove cleanup responsibility. Pending ordinary
File-cleanup addresses—both `CaveAssetRemoval` and `UnpublishedFileRemoval`—are folded into Reset Account cleanup
responsibility before their original targets are purged. This preflight fails closed, leaving relational data and
storage untouched rather than committing an unsafe or unrecoverable purge. Cross-tenant reference checks operate in
bounded key batches to avoid one query per stored object.

That ordering is intentional.

## Reset Account partial-success contract and recovery

The API distinguishes `relationalResetCompleted` from `storageCleanupCompleted`.
Provider-neutral object locators and the account-container deletion target are
written to `StorageCleanupTargets` with purpose `AccountReset` in the **same PostgreSQL transaction**
as the relational purge. Storage deletion starts only after commit. Deletion
targets are removed in bounded batches after successful storage operations;
crashes and failures leave durable targets for the administrator's
`POST /api/account/reset/cleanup` retry, exposed in Account Settings. The
`GET /api/account/reset/cleanup` status survives page refresh.

Until all targets are cleared, the account's mutating HTTP requests are
rejected. This matters: a retry must not erase new files uploaded into a
previously deleted container or reuse an object key. Reads remain available.
The account's identity, users and feature settings remain intact.

Reset and account archive creation use PostgreSQL **session-level advisory
locks**. Ordinary account-scoped mutating HTTP requests and full background
archive jobs hold a shared per-account maintenance lease. Reset and cleanup retry need an
exclusive maintenance lease, which fails closed if another request/archive is active.
Archive jobs acquire the shared maintenance lock and a separate exclusive per-account archive-job lock on the **same PostgreSQL session/lease**, so replicas running this archive-lock contract cannot generate/clean the same account's archive namespace concurrently without consuming a second long-lived lock-pool connection. Mixed-version rollout is handled separately by the stale-age guard on abandoned temp cleanup.
A database-session failure automatically releases its lease. Coordination
uses a separate bounded Npgsql connection pool so holding these long-lived
leases does not starve ordinary EF database connections. The middleware
enforces this after authentication/authorization, on all mutating HTTP verbs,
including bulk import endpoints. The reset/cleanup endpoints use the exclusive
lease themselves instead.

Pre-commit address checks reject foreign-account containers, shared
provider-neutral objects, and **any foreign live File reference to the entire
account container**, even if the key does not appear in this account's own File
table. Cleanup retries re-check foreign live-File references before deleting.
The preflight and cleanup reference queries are bounded in key batches.

Account container deletion intentionally removes stored archives, temporary
archives and import transport files. Azure acknowledges the container-delete
request before all physical cleanup necessarily finishes; a successful API result
means the storage provider accepted the deletion, not that every byte is already
gone or that any configured soft-delete retention has expired. `FileService` invalidates both its own
and the provider-neutral Azure storage initialization caches. Uploads handle
Azure's transient `ContainerBeingDeleted` response with bounded retries
to accommodate asynchronous container deletion.

**Boundaries:** the shared lease covers HTTP requests entering this app and
ArchiveJobCoordinator, not arbitrary direct database writers, external
processes bypassing the app, or hypothetical future background jobs that do
not take the lease. Cross-tenant reference checks cannot prevent an unrelated
non-cooperating writer from adding a foreign reference *after* a check.
Storage operations and Postgres commits are not a distributed transaction;
unavailability can leave recorded cleanup debt. Do not claim stronger
guarantees without a deployment-level audit of every writer.

## Account container also contains archives

ArchiveJobCoordinator writes recent archives under the same account container using an archives/ prefix.

ResetAccount removes the whole account container after relational reset.

Therefore stored server-side account archives are currently removed by Reset Account as part of that container deletion.

**Decision:** archives are intentionally deleted during Reset Account. They are
current-state exports, not a protected recovery/backup channel. The UI explicitly
warns that stored archives are deleted, and the integration tests assert that
Account A's archive objects disappear while Account B's remain. Previously
downloaded archive copies outside Planarian are unaffected.

## Archive jobs

ArchiveJobCoordinator is a background in-process coordinator for account archive generation.

It:

- creates temporary archive objects;
- writes current-state archive content;
- promotes temp to final archive;
- keeps a bounded recent set;
- deletes old archives;
- handles cancellation/cleanup.

The final archive becomes authoritative when the temporary-to-final copy completes. Failure to delete the promoted temporary object or prune an older archive does **not** retroactively report that completed export as failed; the extra object is safe storage debt and a later archive job retries cleanup. In addition to the shared maintenance lease, archive generation takes a separate exclusive per-account archive-job advisory lease, so new-version application replicas serialize archive jobs for that account. Abandoned `archives/temp/` cleanup also requires the temp object to be at least seven days old before another job may delete it. That grace period is intentionally conservative: during a rolling deployment an older replica does not yet take the new archive-job lock, so a fresh temp object cannot safely be assumed abandoned merely because the new replica does not own it. Cancellation/failure cleanup of the current job's own temp key remains best-effort; an undeleted partial temporary archive is retried by a later job after it becomes stale.

This path still uses Azure-specific Blob APIs and is **not** part of the provider-neutral IObjectStorage Cave/File boundary.

The current implementation is a **current-state export**: `ExportService` writes `caves.csv`, `entrances.csv`, current live Cave Files, current CaveGeoJson line plots, and `missing-files.csv` when needed. It does not export CaveRevision history, proposal/review history, or `StorageCleanupTarget` operational debt, and no restore workflow is defined. It should therefore not be described to users as a complete disaster-recovery backup unless that contract is expanded.

## External cleanup failure model

A safe destructive workflow should make these states understandable:

| Relational result | External delete | Interpretation |
| --- | --- | --- |
| rolled back | not attempted | no destructive result |
| committed | succeeds | fully cleaned |
| committed | fails | authoritative deletion succeeded; external cleanup debt remains |
| not committed | succeeds | **unsafe ordering; avoid** |

The architecture is designed around the first three states.

## Read the code in this order

1. CaveService.DeleteCave
2. CaveRepository.LockForHardDeleteAsync
3. CaveRepository.DeleteAggregateForHardDeleteAsync
4. CaveMutationRepository.PublishPreparedDeleteAsync
5. AccountService.ResetAccount
6. AccountRepository.ExecuteResetAsync
7. AccountRepository.DeleteRevisionWorkflowForAccountAsync
8. ArchiveJobCoordinator
9. ImportService.Caves + CaveImportExecutionRepository sync deletion
