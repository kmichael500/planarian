# Cave revision and proposal invariants

The invariants below are the durable contract for Cave revisions and proposals. Detailed mechanics live in the linked architecture documents. Release evidence and deployment prerequisites are recorded in [`../CAVE_REVISIONS_COMPLETION_STATUS.md`](../CAVE_REVISIONS_COMPLETION_STATUS.md).

## Publication

- Current Cave state is mutable relational data. `CaveRevision` records each accepted published state as a complete, immutable semantic snapshot with provenance.
- Interactive Cave fields, tags, Entrances, Files, and line plots publish as one aggregate desired state. A semantic no-op creates no revision.
- Direct authorized edits use the published mutation path; they are not forced through proposals.
- Hard deletion may publish a final tombstone without retaining a live Cave row and is blocked while unresolved proposals exist.
- Cave/Entrance imports and reference-administration workflows use set-oriented execution, but share the same canonical snapshot, semantic-diff, revision-chain, expected-current-revision, tenant-isolation, and relational/revision atomicity rules as interactive publication.
- TagType or County label/remap operations publish an `AdministrativeChange` revision for each affected Cave whose serialized published state changes rather than presenting those propagated revisions as ordinary `ManagerEdit` activity. Label-only changes preserve stable reference IDs; merges/remaps change them.

See [Cave publication](architecture/cave-publication.md), [Imports](architecture/imports.md), and [Data model](architecture/data-model.md).

## Lock contract

When a transaction needs multiple resource classes, locks are acquired in this order:

1. County rows;
2. TagType rows;
3. Cave rows;
4. Cave-owned child, relationship, and File rows.

Multiple stable IDs within a class are acquired in deterministic ordinal order. Application ordering uses `.NET StringComparer.Ordinal`; PostgreSQL multi-row lock queries use `COLLATE "C"` for `ORDER BY`. Planarian-generated IDs are ASCII.

Interactive Cave/File reference mutations key-share-lock the union of current and target existing stable IDs through relational mutation and revision commit. Newly created People identities have no existing row to lock. Direct Cave mutations that change County membership or allocate County numbers acquire the County mutation lock before TagType reference locks.

County State moves, deletes, and merges use the conflicting County mutation lock. Cave import key-share-locks planned existing Counties so account, State, display code, and name remain stable, then key-share-locks planned existing TagTypes and locks affected Caves `FOR UPDATE`. Entrance import starts at TagType locks because it does not change Cave County membership.

TagType rename/merge/delete uses conflicting TagType update locks; ordinary published reference writers use key-share locks. TagType rename and merge lock affected Caves and publish their resulting semantic changes in the same transaction. File-type merge also remaps account-owned staged File rows, which have no published Cave state to revise. Existing immutable proposal versions retain their recorded File-type intent.

County rename locks the County and affected Caves while preserving each Cave's stable County ID and County Number. County merge is limited to source/destination Counties in the current account and selected State, preserves existing Cave County Numbers, rejects destination duplicates atomically, and leaves the source County as an unused administrative value.

Hard Cave deletion locks the Cave `FOR UPDATE` and checks unresolved proposals. It does not first lock every reference being removed; the Cave lock and expected-revision checks serialize aggregate replacement/deletion.

## Proposals and review

- A proposal is not published state and creates no `CaveRevision` until approval.
- `CaveProposalVersion` is immutable and append-only. Existing-Cave versions target a specific published revision; creation versions have no base revision. `CurrentProposalVersionId` selects the active version without rewriting older versions.
- Proposal staleness is based on published semantic compatibility, not revision-ID equality alone. A later revision whose only differences are descriptive metadata for the same stable references (for example a TagType or County rename) is proposal-compatible and does not require re-review. Stable-ID changes such as merges/remaps, or any Cave/Entrance/File/line-plot content or association change, remain stale.
- A stale proposal is explicitly revised against current published state; it is never silently rebased or merged. Metadata-compatible revision advances do not rewrite an immutable proposal version or its captured historical labels.
- Every persisted proposal version must differ semantically from its base. File-only changes are valid; semantic no-ops are rejected. Refreshing only renamed reference labels does not manufacture a new proposal version. Nullable Cave measurements preserve `null` separately from zero.
- Approval uses the normal single-Cave publication boundary. Validated relational state, the accepted revision, request/provenance linkage, request status, and reviewer metadata commit atomically. For a metadata-compatible base advance, approval serializes against the actual current Cave revision and publishes after it while retaining the immutable proposal's original base and labels. The accepted snapshot is built from the validated current result rather than copied blindly from proposal JSON.
- Cave `xmin` remains a concurrency guard during approval. A racing substantive publication causes the approval transaction to roll back and report a revision conflict; a racing identity-preserving reference rename is serialized and may be crossed only after the same metadata-compatibility check. Approval is never retried as last-write-wins.
- Rejection and request-changes preserve proposal/review history without changing published Cave state.
- Existing-Cave PUT requires the Cave ID and client-loaded revision: missing/blank revision is HTTP 400 and stale revision is HTTP 409. New-Cave POST has no expected revision.
- Multiple pending requests for one Cave are supported. Approval can make another request stale; that request must be revised explicitly.

