# Ordered implementation plan

This is the target implementation sequence and remains useful as rationale/acceptance guidance; it is not a live progress
ledger. Do not redo a phase merely because it appears below. Re-check current source and use
[`../../../CAVE_REVISIONS_COMPLETION_STATUS.md`](../../../CAVE_REVISIONS_COMPLETION_STATUS.md) for SHA-pinned completion
status.

Follow these phases in order when implementing remaining work. A later phase may modify files introduced earlier, but do
not skip the earlier contract and validation work. The file lists below are concrete starting points from the inspected
baseline, not permission to ignore adjacent code reached by compilation or tests.

## Phase 0 — Re-establish the live baseline

Before editing:

1. confirm the current branch is still `feature/cave-revisions-completion`;
2. run `git status --short --branch` and preserve unrelated local work;
3. inspect the diff against the actual merge target (currently `main`); use `feature/cave-revisions` only when it is
   still useful for understanding the historical completion-plan delta;
4. reread `AGENTS.md`, `Planarian/AGENTS.md`, and this plan;
5. locate any changes to the named files since baseline `565ba738` and adapt paths without changing the target design.

Do not reset, stash, discard, or rewrite unrelated user work.

## Phase 1 — Fix stale direct Cave edits

Primary files:

- `Planarian/Planarian/Modules/Caves/Models/AddCaveVm.cs`
- `Planarian.Web/src/Modules/Caves/Models/AddCaveVm.ts`
- `Planarian.Web/src/Modules/Caves/Helpers/CaveFormMapper.ts`
- `Planarian.Web/src/Modules/Caves/Pages/EditCavePage.tsx`
- `Planarian/Planarian/Modules/Caves/Services/CaveService.cs`
- `Planarian/Planarian/Modules/Caves/Controllers/CaveController.cs`
- existing Cave revision/concurrency tests under `Planarian.Tests.Integration/Caves/Revisions/`
### 1.1 Request/form contract

Add `ExpectedRevisionId`/`expectedRevisionId` to the existing Cave edit model.

For the frontend mapping:

```text
caveToForm(cave).expectedRevisionId = cave.currentRevisionId
```

Do not populate it later from a refetched Cave immediately before Save. The token represents the version actually
edited.

For snapshot/proposal form mapping, keep proposal base semantics separate. Do not blindly put a published current
revision into a proposal form and treat it as direct-edit concurrency state if the proposal APIs already carry an
explicit expected base revision.

### 1.2 Server enforcement

In the existing direct update path:

- new Cave → expected revision must be null;
- existing Cave → missing expected revision is invalid;
- pass the supplied expected revision to the mutation preparation/publication boundary;
- remove the fallback pattern `expectedRevisionId ?? entity.CurrentRevisionId` for interactive existing-Cave Save;
- preserve the existing `CaveRevisionConflictException` and controller 409 mapping;
- preserve `xmin`/transaction concurrency protection already inside revision publication.

### 1.3 Frontend conflict UX

`EditCavePage.tsx` must recognize the published-Cave conflict response and tell the user the Cave changed after the
editor was opened. Do not auto-retry, silently reload and submit, or overwrite the user's unsaved form.
### 1.4 Phase exit gate

Before Phase 2, focused tests must prove:

- R1 editor + competing R2 publication + submit with R1 → 409 and no R3;
- correct expected revision → one successful revision;
- missing expected revision on existing direct edit → rejected;
- create-new-Cave still works without an expected revision;
- `caveToForm` preserves `currentRevisionId` into the direct-edit request model.

## Phase 2 — Add the provider-neutral object-storage boundary

Primary files/directories:

- `Planarian/Planarian/Modules/Files/Services/FileService.cs`
- `Planarian/Planarian/Startup/Program.cs` / current DI composition code that registers `FileService`
- a small new storage abstraction/infrastructure location consistent with existing module organization
- `Planarian/Planarian.Model/Database/Entities/RidgeWalker/File.cs`
- `Planarian/Planarian/Modules/Files/Repositories/FileRepository.cs`
- `Planarian.Tests.Integration/Infrastructure/Services/IntegrationTestFileService.cs` or its replacement if the new
  external-boundary fake makes that subclass obsolete.

### 2.1 Introduce application-facing types

