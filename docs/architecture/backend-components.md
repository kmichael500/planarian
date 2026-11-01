# Backend components

Planarian's persistence boundary is defined in [Data-access architecture](../data-access-architecture.md). Controllers translate HTTP requests, services coordinate use cases, pure planners/policies perform deterministic transformations, repositories own persistence mechanics, and infrastructure adapters own external providers.

## Cave publication and history

| Component | Responsibility |
| --- | --- |
| `CaveService` | Interactive Cave aggregate authoring, authorization, and desired-state orchestration |
| `CaveMutationCoordinator` | Application boundary for single-Cave publication primitives |
| `CaveMutationRepository` | Single-Cave conflict checks, transaction ownership, revision publication, and revision-pointer advancement |
| `CavePublishedSnapshotRepository` | Canonical semantic Cave snapshots |
| `CaveRevisionQueryRepository` | Revision-history and comparison queries |
| `CaveBulkRevisionRepository` | Set-oriented revision publication for bulk/admin workflows |
| `Planarian.Operations` | Deployment-only operations that must not run through the web host |
| `CaveRevisionRolloutRepository` | One-time legacy Cave revision-history initialization |

`CaveMutationRepository` and `CaveBulkRevisionRepository` share snapshot and semantic-diff contracts but serve different execution models: one-Cave optimistic publication versus bounded set-oriented publication.

## Proposal and review

| Component | Responsibility |
| --- | --- |
| `CaveChangeRequestService` | Proposal authoring, revision, review, approval, rejection, and request-changes workflows |
| `CaveChangeRequestRepository` | Proposal-version, review-event, and workflow persistence/locking |
| `CaveProposalSnapshotV1` | Versioned immutable proposed state |
| `CaveChangeRequestStatePolicy` | Proposal workflow state rules |
| `CaveRevisionDiffService` | Semantic comparison of published/proposed snapshots |

Approval ultimately uses the same single-Cave publication semantics as direct authoring.

## Files and object storage

| Component | Responsibility |
| --- | --- |
| `FileService` | Coordinate File upload, staging, authenticated reads, object availability checks, one-Cave File-import publication, and post-commit cleanup |
| `FileRepository` | Current Cave File access/metadata projection and duplicate checks |
| `AuthoringFileRepository` | Account-owned staged File eligibility, proposal claims, publication attachment, expiration and relational removal |
| `StorageCleanupRepository` | Idempotent durable cleanup-target queueing, live-reference queries, acknowledgement, and retry ordering |
| `StorageCleanupService` | Bounded post-commit object deletion, retries, and uncertain-create reconciliation |
| `IObjectStorage` | Provider-neutral create-only object, read, and delete boundary |
| `AzureBlobObjectStorage` | Azure Blob implementation of `IObjectStorage` |
| `AzureBlobTransportService` | Azure-specific chunk-upload, archive, and container transport APIs |
| `StorageCleanupTarget` | Durable post-commit external cleanup responsibility |

The Cave/File and import-source lifecycles use `IObjectStorage`. Chunk uploads, archives, and certain older photo/trip workflows still need Azure transport; these do not expand the provider-neutral contract.

## Imports

| Component | Responsibility |
| --- | --- |
| `CaveImportCsvParser` / `EntranceImportCsvParser` | Parse CSV into typed records |
| `ImportSourceService` / `ImportSourceRepository` | Create/read account-owned CSV provenance, promote an uploaded source atomically with apply, and retire expired temporary sources with durable cleanup |
| Import planning repositories | Load account-scoped state required for planning |
| `CaveImportPlanner` / `EntranceImportPlanner` | Produce immutable plans without database writes |
| Import execution repositories | Lock, revalidate, and apply plans with set-oriented writes |
| `CaveBulkRevisionRepository` | Publish revisions for the accepted bulk result |
| `ImportService` | Coordinate parse, plan, preview, execution, and post-commit cleanup |

Dry run ends after planning; it does not execute writes and roll them back.

## Reference administration

| Component | Responsibility |
| --- | --- |
| `TagTypeAdministrationRepository` | TagType rename/merge/delete and affected-Cave revision publication |
| `TagReferenceLockRepository` | Ordered TagType reference locks |
| `CountyAdministrationRepository` | County create/update/merge/delete persistence |
| `CountyReferenceLockRepository` | County reference/mutation locking |
| `EligiblePeopleTagLookup` | Shared People-tag identity resolution |

## Account and destructive workflows

| Component | Responsibility |
| --- | --- |
| `AccountService` | Account administration, Reset Account orchestration, and archive orchestration |
| `AccountRepository` | Account purge graph and archive projections |
| `ArchiveJobCoordinator` | Archive publication, serialization, cancellation, and cleanup |
| `ExportService` | Current-state account archive generation |

## Cross-cutting persistence and authorization

- `SaveChangesInterceptor` and `SaveChangesOwnershipBatchLoader` enforce tenant/ownership invariants across EF writes.
- `RequestUser` and `PermissionPolicy` resolve effective capability and resource scope.
- `FileResponsePolicy` centralizes authenticated File response/range/security behavior.
- `RequestThrottleService` and `ThrottleEventLogRepository` own request/file-stream throttling and event persistence.
