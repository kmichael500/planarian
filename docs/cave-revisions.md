# Cave revision and proposal architecture

## Published state

`CaveRevision` represents actual published Cave state as a complete, versioned snapshot. The current-revision pointer,
revision chain, normalized relational state, and publication provenance advance through a controlled mutation boundary.
Semantic no-ops do not manufacture revisions. Historical snapshots preserve the labels that were true when published;
hard deletion may retain a final tombstone without requiring a live Cave row.

Authorized manager/admin edits, imports, archive changes, and published-file changes use the normal published mutation
path. Direct authorized edits are not forced through the proposal queue. Published mutations preserve tenant checks,
concurrency/locking requirements, and relational/revision atomicity.

## Proposed state

A pending proposal or change request is not published state and must not create a `CaveRevision`. Proposal versions are
immutable and append-only; `CurrentProposalVersionId` identifies the current version. An update proposal is based on a
specific published revision. If that base is stale or incompatible, approval enters an explicit conflict and re-review
path rather than silently applying the proposal to newer state.

## Review outcomes

Approval passes through the normal published mutation boundary rather than independently writing Cave tables. The
relational transaction atomically persists validated normalized state, the revision describing the actual resulting
state, request/provenance linkage, request status, and reviewer metadata. The accepted revision is built from the
validated result, never copied blindly from proposal JSON.

Rejection preserves the request, proposal versions, decision, and reviewer audit data, but does not alter published
Cave state or create a Cave revision.

## Proposal files and blob consistency

Proposal files remain staged and account-owned while pending. They become published Cave files only through successful
approval. File association and revision publication are relationally atomic. Because blob storage cannot participate in
the PostgreSQL transaction, newly written blobs use failure compensation and destructive cleanup is deferred until the
relational commit succeeds.