Create a cohesive interface such as `IObjectStorage`, plus provider-neutral address/read-result types. Keep the surface
minimal: put, open/read, and delete-if-exists are the expected required operations.

Do not expose Azure SDK objects or response types from that interface.

### 2.2 Azure adapter

Move/wrap the Azure-specific container/client/stream mechanics behind a concrete implementation such as
`AzureBlobObjectStorage`. Wire it through DI.
The concrete adapter may internally reuse pieces of the current `FileService` while they are extracted, but the final
Cave/File application workflow must not construct `BlobContainerClient` itself.

Do not add provider-neutral wrappers around concepts the application does not need merely to mirror the Azure API.

### 2.3 Keep persistence renaming out of the critical path

Do not require a repository-wide rename of `File.BlobKey`/`BlobContainer`. They may remain legacy persistence property
and column names. The new application-facing storage interface/address types are provider-neutral and adapt those values
at the affected boundary.

If an entity-property rename is truly localized and simplifies the touched code, map it to the existing physical columns
and avoid a migration. Otherwise leave the names alone. Provider-neutral architecture is the goal; cosmetic churn is not.

### 2.4 Phase exit gate

Before Phase 3:

- affected File/Cave services construct successfully from an `IObjectStorage` implementation;
- Azure SDK types no longer cross the affected application-service boundary;
- focused File download/upload/delete behavior remains correct through the adapter;
- tenant-qualified File repository lookups remain intact;
- no unrelated storage consumer was refactored without need.

## Phase 3 — Convert new Cave/staged Files to upload-once lifecycle

Primary files:

- `Planarian/Planarian/Modules/Files/Services/FileService.cs`
- `Planarian/Planarian/Modules/Files/Repositories/FileRepository.cs`
- `Planarian/Planarian.Model/Database/Entities/RidgeWalker/File.cs`
- a shared `StorageCleanupTarget` entity/configuration under the RidgeWalker model
- `Planarian/Planarian.Model/Database/PlanarianDbContext.cs`
- `Planarian/Planarian.Migrations/Migrations/` for the required schema migration
- `Planarian/Planarian/Modules/Caves/Revisions/StagedCaveFilePublication.cs`
- `Planarian/Planarian/Modules/Caves/Revisions/CaveChangeRequestRepository.cs`
- `Planarian/Planarian/Modules/Caves/Services/CaveChangeRequestService.cs`
- `Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs`
- affected File/proposal integration tests.

### 3.1 Allocate identity before upload

For new Cave/staged Files:

1. create/allocate the account-owned `File` identity;
2. derive an immutable logical object key from that identity, e.g. `objects/files/{fileId}`;
3. upload bytes once;
4. persist the storage address and File metadata;
5. set `CaveId = null` and `ExpiresOn` while staged.

Do not encode temporary/published lifecycle in the object key. Existing legacy keys remain supported.

### 3.2 Publication becomes relational

Replace the current staged-publication copy model. Publishing an already-staged File should:

- validate account ownership and the exact staged File ID;
- associate the same `File` with the target Cave;
- clear `ExpiresOn`;
- keep the same storage address and File ID;
- avoid provider-side copy/re-upload.

Retire `StagedCaveFilePublication` source/destination semantics if they are no longer meaningful. Do not preserve dead
copy abstractions for compatibility with branch-only code.

### 3.3 Failure semantics
If Cave publication fails after staging, leave the File staged/expiring. The failed publication must not require deleting
a newly created permanent-copy candidate because no candidate copy should exist.

If the initial upload itself fails before a valid staged row/object pair exists, compensate only the object/row created by
that failed attempt according to existing reliability conventions.

### 3.4 Remove published File payloads safely after commit

Use the shared provider-neutral `StorageCleanupTarget` persistence model with a purpose discriminator. For ordinary removal
of a once-published File, inside the same Cave relational transaction:

1. validate the current File belongs to the current account/Cave;
2. queue its exact `StoragePartition`/`StorageKey` as purpose `CaveAssetRemoval`;
3. delete the live `File` row so current-domain FKs no longer remain in use;
4. publish the new Cave revision without that File;
5. commit.

Only after commit may the application delete the object. Successful/already-absent cleanup removes the target. External
failure leaves durable debt for retry and must emit structured diagnostics. If the address is now referenced by a live File, do not delete it; clear the stale
cleanup request instead. Ordinary asset cleanup must not use Reset Account's account-wide write block. Immediate cleanup handles targets created by the current commit; later normal Cave/File authoring may retry only a bounded batch of older debt so cleanup cannot turn an interactive request into an unbounded storage sweep. A dedicated background relay is deferred unless operational evidence shows it is needed.

