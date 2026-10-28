# Cave revision and proposal architecture

> [!IMPORTANT]
> `docs/plans/cave-revisions-completion/README.md` records the target-state implementation plan and validation gates for
> this pre-release architecture. This document is the durable architecture contract. Temporary pre-merge implementation
> gaps do not weaken its invariants; the SHA-pinned live status is tracked in [`../CAVE_REVISIONS_COMPLETION_STATUS.md`](../CAVE_REVISIONS_COMPLETION_STATUS.md).

## Published state

`CaveRevision` represents actual published Cave state as a complete, versioned snapshot. The current-revision pointer,
revision chain, normalized relational state, and publication provenance advance through a controlled mutation boundary.
Semantic no-ops do not manufacture revisions. Historical snapshots preserve the labels that were true when published;
hard deletion may retain a final tombstone without requiring a live Cave row.

Authorized manager/admin edits, imports, archive changes, and published-file changes use the normal published mutation
path. Direct authorized edits are not forced through the proposal queue. Published mutations preserve tenant checks,
concurrency/locking requirements, and relational/revision atomicity.

Interactive Cave and File reference mutations hold PostgreSQL key-share locks on the union of current existing stable
IDs and target existing stable IDs until the relational mutation and Cave revision publication commit. These paths can
replace or detach individual relationships as part of an aggregate mutation. Newly created People identities have no
existing row to lock. TagType and County label administration takes the corresponding conflicting update lock before
locking affected Caves, so a rename cannot race a publication that is capturing that reference's current metadata.

Bulk Cave import first key-share-locks existing planned target Counties and validates their account, State, display
code, and name while those locks are held. It then key-share-locks existing planned target TagTypes and locks affected
Cave rows `FOR UPDATE`. Entrance import starts at the TagType step because it operates against stable Cave IDs and does
not mutate Cave County membership. Bulk and interactive publication use the same canonical published snapshot and
semantic-diff rules; bulk differs in how it plans, locks, and applies many relational writes, not in what counts as a
published Cave change. Both imports validate expected Cave and revision state before replacing aggregate
child/reference state. They do not pessimistically lock every old reference absent from the target plan: the owning
Cave lock and expected-revision validation serialize that wholesale replacement. Hard Cave deletion similarly locks
the Cave `FOR UPDATE`, checks for pending proposals, and deletes the aggregate without first locking every referenced
TagType.

Whenever a transaction actually needs these resource classes, it uses this durable lock hierarchy: County rows, then
TagType rows, then Cave rows, then Cave-owned child, relationship, and File rows. Multiple stable IDs within each
resource class are acquired in deterministic ordinal order. The application orders and chunks IDs with .NET
`StringComparer.Ordinal`; PostgreSQL multi-row lock queries explicitly use the `C` collation for their `ORDER BY`, so
database locale/collation cannot change the row-lock acquisition sequence. Planarian-generated IDs are ASCII. Direct
Cave mutations that need County-number or County-transition
serialization acquire the County mutation lock before TagType reference locks. Account County State moves, deletes,
and merges use that same conflicting County mutation lock, while Cave import uses a County key-share lock to keep existing target
metadata stable through relational writes and revision commit. Ordinary Entrance import, File mutation, and Tag Merge
do not acquire County locks because they do not mutate Cave County membership.

Destructive, remapping, or label-changing TagType operations acquire update locks on their TagType rows in that same
ordering; ordinary published reference writers use key-share locks. TagType renames discover and lock every Cave whose
published snapshot contains that stable reference, update the shared label, and publish one `ManagerEdit` revision per
affected Cave in the same transaction. TagType merges use the same affected-Cave boundary while also rewriting
relationships set-wise. Both operations advance `CurrentRevisionId` only for Caves whose serialized published state
actually changes.

County name or display-code changes follow the same rule: an in-use County rename locks the County and its affected
Caves, preserves the stable County ID and each Cave's County Number, and publishes one `ManagerEdit` revision per
affected Cave. Updating an unused County creates no Cave revision because no published Cave snapshot changes. As with
any other published revision, a rename makes an affected pending proposal based on the prior revision stale without
rewriting that proposal version's captured labels or base revision.

County merge is likewise a published bulk Cave mutation. It is restricted to source and destination Counties in the
current account and selected State, locks those County rows before affected Cave rows, preserves every Cave's existing
County Number, and rejects the entire merge if the destination would contain a duplicate County Number. Successful
merge remaps each affected Cave to the destination County and publishes one `ManagerEdit` revision per affected Cave in
the same transaction. Source County rows are retained as unused administrative values rather than being implicitly
deleted.