See [Proposals](architecture/proposals.md) and [Data model](architecture/data-model.md).

## Files and line plots

- A newly uploaded Cave File receives one immutable object-storage address. Staging creates an account-owned `File` row with `CaveId = null`; publication associates that same File/object with the Cave without copying or renaming bytes.
- Proposal versions reference the exact staged File IDs that define desired state. Initial request creation claims selected staged Files transactionally. Generic staged File access is account/uploader-scoped; request-bound access additionally requires current request visibility.
- Published File removal deletes the live `File` row and records a `CaveAssetRemoval` `StorageCleanupTarget` in the same relational transaction that publishes the Cave without the File. External deletion begins only after commit.
- Never-published staged File removal uses the same ordering with `UnpublishedFileRemoval`. Failed external deletion leaves durable retryable cleanup debt. Ordinary authoring retries bounded cleanup batches; a completely inactive account may retain expired staging/debt because there is no periodic ordinary-asset sweeper.
- Revision history retains File identity and historical user-facing metadata, not object-storage locators or removed bytes.
- Current line-plot payloads remain `CaveGeoJson` `jsonb`. Stable line-plot IDs survive rename/content edits; published revisions retain ID, historical name, and deterministic SHA-256 content fingerprint, while immutable proposal versions retain the exact normalized desired GeoJSON.
- GeoJSON fingerprint canonicalization sorts object properties while preserving array/feature ordering. Formatting/property-order changes alone are semantic no-ops; geometric equivalence is not inferred.

See [Files and storage](architecture/files-and-storage.md) and [Destructive operations](architecture/destructive-operations.md).

## Authorization and persisted compatibility

- Persisted V1 snapshot/proposal enum values use string member names; numeric enum tokens are invalid. Persisted V1 semantics are immutable; incompatible persisted semantics require a V2 model and schema-version dispatch.
- Stable IDs identify references while snapshot-time labels preserve historical presentation. Later reference renames do not rewrite old snapshots or proposals and, by themselves, do not make a proposal stale. Publication uses the current label for the unchanged stable identity.
- Only People tags may be proposed as new free-form database tags. Cartographer, Cave Reported By, and Entrance Reported By use People identities; other database-backed tag fields must reference an eligible existing tag. Alternate Names remain free-form strings.
- People-name identity uses invariant-culture, case-insensitive comparison across import, proposal, approval, and direct-save workflows. Existing People references remain stable-ID references and never degrade into creation intents when the current TagType changes or disappears.
- Current Cave `View` access is sufficient to submit a proposal against an existing Cave. Existing-Cave request, proposal, staged-file, and revision access still requires current Cave visibility; request ownership does not override later visibility loss. Review requires current Manager authority for the Cave.
- A user with effective Cave `View` capability in the account may propose a new Cave in any State/County configured for that account. The request reserves the eventual Cave ID. Approval creates the Cave and first revision atomically and grants the submitter Cave-scoped `View` only if that user is still an account member.
- Server-allocated IDs for proposed new Entrances remain their identities after publication.
- Mine/review request lists are permission-filtered in database queries and returned as bounded pages.
- “Reported By” Cave/Entrance data uses People tags. Revision actors, submitters, proposal authors, and reviewers carry Planarian-user attribution separately.
- Legacy Caves receive a system `Baseline` revision during the controlled rollout. It marks the start of authoritative revision history, not Cave creation or proof that no earlier changes occurred.
- Revision, proposal, request, and staged-file relationships remain tenant-qualified to their account and owning resources.

See [Authorization](authorization.md), [Data-access architecture](data-access-architecture.md), and [Backend components](architecture/backend-components.md).
