# Important data model relationships

This is a conceptual map of the revision/proposal/File model introduced or substantially changed by the Cave-revisions completion work.

It is not a complete ERD of Planarian.

The diagram is conceptual, but relationship labels explicitly mark the important **logical-only** links. In particular, `CaveRevision.CaveId`, `CaveChangeRequest.CaveId`, and `RetainedCaveFileObject` ownership are not live-domain foreign keys. That is deliberate so history/retention can survive removal of current Cave/File relationships.

## Core relationships

~~~mermaid
erDiagram
    ACCOUNT ||--o{ CAVE : owns
    CAVE ||--o{ ENTRANCE : contains
    CAVE ||--o{ FILE : publishes
    CAVE ||--o{ CAVE_GEOJSON : contains

    CAVE ||--o{ CAVE_REVISION : "logical CaveId history (no FK)"
    CAVE_REVISION o|--o{ CAVE_REVISION : previous_revision
    CAVE ||--o| CAVE_REVISION : current_revision

    CAVE ||--o{ CAVE_CHANGE_REQUEST : "logical CaveId workflow (no FK)"
    CAVE_CHANGE_REQUEST ||--o{ CAVE_PROPOSAL_VERSION : versions
    CAVE_CHANGE_REQUEST ||--o| CAVE_PROPOSAL_VERSION : current_version
    CAVE_PROPOSAL_VERSION }o--|| CAVE_REVISION : based_on

    CAVE_CHANGE_REQUEST ||--o{ CAVE_CHANGE_REQUEST_REVIEW_EVENT : audit
    CAVE_CHANGE_REQUEST ||--o{ CAVE_CHANGE_REQUEST_STAGED_FILE : claims
    FILE ||--o{ CAVE_CHANGE_REQUEST_STAGED_FILE : staged_reference

    ACCOUNT ||--o{ RETAINED_CAVE_FILE_OBJECT : "logical AccountId ownership (no FK)"
    CAVE_REVISION }o--o| CAVE_IMPORT_BATCH : import_source
~~~

All other relationship lines in the diagram correspond to configured EF relationships, many using tenant-qualified composite keys rather than a single ID. The logical-only labels above are intentionally different.

## Cave

Cave is the **current relational state**.

Important fields for the revision architecture:

- AccountId;
- CurrentRevisionId;
- xmin-backed Version.

CurrentRevisionId points at the accepted current CaveRevision for the same account and Cave.

Cave also owns current relationships to Entrances, Files, tag joins, and CaveGeoJson line plots.

## CaveRevision

CaveRevision is published history that application persistence treats as append-only after creation.

Important fields:

- AccountId;
- CaveId;
- PreviousRevisionId;
- Source;
- Operation;
- SnapshotSchemaVersion;
- SnapshotJson;
- optional ChangeRequestId;
- optional ImportBatchId.

Sources include:

- SystemBaseline;
- ManagerEdit;
- UserSubmission;
- Import;
- System.

Operations include:

- Baseline;
- Create;
- Update;
- Archive;
- Unarchive;
- Delete.

PreviousRevisionId forms the history chain.

There is deliberately no CaveRevision → live Cave foreign key. Cave.CurrentRevisionId points to its current revision while the Cave is live, but revision rows can remain after the Cave row is gone.

## CavePublishedSnapshotV1

SnapshotJson stores CavePublishedSnapshotV1.

The V1 persisted schema contains:

- Cave identity and scalar fields;
- historical State/County labels;
- tags and historical labels;
- Entrances;
- File identity/user-facing metadata;
- line-plot identity/name/content hash.

V1 deliberately excludes storage locators and full line-plot GeoJSON.

Once this pre-release feature lands, changing persisted semantics requires a new schema version rather than silently redefining V1.

## CaveChangeRequest

The request is the mutable workflow envelope.

Important fields:

- AccountId;
- CaveId;
- original BaseRevisionId;
- CurrentProposalVersionId;
- ApprovedRevisionId;
- Status;
- reviewer metadata;
- xmin-backed Version.

Its mutable pointer/status tell the workflow which immutable proposal version is active. CaveId is a tenant-scoped logical Cave identity here rather than a direct FK to the live Cave row.

## CaveProposalVersion

CaveProposalVersion is proposed state that application persistence treats as append-only after creation.

Important fields:

- AccountId;
- ChangeRequestId;
- CaveId;
- BaseRevisionId;
- PreviousProposalVersionId;
- SchemaVersion;
- ProposalJson.

Each version independently identifies the published Cave revision it was authored against.

PreviousProposalVersionId gives the proposal-version history.

## CaveProposalSnapshotV1

ProposalJson stores CaveProposalSnapshotV1.

Unlike published history, proposals need exact future intent, so the proposal schema includes:

- County-number intent;
- exact File dispositions;
- staged File metadata;
- exact proposed line-plot GeoJSON;
- new People tag creation intents.

This allows an older proposal version to remain meaningful even when current published Cave data changes.

## Review events

CaveChangeRequestReviewEvent is append-only workflow audit data.

It identifies:

- request;
- proposal version;
- action;
- actor user;
- message;
- audit timestamps.

Review actions include:

- ChangesRequested;
- Resubmitted;
- Approved;
- Rejected.

## File

File represents a **current or staged live File**, not immutable history.

Important fields:

- AccountId;
- optional CaveId;
- FileTypeTagId;
- BlobContainer / BlobKey;
- Name;
- Extension;
- ExpiresOn.

Typical lifecycle:

- staged: CaveId null, ExpiresOn set;
- published: CaveId set, ExpiresOn null;
- removed from current state: live File row deleted after RetainedCaveFileObject is recorded.

## CaveChangeRequestStagedFile

This row claims a staged File for a specific request.

It is tenant-qualified and prevents request workflow from treating a random client-supplied File ID as proof of ownership.

Proposal versions still carry the exact File semantic intent; this table is workflow ownership/lifecycle support.

## RetainedCaveFileObject

This internal table is intentionally disconnected from live-domain foreign keys.

Fields:

- AccountId;
- CaveId;
- FileId;
- StoragePartition;
- StorageKey.

Constraints/indexes:

- unique AccountId + FileId;
- indexed AccountId + CaveId.

Its purpose is to locate historical bytes after the current File row is gone.

## CaveGeoJson

CaveGeoJson is current line-plot payload storage.

Fields:

- Id — stable logical line-plot identity;
- CaveId;
- Name;
- GeoJson jsonb.

Published revisions store only a deterministic hash of GeoJson content.

Proposal versions store exact desired GeoJson.

## CaveImportBatch

An import batch records import provenance and counts.

Import-created CaveRevisions may identify the batch through ImportBatchId.

This ties accepted history to one bulk import without turning every Cave mutation into an interactive service call.

## Current state versus history versus proposal

A useful distinction:

| Concern | Authoritative representation |
| --- | --- |
| Current Cave | Cave + relational children |
| Current line-plot body | CaveGeoJson |
| Current File metadata | File |
| Published history | CaveRevision + CavePublishedSnapshotV1 |
| Proposed future state | CaveProposalVersion + CaveProposalSnapshotV1 |
| Proposal workflow status | CaveChangeRequest |
| Review audit | CaveChangeRequestReviewEvent |
| Historical removed File bytes | RetainedCaveFileObject + object storage |
| Import provenance | CaveImportBatch |
