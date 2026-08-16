# Focused testing and completion validation

This task is broad in behavior but should not trigger indiscriminate repository validation. Follow `docs/testing.md` and
run the narrowest tests that prove the changed contracts. Expand only when an actual dependency or shared contract makes
it necessary.

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
17. Removing a previously published File creates exactly one tenant-qualified `RetainedCaveFileObject` and does not call
    physical object delete.
18. That ordinary removal deletes the live `File` row, so its current `FileTypeTagId` relationship no longer blocks later
    TagType deletion/merge semantics; historical snapshot metadata remains readable.
19. A repeated retention attempt for the same `(AccountId, FileId)` accepts only the same Cave/object address and rejects
    an inconsistent repoint.
20. Retained-object rows are not returned by ordinary current-File or Cave-revision APIs and cannot be guessed across
    accounts.
21. Expired never-published staged File cleanup deletes exactly that object and creates no retention row.
22. Cross-account staged File cannot be attached/published.
23. Unbound staged File read/delete is limited to its staging actor/account policy; guessed foreign IDs do not leak data.
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
39. A removed published File keeps its stable snapshot File ID while its private object address lives only in the
    tenant-qualified `RetainedCaveFileObject` table; the live `File` row is gone.
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

This plan intentionally adds `RetainedCaveFileObject`, so a model/schema migration is required. Run the migration-focused
integration tests that prove a pristine database and the supported main baseline upgrade to the latest schema:

```bash
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj \
  --configuration Release --no-restore \
  --filter 'FullyQualifiedName~MigrationUpgradeIntegrationTests|FullyQualifiedName~PostgresFoundationIntegrationTests'
```

If the migration adds no special data backfill, do not invent a historical-data migration test unrelated to its shape;
the existing empty/latest and exact-main upgrade contracts plus focused retained-object schema tests are the relevant
coverage. Add a dedicated migration test only if the migration itself contains data transformation logic.

Then run the repository-required pending-model check:

```bash
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project Planarian/Planarian.Migrations/Planarian.Migrations.csproj \
  --startup-project Planarian/Planarian.Migrations/Planarian.Migrations.csproj \
  --context PlanarianDbContext --configuration Release --no-build
```

Do not create an additional migration merely for legacy `BlobKey`/`BlobContainer` naming; the required migration is for
the new retained-object table/indexes.

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
9. inspect the final diff against `feature/cave-revisions` for the bypass/concurrency/tenant issues listed in
   `implementation.md` Phase 10;
10. search for obsolete direct File/GeoJSON publication and staged blob-copy call sites;
11. confirm durable docs match implemented behavior.

Do not convert this focused sequence into a full repository suite unless a failure or changed shared contract gives a
specific reason.

## 14. Final acceptance checklist

Do not mark the task complete until every applicable item is true:

### Concurrency and aggregate publication

- [ ] Existing direct edits submit the client-loaded `ExpectedRevisionId`.
- [ ] Missing/stale expected revision cannot silently publish over newer Cave state.
- [ ] Stale direct edit returns explicit 409/conflict UX.
- [ ] One aggregate direct edit creates at most one `CaveRevision`.
- [ ] One aggregate approved proposal creates at most one `CaveRevision`.
- [ ] Complete semantic no-op creates no published revision and no proposal version.

### File authoring and storage

- [ ] Cave editor can stage/add, edit metadata, and remove Files.
- [ ] Cave detail page is read-only for published File mutation.
- [ ] A new staged Cave File is uploaded once to one immutable object identity.
- [ ] Publication associates the same File/object; no staging→published byte copy remains.
- [ ] Failed publication leaves the staged File recoverable/expiring and Cave unchanged.
- [ ] Ordinary removal of a once-published File creates/verifies `RetainedCaveFileObject`, deletes the live `File` row,
      and does not physically delete its object.
- [ ] `RetainedCaveFileObject` has no File/Cave/TagType FK and is unique by tenant + historical File ID.
- [ ] Removed historical File content therefore does not keep a live FileTypeTag relationship in use.
- [ ] Expired never-published staging is physically cleaned up without creating retention rows.
- [ ] Affected application logic depends on provider-neutral storage types, not Azure SDK types.
- [ ] Published File snapshot does not expose object-storage locators; stable File ID resolves retained content server-side.
- [ ] Cross-account/unowned staged File attachment is rejected.

### Line plots

- [ ] Cave editor can add, rename, replace, and remove line plots.
- [ ] Existing line-plot IDs remain stable across rename/content replacement.
- [ ] Cave detail page cannot directly persist GeoJSON.
- [ ] No standalone published GeoJSON mutation path bypasses Cave revisions.
- [ ] Current `CaveGeoJson` `jsonb` storage remains in place.
- [ ] Current map line-plot retrieval/rendering remains in place and functional.
- [ ] Published snapshot contains line-plot ID/name/content hash, not raw GeoJSON.
- [ ] Line-plot Added/Removed/Renamed/ContentChanged diffs are readable and deterministic.
- [ ] No GeoJSON recovery table/blob/rollback system was added.

### Proposals

- [ ] View permission remains sufficient to author a proposal.
- [ ] Initial proposal can stage Files before request creation.
- [ ] Proposal versions preserve exact File identity/intents.
- [ ] Proposal versions preserve exact proposed line-plot GeoJSON.
- [ ] New proposal versions do not mutate old versions.
- [ ] File-only and line-plot-only proposals are valid.
- [ ] Empty proposal versions are rejected.
- [ ] Approval publishes the exact selected immutable proposal version.
- [ ] Rejection creates no published revision.

### Architecture/security/compatibility
- [ ] Services/workflow coordinators do not acquire direct DbContext/Npgsql ownership.
- [ ] Every changed `IgnoreQueryFilters()` path remains explicitly tenant-qualified.
- [ ] People-only free-form tag and string-enum invariants remain unchanged.
- [ ] Imports remain set-oriented/bounded; no interactive per-Cave import loop was introduced.
- [ ] Snapshot query growth remains bounded for supported import scale.
- [ ] Archive still represents current state without unexpectedly exporting historical/staged payloads.
- [ ] Hard deletion remains blocked for unresolved proposals.
- [ ] Explicit Cave/account hard purge removes retained-object rows and physically deletes their de-duplicated objects;
      ordinary revisioned removal does not.
- [ ] No PostGIS/MVT line-plot redesign leaked into this task.
- [ ] Durable docs describe the implemented truth and no longer contradict the new File lifecycle.

### Validation/reporting

- [ ] All changed/new focused unit tests pass.
- [ ] All changed/new focused integration tests pass.
- [ ] All changed/new focused frontend tests pass.
- [ ] Directly impacted backend build passes.
- [ ] Frontend build passes.
- [ ] Required migration tests and pending-model check pass.
- [ ] Conditional import/archive checks were run if actually applicable.
- [ ] `git diff --check` passes.
- [ ] Final adversarial diff review finds no remaining mutation bypass or stale-write path.

## 15. Required final report

The implementing agent's final response must list:

- exact files/major areas changed;
- exact test/build commands run and whether each passed;
- any planned command not run and the concrete reason;
- confirmation that the required retained-object migration tests and pending-model check ran;
- any known remaining limitation, especially the intentionally deferred line-plot storage/versioning redesign;
- confirmation that no unrelated full-suite validation was claimed or silently substituted.