The target is operational cleanup state, not historical File identity. Historical user-facing metadata already lives in
Cave snapshots; storage addresses remain private and never become revision wire data.

### 3.5 Cleanup, Reset Account, and hard purge

Expired **never-published** staged Files remove their relational File row only after queuing
`UnpublishedFileRemoval` responsibility in the same commit; physical deletion follows after commit. Account Reset uses
the same `StorageCleanupTarget` table with purpose `AccountReset` and retains its stronger retry/write-block semantics.
During reset, pending `CaveAssetRemoval` and `UnpublishedFileRemoval` addresses must be captured into Reset cleanup
responsibility before those ordinary target rows are purged.

Explicit hard Cave deletion and specialized import deletion queue `CaveAssetRemoval` responsibility with their
relational/revision result and still obey commit-before-external-delete ordering. Do not turn ordinary asset removal into
a hard-history purge because published history already contains only metadata, not payload.

### 3.6 Migration

Create the normal EF migration for the shared cleanup table/purpose. Because the superseded retention design existed on
the pre-release completion branch, the forward migration must convert existing `RetainedCaveFileObject` addresses into
`CaveAssetRemoval` cleanup debt rather than dropping them. Preserve existing Reset Account cleanup debt. Migration ID
collisions must not lose cleanup addresses, and downgrade must fail closed while any ordinary File cleanup debt (`CaveAssetRemoval` or `UnpublishedFileRemoval`) is still pending.

### 3.7 Phase exit gate

Prove with focused tests:

- staging writes one object;
- successful publication performs no second put/copy;
- File ID/storage address are unchanged by publication;
- publication failure leaves the File staged and the Cave unchanged;
- expired never-published staging is physically deleted;
- ordinary published removal deletes the live File row, publishes one revision, and deletes bytes only after commit;
- a failed external delete leaves one tenant-qualified `CaveAssetRemoval` target that can be retried;
- historical revision metadata still identifies the removed File after bytes are deleted;
- ordinary asset-cleanup debt does not block unrelated account writes;
- Reset Account absorbs pending ordinary asset-cleanup addresses;
- storage cleanup targets are not exposed through ordinary File/revision APIs;
- cross-account staged File publication and storage cleanup remain tenant-safe.

## Phase 4 — Complete File authoring in the aggregate Cave editor

Primary frontend files:

- `Planarian.Web/src/Modules/Caves/Components/AddCaveComponent.tsx`
- `Planarian.Web/src/Modules/Caves/Models/AddCaveVm.ts`
- `Planarian.Web/src/Modules/Files/Models/EditFileMetadataVm.ts`
- `Planarian.Web/src/Modules/Files/Components/UploadComponent.tsx` if it can be reused cleanly
- `Planarian.Web/src/Modules/Caves/Helpers/CaveFormMapper.ts`
- `Planarian.Web/src/Modules/Caves/Pages/EditCavePage.tsx`
- `Planarian.Web/src/Modules/Caves/Components/CaveComponent.tsx`
- `Planarian.Web/src/Modules/Caves/Service/CaveService.ts`
- `Planarian/Planarian/Modules/Files/Controllers/FileController.cs`
- affected backend File staging endpoint/service/repository code.

### 4.1 Add generic authoring-stage upload

Provide an authenticated account-owned staging endpoint that does not require a Cave or change-request ID. Prefer the
Files module rather than creating another Cave-specific temporary blob API.

Security for an unbound staged File:

- `AccountId` must be the current account;
- `CreatedByUserId` identifies the staging actor using the existing `EntityBase` audit field;
- generic pre-request read/delete access is restricted to that actor plus any already established administrative rule;
- attaching the File to a Cave/proposal independently revalidates Cave/request permission and account ownership;
- once a File is represented through a request, request-specific access may use the existing request authorization path.

Do not make all account users able to read arbitrary unbound staging merely because `CaveId` is null.

Return enough metadata for the editor to add the staged File to its form state: ID, editable `Name`, immutable
`Extension`,
selected/default File type metadata as appropriate, and any existing `FileVm` fields used by the editor.

