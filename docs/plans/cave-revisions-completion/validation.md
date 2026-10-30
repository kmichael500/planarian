# Focused testing and completion validation

This document defines the validation contract, not the live pass/fail ledger. Current SHA-pinned results and known gaps are
tracked in [`../../../CAVE_REVISIONS_COMPLETION_STATUS.md`](../../../CAVE_REVISIONS_COMPLETION_STATUS.md).

This task is broad in behavior but should not trigger indiscriminate repository validation while developing. Follow
`docs/testing.md` and run the narrowest tests that prove the changed contracts. Expand when an actual dependency or shared
contract makes it necessary; the final branch currently touches shared migration/storage/revision infrastructure, so the
live status ledger may legitimately require broader completion validation than an individual development step.

## 1. Test placement rules

Use existing cohesive test classes when the new scenario naturally belongs there. Create a focused class when adding the
scenario to an existing class would make that class conceptually mixed or excessively large.

Recommended homes:

- pure snapshot/hash/diff/contract behavior → `Planarian.Tests.Unit/Caves/Revisions/`;
- real PostgreSQL transaction/concurrency/tenant/repository behavior → `Planarian.Tests.Integration/Caves/Revisions/`;
- external object-storage behavior in service tests → a fake `IObjectStorage`, not live Azure;
- frontend mapping/diff/presentation → existing Cave helper/component tests;
- frontend editor/detail behavior → focused page/component tests if the current React test harness supports them cleanly.

Do not use EF InMemory/SQLite for transaction, lock, tenant-filter, `xmin`, or repository semantics.

Tests are scenario documentation. Keep permission grants, competing revisions, staged assets, and other business-significant
setup visible; hide only mechanical database/auth/service composition in helpers.

## 2. Required direct-edit concurrency tests

Add focused coverage for:

1. Load/edit R1, publish a competing R2, submit direct edit with `ExpectedRevisionId = R1` → conflict/409.
2. The stale submission changes no Cave state and creates no extra revision.
3. Correct expected revision publishes exactly one new revision.
4. Existing-Cave direct edit with missing expected revision is rejected.
5. New-Cave creation still accepts a null expected revision.
Frontend mapping coverage must prove `CaveVm.currentRevisionId` becomes `AddCaveVm.expectedRevisionId` for direct edit.

## 3. Required aggregate publication tests

Prove one aggregate mutation boundary:

6. One request changes a scalar field, tag, Entrance, File, and line plot → exactly one revision.
7. File-only mutation → exactly one revision.
8. Line-plot-only mutation → exactly one revision.
9. Complete semantic no-op → no revision.
10. Reordering Files/line plots without semantic changes → no revision.
11. Upload staged File, remove it from desired state before Save, change nothing else → no revision attributable to it.
12. A relational failure during publication leaves current Cave state/revision unchanged.

Where a no-write assertion matters, use the existing integration-test conventions rather than merely checking the HTTP
response.

## 4. Required File storage/lifecycle tests

Use a deterministic fake `IObjectStorage` that records puts/reads/deletes. Do not test Azure's SDK implementation details.

13. New staged File performs one object write to its immutable address.
14. Successful publication performs no second write/copy and retains the same File ID/address.
15. Publication clears staging expiration and associates the File with the target Cave.
16. Failed relational publication leaves the File staged/expiring and the Cave unchanged.
17. Removing a previously published File queues exactly one tenant-qualified `StorageCleanupTarget` with purpose
    `CaveAssetRemoval` inside the publication transaction and does not delete bytes before commit.
18. Successful post-commit cleanup deletes the object and clears the target while the historical snapshot remains readable.
19. Failed external deletion leaves durable cleanup debt that can be retried idempotently.
    - A mixed cleanup batch handles success, already-missing objects, and provider failure independently so only failed debt remains.
    - If object deletion succeeds but cleanup-target acknowledgement fails, the target remains and a later retry safely clears it after observing the object is already absent.