## Proposed state

A pending proposal or change request is not published state and must not create a `CaveRevision`. Proposal versions are
immutable and append-only; `CurrentProposalVersionId` identifies the current version. An update proposal is based on a
specific published revision recorded immutably on each proposal version. The request retains its original submission
base separately. If the active version's base is stale, explicit re-review starts from the current published Cave and
creates another immutable proposal version against that revision rather than silently applying or rebasing the older
proposal state.

Every persisted proposal version must have an authoritative semantic difference from its base snapshot. File additions,
removals, and metadata changes are semantic differences, so a file-only proposal is valid; a version with no field,
relationship, or file difference is rejected before request/version persistence. Nullable Cave measurements preserve
unknown (`null`) separately from an explicitly known zero throughout authoring, revision, and publication.

## Review outcomes

Approval passes through the normal published mutation boundary rather than independently writing Cave tables. The
relational transaction atomically persists validated normalized state, the revision describing the actual resulting
state, request/provenance linkage, request status, and reviewer metadata. The accepted revision is built from the
validated result, never copied blindly from proposal JSON.

The Cave `xmin` concurrency token remains authoritative during approval. If a published edit races after approval has
loaded the Cave, the approval transaction rolls back and reports the same explicit published-revision conflict used for
an already-stale base; it is never retried or applied as last-write-wins.

Rejection preserves the request, proposal versions, decision, and reviewer audit data, but does not alter published
Cave state or create a Cave revision.

## Proposal files and object-storage consistency

Interactive Cave Files use one provider-neutral object-storage address for their entire lifecycle. Uploading through the
aggregate editor creates an account-owned, expiring `File` row with `CaveId = null` and writes the object once. Direct
publication associates that same File ID/object address with the Cave and clears its expiration. Initial proposal
authoring can stage a File before a change-request row exists; request creation claims the selected staged File
transactionally. Proposal versions store the exact stable File IDs that define their desired state.

Approval publishes the exact staged File IDs referenced by the selected immutable proposal version. Association, Cave
state, revision publication, and request/reviewer state are relationally atomic; publication does not copy or rename the
object merely because it changed from staged to published. Generic staged lookup/download/delete is account- and
uploader-qualified, while request-bound staged downloads additionally require current request visibility.

When a published File is removed from current Cave state, its live `File` row is deleted so obsolete Cave/TagType
relationships do not survive. The same relational transaction queues the exact object address as a
`StorageCleanupTarget` with purpose `CaveAssetRemoval` and publishes the Cave revision without the File. Only after that
transaction commits does Planarian attempt external deletion. Successful cleanup removes the target; an external failure
leaves durable retryable cleanup debt without making the already-committed Cave publication false. Cleanup failures are
logged; later Cave/File authoring retries older debt in bounded batches. There is no dedicated ordinary-asset background
relay, so debt can remain while an account is inactive. Revision history keeps the File's stable ID and historical
user-facing metadata, not its storage locator or removed bytes.

Never-published Files use the same fail-safe ordering with a distinct `UnpublishedFileRemoval` cleanup purpose. Explicit
discard, expiration, proposal rejection, and unused staged Files discarded during approval remove the relational File
row and queue the exact object address before that relational result commits. External deletion happens afterward;
provider failure leaves durable retryable debt instead of an untracked object. Later Cave/File authoring retries a
bounded ordinary-cleanup batch, and Reset Account absorbs both unpublished and published File cleanup debt.

Browser downloads for proposal Files use the same API-origin-aware, account-qualified URL construction as published
File downloads. Request, version, revision, and staged-File access remains permission-filtered and tenant-qualified,
including guessed and mismatched identifiers. Azure is the current object-storage adapter, but affected Cave/File
application semantics depend on the provider-neutral storage boundary rather than Azure SDK types.

## Line plots

Current line-plot payload storage remains `CaveGeoJson` `jsonb`; this feature deliberately does not redesign that storage.
Line plots nevertheless participate in the same aggregate Cave mutation as fields, tags, Entrances, and Files. A stable
line-plot ID survives rename/content edits, omitted IDs are additions, omitted existing IDs are removals, and one aggregate
save produces at most one Cave revision. Direct standalone GeoJSON publication is not an interactive mutation path.

Files and line plots intentionally share the same published-history/removal semantics: current payload disappears after
the removal publishes, while the previous revision retains enough lightweight metadata to explain what existed and what
changed. Their physical deletion mechanics differ only because `CaveGeoJson` participates in the PostgreSQL transaction
while File bytes live in external object storage.