### 4.2 Add File upload to `AddCaveComponent`

Reuse the existing upload UI primitive if it fits; do not duplicate progress/error handling unnecessarily. On successful
staging, append the returned File metadata to the form's `files` desired state.
Existing File rename/type/remove controls remain form-only intent. Do not call `FileService.UpdateFilesMetadata` or a
direct published mutation endpoint from inside the editor.

If a newly staged File is removed from the form, leave it staged and expiring. Do not make Save depend on synchronous
cleanup.

### 4.3 Backend complete desired File state

The direct Cave Save path must interpret `AddCaveVm.Files` as the desired current File set for the Cave:

- existing File IDs retained in the request stay associated;
- metadata changes are applied inside the Cave publication transaction;
- existing current File IDs omitted from desired state are logically removed;
- staged File IDs included in desired state are validated and associated/published;
- duplicate IDs or foreign-account/foreign-Cave IDs are rejected;
- removal/metadata/addition participate in the same one-revision semantic comparison.

Do not publish Files first and then publish the Cave.

### 4.4 Remove detail-page published File mutation

Remove `UploadComponent`/`CaveService.AddCaveFile` usage from `CaveComponent.tsx`. Keep current File display/download.
An authorized Edit action should take the user to the aggregate editor instead.

Retire the old direct published File endpoint if nothing outside an intentionally separate workflow still requires it.
Do not leave it as a second supported interactive publication path.

### 4.5 Phase exit gate

Prove editor File add/edit/remove, cancel-with-staged-file, File-only Save, no-op after removing a newly staged File, and
read-only detail behavior before moving to line plots.

## Phase 5 — Add line plots to the aggregate editor without redesigning storage
Primary files:

- `Planarian/Planarian.Model/Database/Entities/RidgeWalker/CaveGeoJson.cs`
- `Planarian/Planarian/Modules/Caves/Models/GeoJsonUploadVm.cs` or a renamed aggregate-edit model
- `Planarian/Planarian/Modules/Caves/Models/AddCaveVm.cs`
- a small new direct-edit authoring-context model under `Planarian/Planarian/Modules/Caves/Models/`
- `Planarian/Planarian/Modules/Caves/Repositories/CaveRepository.cs`
- `Planarian/Planarian/Modules/Caves/Services/CaveService.cs`
- `Planarian/Planarian/Modules/Caves/Controllers/CaveController.cs`
- `Planarian.Web/src/Modules/Caves/Models/GeoJsonUploadVm.ts`
- `Planarian.Web/src/Modules/Caves/Models/AddCaveVm.ts`
- `Planarian.Web/src/Modules/Caves/Components/AddCaveComponent.tsx`
- `Planarian.Web/src/Modules/Caves/Components/CaveComponent.tsx`
- `Planarian.Web/src/Modules/Caves/Components/GeoJsonSaveModal.tsx`
- `Planarian.Web/src/Modules/Caves/Service/CaveService.ts`

### 5.1 Add line plots to the edit DTO/form

Represent desired line plots explicitly in the aggregate Cave edit model. Reuse `GeoJsonUploadVm` if that produces a
clear model; otherwise rename/replace it with an edit-oriented type rather than keeping parallel duplicate contracts.

Required data per desired line plot:

```text
Id?          // existing logical ID, absent for new
Name
GeoJson      // exact current desired payload
```

Do not add full GeoJSON bodies to ordinary `CaveVm`; normal Cave/detail reads should remain lightweight. Add a dedicated
direct-edit authoring context, conceptually:

```csharp
CaveEditAuthoringContextVm(
    CaveVm Cave,
    IReadOnlyList<CaveLinePlotEditVm> LinePlots)
```

Expose it from the Cave module through an edit-context endpoint authorized for the same manager/admin capability as
direct Save. Its repository query is account-qualified, no-tracking, and returns current line-plot ID/name/body for that
Cave. `CaveVm.CurrentRevisionId` in the returned Cave remains the source for `ExpectedRevisionId`.

Update `EditCavePage.tsx` to load this edit context instead of the ordinary detail `GetCave` response, then map the Cave
and line plots into one `AddCaveVm` form. Do **not** reuse `api/map/lineplots/{id}` for authoring: that route is a map
rendering/read endpoint, returns no line-plot name, and has independent long-lived HTTP caching. Authoring must receive a
fresh aggregate edit context.