20. Ordinary `CaveAssetRemoval` debt does not trigger the Reset Account write block.
21. Reset Account absorbs pending asset-removal addresses without losing cleanup responsibility.
22. Expired never-published staged File cleanup deletes exactly that object and creates no historical retention record.
23. Cross-account staged File cannot be attached/published; cleanup never deletes an address referenced by a live File.
24. Published File download still reads through the provider-neutral storage boundary.
## 5. Required line-plot tests

25. Unchanged existing line plot retains ID and does not cause unnecessary semantic change.
26. Rename retains ID and produces a rename diff.
27. Content replacement retains ID and produces `ContentChanged`.
28. Addition receives a new ID and produces Added.
29. Removal produces Removed and no longer appears in current state.
30. Duplicate incoming line-plot IDs are rejected.
31. Existing line-plot ID from another Cave is rejected.
32. Existing line-plot ID from another account is rejected.
33. Published snapshot line plots are deterministically ordered by ordinal ID.
34. Centralized content fingerprint is deterministic for the server's chosen normalized JSON representation.
35. Snapshot/diff collection ordering does not produce false changes.
36. The old standalone GeoJSON publication endpoint/path can no longer mutate published state outside a revision.
37. Current map retrieval/rendering sees only current `CaveGeoJson` rows and remains functional.

Do not add tests for future PostGIS/MVT behavior in this task.

## 6. Required V1 snapshot/diff contract tests

Update the pre-release V1 fixtures and contract tests to prove:

38. `CaveFileSnapshotV1` remains limited to stable File ID plus historical user-facing metadata; it does not expose
    storage partition/key through the revision API.
39. A removed published File keeps its stable snapshot File ID/metadata while the live `File` row is gone; revision JSON
    contains neither a storage locator nor the removed bytes.
40. `CaveLinePlotSnapshotV1` serializes ID/name/content hash but never raw GeoJSON.
41. V1 enum tokens remain strings and numeric enum tokens remain invalid.
42. Round-trip serialization/deserialization preserves the corrected pre-release V1 shape.
43. Added/Removed/Renamed/ContentChanged line-plot diffs have stable, readable structure.
44. Frontend empty-diff helpers treat line-plot-only changes as non-empty.
## 7. Required proposal-version tests

45. Initial proposal can stage a File before the request exists.
46. File-only proposal is valid.
47. Line-plot-only proposal is valid.
48. Aggregate-empty proposal is rejected.
49. Proposal V1 stores exact File identities/dispositions.
50. Proposal V1 stores exact desired line-plot GeoJSON payloads.
51. Authoring V2 does not mutate V1's File or line-plot meaning.
52. A later current `CaveGeoJson` change does not alter an older proposal version's line-plot payload.
53. A staged File still needed by an unresolved/reviewable proposal version is not physically deleted by lifecycle cleanup.
54. Cross-account/unowned staged File IDs are rejected during preview/submit/versioning.
55. Current View permission remains sufficient to propose.
56. Loss of current Cave visibility still prevents later request/version/staged-file access as documented.
57. Existing People-only free-form tag restrictions still apply in proposal authoring.

## 8. Required approval/rejection tests

58. Approval publishes the exact selected proposal version, not mutable current/request state.
59. Combined File + line-plot + ordinary field approval creates exactly one Cave revision.
60. `PublishStaged` associates the exact staged File without storage copy/re-upload.
61. Approval applies exact proposed line-plot body using stable-ID rules.
62. Stale published base returns the existing explicit conflict and leaves the request pending/unpublished.
63. Stale expected active proposal version returns the proposal-version conflict.
64. Competing approval/publication race preserves existing `xmin`/transaction correctness.
65. Rejection publishes no Cave state and creates no revision.
66. Request/reviewer audit history remains available after rejection.

## 9. Frontend behavior coverage

