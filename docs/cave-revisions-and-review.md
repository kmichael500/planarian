# Cave revisions and review architecture

This document separates the revision/history functionality delivered on
`feature/cave-revisions` from the persistence foundation and the future
change-request workflow. Persistence entities are not evidence that an
end-to-end review feature exists.

## Implemented now

### Published Cave revision history

- `CaveRevision` records actual published Cave state as a versioned
  `CavePublishedSnapshotV1` JSON document.
- `Cave.CurrentRevisionId` points to the snapshot matching current relational
  state. `PreviousRevisionId` forms the revision chain.
- Each revision records its source and operation. `CaveRevisionDiffService`
  derives semantic differences from complete snapshots.
- Authorized manager create/edit, archive/unarchive, hard delete, and published
  file mutations publish through `CaveMutationCoordinator`.
- Cave and Entrance import execution repositories publish through the import revision repository, with
  provenance stored in `CaveImportBatch`.
- Both publication paths suppress semantic no-ops and commit relational state,
  the revision, and the current-revision pointer atomically.
- Hard delete retains a final tombstone revision after the live Cave row is
  removed. `CaveRevision` therefore keeps a logical Cave ID without requiring a
  live Cave foreign key.
- Snapshot references retain historical labels such as State, County, TagType,
  Location Quality, and File Type names. Reference renames are reported as
  metadata changes rather than retroactively rewriting history.
- Tenant-filtered reads and account-qualified mutation checks remain the
  security boundary.
- Every persisted `File`, including a staged temporary file, is account-owned.
  Tenant-qualified File and staged-file foreign keys enforce ownership.

`CavePublishedSnapshotV1` contains revisionable metadata, not file bytes, SAS
URLs, GeoJSON, search vectors, EF metadata, or concurrency tokens. V1 does not
fan out Cave revisions merely because shared reference data is renamed.

### Change-request persistence/model foundation

The branch also implements database/model infrastructure for later workflow:

- `CaveChangeRequest`;
- immutable/versioned `CaveProposalVersion` rows and proposal JSON V1;
- `CaveChangeRequestStagedFile` with account-qualified ownership;
- base, current-proposal-version, and approved-revision linkage fields;
- Pending, Approved, and Rejected persistence states;
- reviewer identity, review timestamp, and review-note metadata; and
- `CaveRevision.Source.UserSubmission` plus change-request provenance linkage.

This is persistence/model foundation only. It is not a complete user workflow.

### Data access and import publication

Planarian uses EF Core 8 with Npgsql/PostGIS. Database-free CSV parsers and pure planners consume immutable,
repository-loaded planning state outside the write transaction. Execution repositories lock affected Caves in stable ID
order, verify planned revision state, apply bounded EF batches, and publish
revisions once inside the transaction. Entrance rows are typed in-memory data;
the former temporary staging table, linq2db, and EFCore.BulkExtensions paths are
gone. Destructive sync operations first establish owned IDs, then retain an
explicit account predicate even when bypassing query filters.

Blob storage cannot join the PostgreSQL transaction. Published-file operations
therefore use compensation: a newly written destination blob is deleted
best-effort if relational/revision publication fails, without masking the
original error. Temporary, hard-delete, and import-sync blob deletion occurs
only after relational commit.

PostgreSQL/PostGIS integration tests run through Testcontainers. Local macOS
Colima socket detection is test-fixture behavior only and does not affect CI or automatically disable Ryuk.

## Not implemented yet

This branch does not provide a complete end-user change-request workflow. The
following remain future work:

- an application service for creating and submitting requests;
- an append-only proposal-version/amendment service;
- request retrieval and review APIs;
- a manager/admin review queue;
- approve and reject operations;
- reviewer amendment operations;
- approval-time stale-base conflict handling and explicit re-review;
- materialization of an approved proposal into normalized Cave state;
- publication of the resulting `UserSubmission` CaveRevision;
- staged-file publication and cleanup for completed requests;
- notifications;
- frontend suggestion/new-Cave/edit UI;
- frontend review/diff UI; and
- an end-user Cave revision/history viewer.

These items must not be described as functioning features merely because their
database columns or entities exist.

## Future change-request architecture contract

### Published state versus proposed state

`CaveRevision` represents actual published Cave state only. A pending suggestion
must never create a fake revision. Pending state is a `CaveChangeRequest` plus
immutable, versioned `CaveProposalVersion` snapshots. Diffs are derived from
complete semantic snapshots/proposals.

Do not restore the obsolete `feature/review-changes` backend design of mutable
request state plus typed field-by-field `CaveChangeHistory` rows replayed to
construct accepted state. Useful product and UX ideas may be retained, but the
backend contract is published immutable snapshots, pending immutable proposal
versions, derived semantic diffs, and one controlled publication boundary.

### Base revision and amendments

An update proposal records the exact published revision on which it was based.
Approval must verify that the Cave has not advanced incompatibly. A stale
proposal must enter an explicit conflict/re-review path; it must not be silently
applied to newer state.

User or reviewer amendments append a new proposal version. Published proposal
versions are never edited in place, and `CurrentProposalVersionId` identifies
the latest version.

### Approval and rejection

Approval must use the normal published mutation boundary, not write Cave tables
independently. In one atomic workflow it must:

1. verify permissions and request status;
2. verify the base revision/concurrency state;
3. resolve proposal intents;
4. materialize the intended normalized Cave aggregate;
5. publish a snapshot of the actual resulting database state;
6. create a `CaveRevision` with `Source = UserSubmission`;
7. attach `ChangeRequestId`;
8. set `ApprovedRevisionId`;
9. persist request status and reviewer metadata; and
10. commit all relational state atomically.

The accepted snapshot must describe actual validated relational state, never a
blind copy of proposal JSON. Rejection records status/reviewer metadata and
preserves the audit record, but changes no published Cave and creates no
`CaveRevision`.

### Permissions and direct edits

Authorized manager/admin direct edits continue to bypass the proposal queue and
publish normal history through `CaveMutationCoordinator`. Users with appropriate
account- and Cave-scoped view access may submit suggestions without acquiring
published-write authority. Managers/admins review those suggestions.

### New Caves and taxonomy

A new-Cave proposal must not allocate final live identifiers such as
CountyNumber while pending. `CountyNumberIntent` remains the design basis unless
implementation review identifies a concrete problem.

Pending proposals may contain new-tag intent but must not create live TagTypes.
Approval first attempts eligible case-insensitive reuse under the same policy as
CSV imports: identity is tag key plus trimmed case-insensitive name; canonical
existing ID/name is retained; active-account or default eligibility is required;
legacy duplicates use exact spelling, then active-account ownership, then stable
ID. A new active-account tag is created only when no eligible equivalent exists.

### Staged files

Proposal files remain staged and account-owned while pending. They become normal
published Cave files only if approval succeeds. File publication and Cave
revision publication must remain relationally atomic, using the existing blob
compensation rules.