The initial proposal authoring context gets the same current line-plot authoring payload in Phase 7 under View
authorization; proposal revise uses the immutable proposal snapshot rather than refetching mutable current bodies.

### 5.2 Preserve stable IDs
Replace the current wholesale remove/recreate behavior with ID-aware desired-state application:

- existing ID + unchanged name/content → retain without needless write;
- existing ID + changed name → update name;
- existing ID + changed content → update `GeoJson`;
- no ID → allocate/add a new `CaveGeoJson`;
- current existing ID omitted from desired state → remove it;
- supplied ID not owned by current account/target Cave → reject;
- duplicate supplied IDs → reject.

Do not create a new logical ID merely because content changed.

### 5.3 Editor UX

Move the current save intent from the detail-page `GeoJsonSaveModal` into `AddCaveComponent` or a focused child
component owned by the Cave editor. Reuse existing shapefile/GeoJSON parsing helpers where appropriate; do not move map
rendering architecture into the form.

The editor must support:

- adding a parsed/uploaded line plot;
- naming/renaming it;
- replacing its desired GeoJSON content;
- removing it before Save/Submit;
- retaining unchanged existing plots.

The form authoring step must not persist `CaveGeoJson` immediately.

### 5.4 Remove standalone published GeoJSON mutation

Remove/retire `CaveController.UploadCaveGeoJson`, `CaveService.UploadCaveGeoJson`, and frontend
`CaveService.uploadCaveGeoJson` as direct published mutation paths once all supported authoring routes use the aggregate.
If an endpoint is still useful only for parsing/previewing source data, make it explicitly non-persisting and name it
accordingly. Do not keep hidden publication behavior.

Keep current line-plot map retrieval/rendering unchanged aside from removing persistence controls from the detail page.

### 5.5 Phase exit gate

Focused tests must prove stable-ID rename/content update, add/remove, duplicate/foreign ID rejection, line-plot-only Save,
no-op retention, and absence of a standalone published GeoJSON bypass.

## Phase 6 — Extend published snapshot, semantic comparison, and diff

Primary files:

- `Planarian/Planarian.Model/Database/Revisions/CavePublishedSnapshotV1.cs`
- `Planarian/Planarian/Modules/Caves/Revisions/CavePublishedSnapshotRepository.cs`
- `Planarian/Planarian.Model/Database/Revisions/CaveRevisionDiffService.cs`
- revision view models under `Planarian/Planarian/Modules/Caves/Models/`
- `Planarian.Web/src/Modules/Caves/Models/CaveRevisionVm.ts`
- `Planarian.Web/src/Modules/Caves/Components/CaveRevisionDiff.tsx`
- `Planarian.Web/src/Modules/Caves/Components/CaveRevisionDiffPresentation.ts`
- V1 JSON fixtures and contract/diff tests under `Planarian.Tests.Unit/Caves/Revisions/`.

### 6.1 Keep File snapshot provider-neutral and public-safe

Do **not** add storage partition/key fields to `CaveFileSnapshotV1`. Its stable File ID plus historical `Name`, immutable
`Extension`, and File-type metadata remain the persisted revision contract. After ordinary published removal, the old
object is cleanup work rather than historical revision content; successful cleanup makes those bytes unavailable.

This matters because Cave revision comparison returns `CavePublishedSnapshotV1` through the API. Never expose internal
object-storage addresses to make cleanup or hypothetical recovery convenient. File and line-plot history should both
preserve lightweight semantic evidence while omitting removed payloads.

### 6.2 Add line-plot snapshot

Add `CaveLinePlotSnapshotV1` with stable ID, historical name, and deterministic content hash. Add the collection to
`CavePublishedSnapshotV1`.

Update `CavePublishedSnapshotRepository.BuildManyAsync` with a bounded no-tracking line-plot projection. The current
comment says five projection groups per chunk; update the implementation/comment/tests truthfully if line plots make it
six. Preserve bounded chunking and account qualification.

Sort line plots by ID with ordinal semantics before snapshot serialization.

### 6.3 Centralize hashing

Create one server-side helper for deterministic JSON normalization/fingerprinting and SHA-256. Use it everywhere this
feature needs line-plot content equality. Do not compute persisted-history hashes in React.