Add focused React/helper tests where the current harness supports them cleanly:
67. `CaveFormMapper` preserves direct-edit expected revision and maps Files/line plots correctly.
68. Cave editor can stage/add a File, change its metadata, and remove it from desired state.
69. Cave editor can add/rename/replace/remove a line plot without persisting before Save/Submit.
70. Direct editor 409 shows an explicit stale-edit message and does not auto-resubmit.
71. Cave detail page has no direct published File upload control.
72. Cave detail page has no direct GeoJSON save/publish control.
73. Existing current File download/view remains available.
74. Existing current line plots still render.
75. Suggest/revise proposal forms can author the same File/line-plot desired state.
76. Revision diff presentation renders line-plot metadata changes without raw GeoJSON.

Do not create brittle tests whose only purpose is asserting removed component implementation details. Test the resulting
user-visible capability boundary: read-only detail, mutation through editor, explicit conflict, correct diff.

## 10. Conditional import/archive/deletion coverage

Only run/add these when the implementation actually reaches the shared contract:

- snapshot projection structural test when line plots add a bounded query group;
- focused import revision/snapshot test when import consumes the changed snapshot shape;
- archive round-trip/current-state test when archive models or serializers change;
- hard-delete/staged-cleanup test when deletion code changes;
- Migration category + pending-model check only when the EF model/schema actually changes.

Do not run unrelated Golden/Scale suites merely because this plan mentions import compatibility. If a shared snapshot
change alters the scale test's measured query structure, then that specific structural test is relevant and should run.

## 11. Development command pattern

Use `--no-restore` after the branch has already been restored. Replace the filter with the exact class/namespace being
worked on; these are patterns, not an instruction to run every revision test after every edit.
Backend unit example:

```bash
dotnet test Planarian.Tests.Unit/Planarian.Tests.Unit.csproj \
  --configuration Release --no-restore \
  --filter 'FullyQualifiedName~CaveRevision'
```

Backend integration example while working one capability:

```bash
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj \
  --configuration Release --no-restore \
  --filter 'FullyQualifiedName~CaveChangeRequestFilePublicationIntegrationTests'
```

For a new focused class, filter that exact class. Prefer a small number of explicit filtered invocations over one broad
`Caves.Revisions` run while developing.

Frontend example:

```bash
cd Planarian.Web
npm test -- --watchAll=false CaveFormMapper CaveRevisionDiff CaveService
```

Add the exact new editor/detail test filename/pattern when those tests are introduced. Do not run all frontend tests by
default.

## 12. Directly impacted build checks

After the focused tests are green, build only the directly changed product projects:

```bash
dotnet build Planarian/Planarian/Planarian.csproj --configuration Release --no-restore
```
The main application project transitively compiles its directly referenced model/library projects; do not separately
build every project unless a failure requires isolation.

Because the web client changes materially, also run:

```bash
cd Planarian.Web
npm run build
```

Do not run `dotnet build Planarian/Planarian.sln` solely for this feature unless the final diff reaches projects outside
the directly impacted graph or repository policy is intentionally broadened.

### Required EF/migration validation

This plan intentionally generalizes the reset cleanup outbox into `StorageCleanupTarget`, adds a purpose discriminator,
and removes the superseded `RetainedCaveFileObject` table, so a model/schema migration is required. Run the
migration-focused integration tests that prove a pristine database and the supported main baseline upgrade to latest:

```bash
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj \
  --configuration Release --no-restore \
  --filter 'FullyQualifiedName~Migration|FullyQualifiedName~PostgresFoundationIntegrationTests'
```

The cleanup migration **does** transform pre-release branch data: existing Reset Account debt must remain `AccountReset`
and existing retained File locators must become `CaveAssetRemoval` cleanup debt. Dedicated migration tests therefore must
prove that conversion, including an old-row-ID collision, and prove downgrade fails closed while published or unpublished ordinary File cleanup debt exists.
Keep the empty/latest and **exact current merge-target (`main`)** upgrade contracts as the broader schema coverage.

Keep historical compatibility coverage separate from the current-main-v32 upgrade/rollback regression.
Deployment-safety claims must come from the current-main regression, not from an older baseline fixture. For this migration,
the downgrade target is the **v32 schema/data contract**, not reconstruction of deliberately removed unused
`ReportedByUserId` values. Migration tests must verify that distinct legacy `FileName` values survive an untouched
round trip, post-upgrade File Name/Extension edits and newly-created Files downgrade from current metadata, the normalized
v32 PostgreSQL schema contract (column types/nullability/defaults, constraints, and indexes; excluding physical column
order) round-trips unchanged, populated completion-owned tables can downgrade, mismatched Cave/File tenant ownership
fails closed transactionally, and Entrance Other rollback removes only rows proven to have been inserted by its backfill.

