# Destructive operations

Destructive workflows deserve separate documentation because the safety question is not merely "did the database transaction succeed?" They often cross PostgreSQL and object storage.

## General rule

When deletion affects both relational state and external objects:

> **Commit the authoritative relational result first, then delete external bytes.**

This avoids the worst failure mode: deleting bytes first and then rolling back the database, leaving relational history pointing to content that no longer exists.

The tradeoff is that post-commit external cleanup can fail and leave cleanup debt. Such failures must be safe, observable enough to understand, and retryable where appropriate.

## Ordinary published File removal is not destruction

Removing a File from current Cave state:

- records RetainedCaveFileObject;
- removes the live File row;
- publishes the new Cave revision;
- keeps the bytes.

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
    Cave->>Repo: Remove aggregate and collect object addresses
    Cave->>Mutation: Publish Delete revision
    Mutation->>DB: Persist tombstone history
    DB-->>Cave: Commit
    loop each current/retained object
        Cave->>Storage: Best-effort delete
    end
~~~

Open CaveChangeRequests block hard deletion.

The “Cave mutation boundary” above is `CaveMutationCoordinator` delegating transaction/history work to `CaveMutationRepository`. The delete revision preserves the final semantic Cave snapshot even after the live Cave row is gone.

## Sync-import delete

Cave import with sync enabled can also delete Caves.

The execution repository performs equivalent safety checks in a set-oriented way:

- verify current account;
- reject open proposal conflicts;
- collect current and retained object addresses;
- remove relational data;
- publish bulk delete revisions;
- commit;
- let ImportService perform external object deletion after commit.

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
    Repo->>DB: Capture live + retained object addresses
    Repo->>DB: Delete revision/workflow/Cave/reference/account state graph
    Repo->>DB: Commit
    Repo-->>Service: Object addresses
    loop captured objects
        Service->>Storage: Best-effort delete object
    end
    Service->>Storage: Delete legacy account container
~~~

The relational reset is committed **before** external cleanup.

That ordering is intentional.

## Important Reset Account caveat

The final whole-container delete is not currently best-effort in the same way as individual object deletion.

If container deletion fails after the PostgreSQL reset committed, the API can report failure even though the relational reset already happened.

Because this is a legitimate partial-success state, its current test coverage and user-visible behavior decision are tracked in the [completion status ledger](../../CAVE_REVISIONS_COMPLETION_STATUS.md) rather than duplicated here.

## Account container also contains archives

ArchiveJobCoordinator writes recent archives under the same account container using an archives/ prefix.

ResetAccount removes the whole account container after relational reset.

Therefore stored server-side account archives are currently removed by Reset Account as part of that container deletion.

This behavior needs an explicit product contract:

- either archives are intentionally reset data and the UX should say so;
- or archives are recovery artifacts and need a separate retention boundary.

The current validation/product-decision status for this behavior is kept in the [completion status ledger](../../CAVE_REVISIONS_COMPLETION_STATUS.md).

## Archive jobs

ArchiveJobCoordinator is a background in-process coordinator for account archive generation.

It:

- creates temporary archive objects;
- writes current-state archive content;
- promotes temp to final archive;
- keeps a bounded recent set;
- deletes old archives;
- handles cancellation/cleanup.

This path still uses Azure-specific Blob APIs and is **not** part of the provider-neutral IObjectStorage Cave/File boundary.

The current implementation is a **current-state export**: `ExportService` writes `caves.csv`, `entrances.csv`, current live Cave Files, current CaveGeoJson line plots, and `missing-files.csv` when needed. It does not export CaveRevision history, proposal/review history, or RetainedCaveFileObject records, and no restore workflow is defined. It should therefore not be described to users as a complete disaster-recovery backup unless that contract is expanded.

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