Document/test exactly what bytes/normalized JSON representation are hashed so future readers do not accidentally change
the persisted V1 meaning.

### 6.4 Semantic no-op

Keep existing File semantic comparison based on stable File ID and historical user-facing metadata. Storage-provider
address changes are infrastructure details and must not manufacture a user-visible Cave revision. Add line-plot
ID/name/hash to aggregate semantic comparison. Collection ordering must not create a diff.

### 6.5 Diff and presentation

Extend backend diff models and frontend presentation for line-plot Added/Removed/Renamed/ContentChanged. Use stable ID
to pair old/new logical plots. Show readable metadata and “content changed”; do not render raw GeoJSON in history.

Update empty-diff helpers so line-plot-only proposals/saves are not treated as empty.
### 6.6 V1 fixture/contract handling

Because this feature is not yet landed, update the existing V1 JSON fixtures and contract tests to the corrected target
shape. Do not introduce V2 solely to preserve a branch-only pre-release V1 shape.

Once this work lands, the updated V1 becomes immutable under `docs/cave-revisions.md`.

### 6.7 Phase exit gate

Prove deterministic serialization/order, line-plot hash stability, all line-plot diff cases, absence of storage locators
from the public V1 snapshot, no-op collection reorder, and bounded/tenant-qualified snapshot repository behavior.

## Phase 7 — Make proposal versions asset-complete and immutable

Primary files:

- `Planarian/Planarian.Model/Database/Revisions/CaveProposalSnapshotV1.cs`
- `Planarian/Planarian.Model/Database/Entities/RidgeWalker/CaveChangeRequest.cs`
- `Planarian/Planarian/Modules/Caves/Revisions/CaveChangeRequestRepository.cs`
- `Planarian/Planarian/Modules/Caves/Services/CaveChangeRequestService.cs`
- `Planarian/Planarian/Modules/Caves/Models/CaveChangeRequestVm.cs`
- `Planarian.Web/src/Modules/Caves/Pages/SuggestCaveChangesPage.tsx`
- `Planarian.Web/src/Modules/Caves/Pages/ReviseCaveChangeRequestPage.tsx`
- `Planarian.Web/src/Modules/Caves/Pages/CaveChangeRequestPage.tsx`
- `Planarian.Web/src/Modules/Caves/Helpers/CaveFormMapper.ts`
- proposal authoring/history/file integration tests.

### 7.1 Initial proposal File staging

Use the generic authoring-stage upload from Phase 4 in `SuggestCaveChangesPage`; do not require a request ID before the
user can add a File. The initial proposal form should use the same File editor behavior as direct edit.

Extend the existing `CaveProposalAuthoringContextVm`/`GetAuthoringContextAsync` result with the current exact line-plot
authoring payload (ID, name, GeoJSON) alongside `CaveVm` and `ExpectedBaseRevisionId`. Load it through the Cave proposal
repository/service under the existing View authorization. Do not add full GeoJSON to ordinary `CaveVm` and do not fetch
authoring content through the map rendering endpoint.

`SuggestCaveChangesPage` maps that context into the same aggregate form model used by direct edit. The context's expected
base revision and line-plot bodies are one authoring read; if publication changes afterward, the existing stale-base
preview/submit check handles it explicitly.

### 7.2 New-Cave proposal identity

Support creation through the same immutable proposal workflow rather than a parallel submission model. Reserve the
eventual Cave ID when the request is created, but do not create a placeholder Cave or revision. A creation request and
all of its proposal versions have a null `BaseRevisionId`; existing-Cave proposals retain their immutable published base.

A View-capable account member may submit a creation proposal for any configured State/County. Review authorization is
resource-based on the current proposed destination grant, not on `UserCavePermissions` expansion through existing Caves,
so the first proposed Cave in an empty County remains reviewable. Raw grant queries preserve the same permission-domain
boundary as `RequestUser`: Account Admin comes only from `PermissionType.User`, Cave authority comes only from
`PermissionType.Cave` `View`/`Manager`, and malformed cross-family or reserved-role rows fail closed. A reviewer may revise
only to another destination they currently manage; the submitter may change destination and route the request to that
destination's Managers.

Approval uses the existing new-Cave publication path inside the request approval transaction, allocates County Cave
Number from authoritative account+County data, creates the first revision, grants the submitter exact Cave `View`
when they are still an Account member, and then marks the request approved. Rejection publishes nothing.