Revision-history rollout is a separate deployment step after the schema migration. Its PostgreSQL coverage must prove
that every pre-existing Cave receives exactly one system `Baseline`, all rollout entries share one boundary timestamp and
no user actor, partial initialization resumes with the original boundary, reruns are idempotent, and a late baseline is
rejected once ordinary revision publication has begun. Keep a supported-scale rollout test so initialization remains
bounded rather than degenerating into one SaveChanges/query loop per Cave. Normal API startup must fail closed until no
live Cave has a null `CurrentRevisionId`.

Then run the repository-required pending-model check:

```bash
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project Planarian/Planarian.Migrations/Planarian.Migrations.csproj \
  --startup-project Planarian/Planarian.Migrations/Planarian.Migrations.csproj \
  --context PlanarianDbContext --configuration Release --no-build
```

Do not create an additional migration merely for legacy `BlobKey`/`BlobContainer` naming; the required migration is the
shared storage-cleanup outbox/purpose conversion and removal of the superseded retained-object table.

## 13. Focused completion sequence

At the end of implementation, perform validation in this order:

1. run all changed/new Cave revision **unit** test classes;
2. run all changed/new direct-edit/File/line-plot/proposal **integration** test classes;
3. run the changed/new Cave frontend tests;
4. run the directly impacted backend build;
5. run the frontend build;
6. run the required migration tests and pending-model check;
7. run conditional import/archive/deletion checks only if their contracts changed beyond the focused retention cases;
8. run `git diff --check`;
9. inspect the final diff against the actual merge target (currently `main`) for the bypass/concurrency/tenant issues
   listed in `implementation.md` Phase 10; use `feature/cave-revisions` only as an additional historical delta if useful;
10. search for obsolete direct File/GeoJSON publication and staged blob-copy call sites;
11. confirm durable docs match implemented behavior.

Do not convert this focused sequence into a full repository suite unless a failure or changed shared contract gives a
specific reason.

## 14. Final acceptance checklist

These checkboxes are acceptance criteria, not a live status board. The final architecture/safety acceptance review at
`52f2c593` classified the applicable criteria as proven from source and executable tests; the follow-on GAP-015 integration
adds focused PostgreSQL evidence for duplicate approvals, staged-File claim races, lost object-create responses, and lost
database-commit/save acknowledgements. No criterion in this section requires the deferred GAP-014 periodic inactive-account
sweeper. A completed checkbox reflects this evidence, not a claim that every broad suite reran on the final docs commit.
The status ledger separates final focused runs from historical SHA-pinned broad validation.

Key falsifiable suites: `CaveAggregateAssetIntegrationTests`, `CaveChangeRequestConcurrencyIntegrationTests`,
`CaveChangeRequestStagedFileIntegrationTests`, `ExternalFileCleanupIntegrationTests`,
`CaveChangeRequestCreateIntegrationTests`, `CaveRevisionRolloutScaleIntegrationTests`,
`ImportScaleBenchmarkTests`, `MigrationUpgradeIntegrationTests`,
`CurrentMainDatabasePreservationMigrationIntegrationTests`, `ArchiveGeneratedContentIntegrationTests`,
the architecture/contract tests, frontend authoring tests, and Chromium/WebKit `npm run test:browser:e2e`.
See `CAVE_REVISIONS_COMPLETION_STATUS.md` for the exact run provenance and accepted residual limitations.

Acceptance conditions verified:

### Concurrency and aggregate publication

- [x] Existing direct edits submit the client-loaded `ExpectedRevisionId`.
- [x] Missing/stale expected revision cannot silently publish over newer Cave state.
- [x] Stale direct edit returns explicit 409/conflict UX.
- [x] One aggregate direct edit creates at most one `CaveRevision`.
- [x] One aggregate approved proposal creates at most one `CaveRevision`.
- [x] Complete semantic no-op creates no published revision and no proposal version.