Published revision V1 stores only each line plot's stable ID, historical name, and deterministic SHA-256 content
fingerprint. Canonicalization sorts JSON object properties while preserving array/feature ordering, so formatting/property
order alone is a semantic no-op without pretending geometrically equivalent GeoJSON is identical. Full GeoJSON bodies
are intentionally excluded from published revision JSON. Proposal V1, by contrast, stores the exact normalized desired
GeoJSON for each line plot so an older immutable proposal version can still be previewed/reviewed/approved even after
current published line plots change. Full current GeoJSON is fetched only by dedicated authoring-context reads, not
ordinary `CaveVm` reads or the revision API.

## Historical invariants

- Persisted snapshot/proposal V1 enum values use their existing string member names. Numeric enum tokens are not a
  valid V1 wire representation.
- Only People tags may be proposed as new free-form database tags. Cartographer, Cave Reported By, and Entrance
  Reported By use People; Biology, Archeology, and every other database-backed Cave/Entrance tag field must reference
  an existing tag of the required type visible to the current account. Alternate Names are canonicalized free-form
  strings, not TagType records.
- Current View access is sufficient to submit a proposal. Current Cave visibility remains a prerequisite for all
  request, proposal-version, staged-file, and revision/history access afterward; request ownership does not survive
  permission revocation. Review requires current Manager permission for the Cave and never relies on historical base
  State/County metadata.
- A server-allocated ID for a proposed new Entrance remains that Entrance's identity after successful publication.
- Mine and review request lists are permission-filtered in their database queries and returned as bounded pages.
- Published revisions and proposal versions are append-only historical records. Persisted snapshot/proposal V1 is
  immutable once this feature lands; new persisted semantics require a V2 model, serializer/reader, and dispatch by
  schema version.
- Stable IDs identify references, while names and other descriptive labels captured at revision time preserve the
  historical presentation. Renaming a current tag or reference does not rewrite an older snapshot.
- A free-form People value that matches an eligible existing People identity when a proposal version is authored is
  captured as a stable-ID snapshot reference with its proposal-time label. A People creation intent is reserved for a
  name that does not resolve to an eligible existing identity at that authoring boundary.
- People-name identity uses .NET invariant-culture, case-insensitive comparison across import, proposal, approval, and
  direct-save workflows. Candidate loading does not delegate this identity decision to database collation behavior.
- Existing People references in a proposal remain stable-ID references through publication and never degrade into
  free-form creation intents when the referenced TagType is missing or no longer eligible.
- “Reported By” Cave and Entrance data is represented by People tags. Planarian-user attribution comes from revision
  actors, change-request submitters, proposal-version authors, and reviewers. Cave and Entrance do not store a separate
  reporter-user identity. A Cave that predates revision history receives a system-generated `Baseline` revision with no
  user actor during rollout. That entry means “revision history starts here”: it records the Cave's state when revision
  history was enabled and does not imply the Cave was created then or that no earlier changes occurred. Recovered older
  history may later be presented before this boundary, but it is separate from the authoritative revision chain.
- File history preserves attachment identity, editable filename stem (`Name`), immutable uploaded-file `Extension`, and
  captured file-type metadata without exposing object-storage locators in revision JSON. Ordinary published removal queues
  post-commit physical deletion; after successful cleanup the old bytes are not recoverable through Planarian.
  Never-published staged bytes may expire or be discarded; those relational removals queue durable
  `UnpublishedFileRemoval` debt before external deletion is attempted.
- Line-plot history preserves stable identity, historical name, and a deterministic content fingerprint, not the full
  GeoJSON payload. Immutable proposal versions retain exact proposed line-plot GeoJSON separately from published history.
- A semantic no-op creates neither a published revision nor a proposal version.
- Multiple pending requests for one Cave are supported. Approval may make another request stale; the stale request must
  be explicitly revised and reviewed against current published state rather than silently rebased.
- Hard deletion is blocked while unresolved proposals exist.
- Every Cave hard-delete entry point, including sync import deletion, checks for unresolved proposals after locking its
  deletion targets and refuses the deletion when one exists.
- Revision, proposal, request, and staged-file relationships remain tenant-qualified and scoped to the appropriate
  account, Cave, request, and proposal version.
- Interactive and bulk mutation workflows may use different execution mechanics, but they share the published
  snapshot, revision-chain, expected-current-revision, tenant-isolation, and relational/revision atomicity invariants.