### 7.3 Proposal File identity

Keep `ProposalFileIntent` as a complete desired-state intent model. A `PublishStaged` intent references the exact immutable
staged `File.Id`; retained published Files reference their exact existing IDs.

Do not add a proposal-version/File join table unless the current serialized proposal contract proves insufficient. The
snapshot already provides semantic version ownership; avoid redundant schema when a stable immutable File ID is enough.

If `CaveChangeRequestStagedFile` remains, redefine its responsibility clearly as lifecycle/access indexing for the pending
request, not immutable proposal meaning. While a request is pending, do not physically delete a staged File still
referenced by any proposal version that the product exposes as reviewable history.

When request resolution allows rejected/unpublished staged bytes to be cleaned, older proposal metadata remains readable
but must not present missing bytes as downloadable.

### 7.4 Proposal line plots

Extend `CaveProposalSnapshotV1` with complete desired line plots containing logical ID (where existing), name, and exact
GeoJSON payload. Update serialization/contract fixture while V1 is still pre-release.

Preview and proposal-version no-op detection must include line plots. Do not derive an old version's line-plot content
from mutable current `CaveGeoJson` rows.

### 7.5 Revision of a pending proposal

`snapshotToForm` and `ReviseCaveChangeRequestPage` must reconstruct the selected/current proposal version's exact File and
line-plot intent. A new version may stage new Files, retain old ones, or remove them; creating the new version never
mutates the older serialized proposal.

Keep existing `ExpectedBaseRevisionId` and `ExpectedProposalVersionId` conflict behavior.
### 7.6 Phase exit gate

Prove:

- initial proposal can stage a File before request creation;
- File-only proposal remains valid;
- line-plot-only proposal is valid;
- empty aggregate proposal is rejected;
- old proposal version retains exact File IDs and line-plot payload after a newer version is authored;
- cross-account/unowned staged File cannot be referenced;
- stale published base and stale active proposal version conflicts still behave explicitly.

## Phase 8 — Approval publishes the exact selected proposal version

Primary files:

- `Planarian/Planarian/Modules/Caves/Services/CaveChangeRequestService.cs`
- `Planarian/Planarian/Modules/Caves/Revisions/CaveChangeRequestRepository.cs`
- `Planarian/Planarian/Modules/Caves/Revisions/CaveMutationWorkflow.cs`
- `Planarian/Planarian/Modules/Caves/Revisions/CaveMutationRepository.cs`
- affected proposal publication/concurrency/File tests.

### 8.1 Reconstruct exact desired aggregate

Approval loads the selected immutable proposal version and reconstructs:

- Cave scalar state;
- tags/People intents under existing rules;
- Entrances;
- exact File intents/IDs;
- exact proposed line-plot bodies.

Never substitute the current request-level staging collection or current `CaveGeoJson` payload for the selected version.
### 8.2 Publish through the canonical mutation boundary

Revalidate current authorization, tenant/reference rules, and stale-base requirements, then apply the complete desired
aggregate through the same publication semantics as direct edit. Persist normalized Cave state, the one resulting
revision, request approval state, reviewer metadata, and provenance linkage atomically.

Do not create independent per-asset revisions and do not commit File/line-plot publication before the Cave revision.

The approved revision snapshot must be built from validated resulting persisted semantics, not copied blindly from
proposal JSON.

### 8.3 File publication during approval

For `PublishStaged` File intents, associate the exact staged File/object and clear expiration. Do not copy it to another
object key. Retained published Files keep their identity. Removed published Files leave current membership; their object address is queued transactionally and physical deletion runs only after approval commits.

### 8.4 Line plots during approval

Apply the exact line-plot desired state from the selected proposal version using the same stable-ID rules as direct edit.
The current mutable line-plot rows are the publication target, not the source of proposal truth.

### 8.5 Rejection

Keep existing rejection semantics: preserve request/version/reviewer history, publish nothing, create no `CaveRevision`.
Clean up unpublished staging only according to the lifecycle rules and without falsifying historical metadata.

### 8.6 Phase exit gate

Prove exact selected-version publication, combined one-revision approval, no-copy File promotion, line-plot publication,
stale approval conflict, concurrent approval safety, and rejection no-write behavior.

## Phase 9 — Re-check archive, import, deletion, and map compatibility
This phase is compatibility review, not permission to redesign unrelated subsystems.