### File authoring and storage

- [x] Cave editor can stage/add, edit metadata, and remove Files.
- [x] Cave detail page is read-only for published File mutation.
- [x] A new staged Cave File is uploaded once to one immutable object identity.
- [x] Publication associates the same File/object; no staging→published byte copy remains.
- [x] Failed publication leaves the staged File recoverable/expiring and Cave unchanged.
- [x] Ordinary removal of a once-published File queues `CaveAssetRemoval` cleanup, deletes the live `File` row, publishes
      history metadata, and physically deletes bytes only after commit.
- [x] Failed object deletion leaves durable retryable cleanup debt without blocking unrelated account writes.
- [x] Mixed cleanup batches independently acknowledge successful/already-missing objects while retaining only failed debt.
- [x] A post-delete cleanup-target acknowledgement failure is idempotently recoverable on a later retry.
- [x] Removed historical File content does not keep a live FileTypeTag relationship in use.
- [x] Expired never-published staging is physically cleaned up without creating historical retention state.
- [x] Affected application logic depends on provider-neutral storage types, not Azure SDK types.
- [x] Published File snapshot does not expose object-storage locators or promise historical payload recovery.
- [x] Cross-account/unowned staged File attachment is rejected.

### Line plots

- [x] Cave editor can add, rename, replace, and remove line plots.
- [x] Existing line-plot IDs remain stable across rename/content replacement.
- [x] Cave detail page cannot directly persist GeoJSON.
- [x] No standalone published GeoJSON mutation path bypasses Cave revisions.
- [x] Current `CaveGeoJson` `jsonb` storage remains in place.
- [x] Current map line-plot retrieval/rendering remains in place and functional.
- [x] Published snapshot contains line-plot ID/name/content hash, not raw GeoJSON.
- [x] Line-plot Added/Removed/Renamed/ContentChanged diffs are readable and deterministic.
- [x] No GeoJSON recovery table/blob/rollback system was added.

### Proposals

- [x] View permission remains sufficient to author a proposal against an existing Cave.
- [x] Effective Account View capability can submit a new-Cave proposal in any configured State/County without
      pre-existing destination Cave visibility.
- [x] A new-Cave request reserves its Cave ID without creating a Cave or fake base revision before approval.
- [x] County/State Managers can review a new-Cave proposal in an empty County; Cave-only or unrelated Managers cannot,
      and removing destination Manager authority immediately removes review/read/decision access.
- [x] Malformed or unsupported persisted permission rows fail closed across Cave visibility, creation-request
      visibility/review, regional Manager selectors, scoped capability probes, and client projection: Cave roles require
      `PermissionType.Cave` plus the supported `View`/`Manager` key, Account Admin requires `PermissionType.User`, unknown
      semantic keys confer no authority, and global/synthetic capabilities are never trusted from account-scoped rows.
- [x] Permission-management writes resolve definitions by (`PermissionType`, `Key`) and accept only supported persisted
      roles (`Cave` `View`/`Manager`, account `User` `Admin`); account provisioning selects the User-domain `Admin`
      even when a conflicting Cave-domain `Admin` row exists.
- [x] `PermissionPolicy` is the executable source of truth for account role inheritance, the account-scoped persisted
      role vocabulary used by account management, resource-scope bypass classification, and effective client-role
      projection; RequestUser, proposal access, Settings selectors, and session projection consume it, while contract
      tests keep the SQL `UserCavePermissions` view and client projection version aligned. The browser uses exact
      membership for current projections and a version-gated legacy adapter only for independently deployed older API
      versions.
- [x] Permission-definition identity is unique within (`PermissionType`, `Key`); migration preserves identical textual
      keys across different domains and fails closed with an explicit migration error if legacy same-domain duplicates
      must be repaired before the unique invariant can be installed.
- [x] Account membership revocation and ordinary Cave/User permission grants serialize on the same `AccountUser` row:
      grant transactions use `FOR KEY SHARE`, revocation uses `FOR UPDATE`, and both lock orderings are proven against
      real PostgreSQL so no account-scoped permission can survive or be created after membership removal.
- [x] Automatic and first-available County Cave Number allocation uses all account+County Caves, including hidden and
      archived Caves, while `IgnoreQueryFilters()` remains account-qualified and cannot observe another Account's numbers.
- [x] View-only creation proposals cannot select manual or first-available County-number modes; both Manager-only
      allocation controls are enforced server-side, and View proposals use automatic-next allocation.
- [x] Create approval atomically creates the Cave + first revision + approved request, and grants submitter Cave View
      only when the submitter is still an Account member.
- [x] Revoking the submitter's last effective Cave View capability removes pre-publication create-request access; a
      later approval may still publish the Cave and restores Cave View only for a submitter who remains an Account member.
- [x] Removing the submitter from the Account does not block later approval and does not recreate a CavePermission row.
- [x] Submitter destination changes reroute the review queue across County/State scopes immediately.
- [x] Two independent create proposals that preview the same County number serialize publication and receive distinct
      authoritative County numbers, with each first revision recording its allocated value.
- [x] The complete authoring contract publishes equivalently through direct Manager create and View-proposal approval.
- [x] Create rejection creates no Cave, revision, or Cave-View grant.
- [x] Create request-changes/revision/resubmit preserves null-base immutable proposal history.
- [x] Initial proposal can stage Files before request creation.
- [x] Proposal versions preserve exact File identity/intents.
- [x] Proposal versions preserve exact proposed line-plot GeoJSON.
- [x] New proposal versions do not mutate old versions.
- [x] File-only and line-plot-only proposals are valid.
- [x] Empty proposal versions are rejected.
- [x] Approval publishes the exact selected immutable proposal version.
- [x] Rejection creates no published revision.

- [x] Downgrade from the Cave Revisions completion migration to the `main` v32 boundary fails closed with an explicit
      `SupportNewCaveProposals` compatibility error while a creation proposal version has null `BaseRevisionId`;
      the failed downgrade leaves the feature schema, migration history, and proposal data unchanged.

### Architecture/security/compatibility
- [x] Services/workflow coordinators do not acquire direct DbContext/Npgsql ownership.
- [x] Every changed `IgnoreQueryFilters()` path remains explicitly tenant-qualified.
- [x] People-only free-form tag and string-enum invariants remain unchanged.
- [x] Imports remain set-oriented/bounded; no interactive per-Cave import loop was introduced.
- [x] Snapshot query growth remains bounded for supported import scale.
- [x] Archive still represents current state without unexpectedly exporting historical/staged payloads.
- [x] Hard deletion remains blocked for unresolved proposals.
- [x] Ordinary File removal and explicit hard deletion both preserve commit-before-external-delete ordering; ordinary
      File removal uses durable `CaveAssetRemoval` debt and line-plot removal deletes `CaveGeoJson` transactionally.
- [x] No PostGIS/MVT line-plot redesign leaked into this task.
- [x] Durable docs describe the implemented truth and no longer contradict the new File lifecycle.

### Validation/reporting

- [x] All changed/new focused unit tests pass.
- [x] All changed/new focused integration tests pass.
- [x] All changed/new focused frontend tests pass.
- [x] Directly impacted backend build passes.
- [x] Frontend build passes.
- [x] Required migration tests and pending-model check pass.
- [x] Conditional import/archive checks were run if actually applicable.
- [x] `git diff --check` passes.
- [x] Final adversarial diff review finds no remaining mutation bypass or stale-write path.

## 15. Required final report

The implementing agent's final response must list:

- exact files/major areas changed;
- exact test/build commands run and whether each passed;
- any planned command not run and the concrete reason;
- confirmation that the required storage-cleanup migration/data-conversion tests and pending-model check ran;
- any known remaining limitation, especially the intentionally deferred line-plot storage/versioning redesign;
- confirmation that no unrelated full-suite validation was claimed or silently substituted.