### 9.1 Snapshot builder/import scale

If the line-plot projection increases `CavePublishedSnapshotRepository.BuildManyAsync` projection groups, keep it
bounded/no-tracking and update structural scale assertions only if their expected query shape is intentionally changed.

Do not convert import to per-Cave editor calls. Run only focused import tests if the shared snapshot shape or deletion
semantics actually touch import behavior.

### 9.2 Archive

Verify current account archive export/import still handles current published Files and current `CaveGeoJson` data.
Removed historical File payloads, storage-cleanup debt, staging, and proposal payload history are not automatically added to archive output.

### 9.3 Hard Cave/account deletion

Inspect every directly affected hard-delete path to ensure it does not accidentally treat ordinary revisioned File
removal as hard purge or leave new staged lifecycle rows orphaned. Preserve the existing unresolved-proposal deletion
block.

Do not define a new legal/privacy purge policy in this task.

### 9.4 Map

Verify current line plots still render through the existing map query/rendering path. Do not change the map to PostGIS or
MVT. The detail page should simply lack the old persistence control.

### 9.5 Phase exit gate

Run only the focused compatibility tests identified by actual code reachability. If import/archive/map code did not
change and the shared contract has targeted coverage elsewhere, do not manufacture unrelated test churn.

## Phase 10 — Durable docs and final bypass review
### 10.1 Update durable architecture

After behavior is implemented and focused tests pass, verify `docs/cave-revisions.md` matches the final implemented and
normative truth. The current completion branch has already removed the old approval-candidate blob-copy/disposable-history
model, so do not reapply that historical transition mechanically; update only facts that actually changed.

Update `docs/data-access-architecture.md` only if the new provider-neutral external-storage boundary adds a durable rule
worth stating there. Do not copy the whole active plan into permanent architecture docs.

Update `docs/testing.md` only if this work establishes a genuinely reusable testing command/pattern, not merely to list
feature-specific test names.

### 10.2 Remove obsolete routes/code

Search the repository for the old bypasses and blob-copy concepts after refactoring. There should be no supported
interactive caller remaining for:

```text
CaveService.AddCaveFile / direct published Cave File upload
CaveService.uploadCaveGeoJson / GeoJsonSaveModal publication
CaveController.UploadCaveGeoJson publication
approval-attempt source/destination blob copy for newly staged Cave Files
```

Delete dead components/models/helpers if they have no other legitimate use. Do not keep commented-out historical
implementations.

### 10.3 Fresh adversarial pass

Before declaring completion, inspect the final branch diff against the actual merge target (currently `main`). If
`feature/cave-revisions` is still useful as a historical parent for isolating this completion work, inspect that delta too.
Specifically look for:

- a mutation route that still bypasses Cave revision publication;
- a stale-client path that still substitutes current server revision state;
- an asset operation that commits independently before aggregate publication;
- a File cleanup path that deletes bytes for an ordinary revisioned removal;
- proposal history whose meaning depends on mutable request/current asset state;
- line-plot delete-all/recreate behavior that destroys stable IDs;
- an `IgnoreQueryFilters()` query without explicit account qualification;
- a newly introduced service-owned DbContext/provider dependency;
- snapshot query growth that breaks the bounded import-scale model;
- frontend tests asserting implementation details instead of user-visible behavior.

### 10.4 Completion gate

Run the exact focused completion validation in `validation.md`. Report commands and results truthfully. Do not claim the
full repository passed if it was not run.

Only after the code and durable docs agree should this active plan be considered complete. At that point either remove
the temporary routing notice from `AGENTS.md`/`docs/cave-revisions.md`, or keep the plan explicitly marked completed if
it remains useful as historical implementation rationale.

## Expected new/changed model summary

The final shape should be conceptually close to:

```text
AddCaveVm
    ExpectedRevisionId?          // direct existing-Cave concurrency
    Files[]                      // complete desired File state
    LinePlots[]                  // complete desired current line-plot state

CavePublishedSnapshotV1
    ... existing state ...
    Files[] = existing stable ID + historical user-facing metadata
    LinePlots[] = { Id, Name, ContentHash }

CaveProposalSnapshotV1
    ... existing desired state ...
    Files[] = complete exact File intents
    LinePlots[] = exact desired payloads
```
