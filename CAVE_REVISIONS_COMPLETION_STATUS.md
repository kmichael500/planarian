# Cave Revisions Completion Status and Validation Ledger

**Branch:** `feature/cave-revisions-completion`
**Status:** **ACTIVE PRE-MERGE — VALIDATION REMAINING**
**Ledger last refreshed:** 2026-10-26
**Completion HEAD reviewed:** `8a2c92ab7d6fde4f8a7fd4b784273daae63eb298`
**Package refresh:** 2026-10-26; latest reviewed code package is shared-reference revision administration `8a2c92ab7d6fde4f8a7fd4b784273daae63eb298`; earlier dead-code, preservation, and asset-lifecycle evidence remains SHA-pinned below.
**Current `main` reviewed:** `e342691c82f1bb6549be903d3a8f94a5d2560ac2`
**Migration validation refreshed:** 2026-10-26; the final current-main preservation fixture passes the migration + PostgreSQL-foundation suite **34/34** in the current working tree
**Rollout-baseline validation refreshed:** 2026-10-26; the preservation census separately validates rollout against the complete v32 fixture; earlier merge-reconciliation evidence remains recorded below
**Merge base:** current `main` above
**Branch distance at baseline review:** 170 commits ahead of `main`, 0 behind
**Three-dot diff at baseline review:** 481 files changed, 67,926 insertions, 5,989 deletions

## Purpose

This is the durable handoff/status ledger for the Cave revisions completion branch. It records what is implemented, what has actually been validated, what remains only partially validated, concrete defects found during adversarial review, and high-value validation still needed before merge.

This file is intentionally broader than the implementation plan. It includes findings from the post-implementation architecture/code-quality/test-quality review and the later destructive/data-loss risk review. Another agent should be able to resume from this file without depending on chat history.

Do not treat an item as validated merely because the implementation exists or because a related test is green. The status below distinguishes implementation from evidence.

## Authority and related documents

Follow the repository workflow router and project rules first:

- `PROJECT_PROTOCOLS.md`
- `AGENTS.md`
- `Planarian/AGENTS.md` for backend/persistence changes
- `docs/data-access-architecture.md`
- `docs/testing.md`
- `docs/cave-revisions.md`
- `docs/plans/cave-revisions-completion/README.md`
- `docs/plans/cave-revisions-completion/architecture.md`
- `docs/plans/cave-revisions-completion/implementation.md`
- `docs/plans/cave-revisions-completion/validation.md`

The completion-plan README is still marked **active pre-merge implementation plan**, so its target-state requirements still apply.

## Status legend

- **DONE + VALIDATED** — implemented and supported by direct review/test evidence.
- **DONE, PARTIAL VALIDATION** — implementation appears complete, but one or more important scenarios remain unproven.
- **MOSTLY DONE** — implementation is substantially present but a known path still contradicts the target state.
- **BLOCKER** — should be fixed before merge.
- **VALIDATION GAP** — no known implementation defect yet, but risk is material enough to require explicit proof before merge.
- **FOLLOW-UP** — useful hardening that may be post-merge only if the merge gate explicitly accepts the risk.

# Executive assessment

The branch is **substantially built**. The core revision/proposal/file/import architecture is coherent and considerably stronger than the size of the diff suggests. This is not a branch that needs a broad redesign.

No known Cave-revision implementation blocker remains in the current working tree. The former Cave-detail line-plot mutation surface has been removed, and the Reset Account destructive-safety package is merged into the completion branch.

The **P0 implementation/validation checklist is now complete in this package**. GAP-001 adds a strict current-main v32 database preservation census plus a separate revision-rollout preservation boundary; the package still needs to be included in the branch's final adversarial validation pass.

The existing test suite is strongest around transactionality, concurrency, tenant isolation, import behavior, revision/proposal publication, destructive Reset behavior, authoring-field preservation, and current-main migration/rollout preservation. The remaining high-value risks are P1/P2 work outside the core publication machinery: production object-storage behavior, shared-interceptor compatibility, real-browser integration, archive generation/runtime behavior, account-switch cache isolation, and mixed-version deployment safety.

## Reviewed scope size

The reviewed three-dot diff contained approximately:

- 82 Cave production files;
- 22 import files;
- 17 File-domain files;
- 23 account-domain files;
- 23 migration-related files;
- 117 frontend files;
- 25 development/tooling files;
- 9 documentation files.

Changed test areas included approximately:

- 41 Cave revision/proposal files;
- 25 import files;
- 34 integration-test infrastructure files;
- 38 frontend test files;
- 11 account/admin test files;
- 2 migration-test files.

These counts explain why the review treated this as a shared-system change rather than only a Cave UI feature.

# 1. Feature implementation status

| Area | Status | Evidence / remaining work |
| --- | --- | --- |
| Cave revision entity/model and rollout history boundary | **DONE + VALIDATED** | Existing Caves are initialized in a deliberate bounded rollout step before the API serves requests; normal runtime fails closed instead of creating a late baseline. PostgreSQL coverage includes multi-account initialization, idempotence, resume with one preserved boundary timestamp, late-baseline rejection, and migration-to-rollout behavior. |
| Direct manager edit optimistic concurrency | **DONE + VALIDATED** | Existing Cave edits require client-loaded `ExpectedRevisionId`; stale edits conflict instead of silently overwriting. Add explicit missing-ID regression if not already present as noted below. |
| Semantic no-op suppression | **DONE + VALIDATED** | Snapshot comparison avoids published revisions/proposal versions for semantically unchanged aggregate state. |
| Revision history query + diff UI | **DONE + VALIDATED** | Unit/frontend coverage exists for nested diffs, line-plot presentation, historical labels, false/zero preservation, etc. |
| Suggest Cave Changes flow | **DONE + VALIDATED** | Immutable proposal-version architecture and authoring flow have broad integration/frontend coverage. |
| Review / approve / reject / request-changes / resubmit | **DONE + VALIDATED** | Includes stale base/proposal concurrency, review-event history, rejection/no-publication behavior, and `xmin` race coverage. |
| Proposal version immutability | **DONE + VALIDATED** | Proposal versions freeze exact proposed state including assets; later mutable state does not redefine older proposal meaning. |
| File authoring in aggregate Cave editor | **DONE + VALIDATED** | Stage/add/edit/remove flows and unsaved-change behavior have frontend/integration coverage. |
| Upload-once staged File lifecycle | **DONE + VALIDATED** | New content is written once; publication associates the existing object rather than copying to a new published key. |
| Published Cave asset removal/history | **DONE + VALIDATED** | Files and line plots now share the same semantic contract: revision history retains lightweight identity/metadata while removed payloads are not deliberately retained as historical Cave content. File bytes use durable post-commit `CaveAssetRemoval` cleanup; `CaveGeoJson` deletion is transactional. |
| Provider-neutral object storage abstraction | **DONE, PARTIAL VALIDATION** | Application logic uses `IObjectStorage` for the affected lifecycle. Service behavior is heavily tested with a fake, and a manual local Azurite smoke validated create-only collision handling plus snapshot-aware deletion. A committed automated Azurite contract suite is still missing. |
| Line-plot aggregate authoring | **DONE + VALIDATED** | `CaveLinePlotAuthoringEditor` is the normal interactive authoring surface. The obsolete Cave-detail `CaveSpatialDataImport` / `GeoJsonSaveModal` path is removed; focused frontend coverage proves aggregate authoring and map rendering still work. |
| Line plots in revision/proposal semantics | **DONE + VALIDATED** | Stable IDs, normalized content hash, metadata diffs, proposal exact GeoJSON payloads, deterministic snapshot behavior covered. |
| Cave hard deletion / proposal blocking | **DONE + VALIDATED** | Core Cave deletion and unresolved-proposal blocking have dedicated integration coverage; hard-delete provider-failure retry remains tracked separately below. |
| Bulk Cave/Entrance import rewrite | **DONE + VALIDATED** | Immutable plan/execute flow, preview/commit equivalence, dry run, rollback, tenant adversarial behavior, golden compatibility, and supported-scale test passed. |
| Import performance architecture | **DONE + VALIDATED** | 10,000-Cave/Entrance workload passed; set-oriented/bounded architecture reviewed. |
| Tenant isolation | **DONE + VALIDATED** | Fail-closed workflow filters, explicit account qualification when bypassing filters, adversarial foreign-account tests. |
| Tag/County administration revision interaction | **DONE + VALIDATED** | Rename/merge behavior has direct rollback, tenant-isolation, proposal-staleness, no-op, lock-order/writer-interleaving, 13-family/16-path Tag coverage, historical duplicate-preservation regression coverage, late multi-batch rollback, and 10,000-Cave rename + Tag/County merge scale coverage. |
| Account settings atomicity / state constraints | **DONE + VALIDATED** | Integration tests prove atomic settings/state update and rollback on invalid state removal. |
| File Name + Extension model | **DONE + VALIDATED** | Completion extends unchanged current-main v32 with a clean post-v32 migration. PostgreSQL coverage proves representative v32 data upgrades safely, preserves distinct legacy `FileName` values across an untouched upgrade/downgrade round trip, respects post-upgrade renames, and restores the v32 `FileName` schema contract without an accidental default. |
| File/PDF/vector read behavior | **DONE, PARTIAL VALIDATION** | Strong unit/component coverage exists. No real-browser PDF.js → custom header → CORS → Range → authenticated API smoke test yet. |
| App/global permission semantics | **DONE + VALIDATED** | Platform-admin/account-admin/default-account/export capability integration coverage exists. |
| Global `SaveChangesInterceptor` batching refactor | **DONE, PARTIAL VALIDATION** | Command-count tests prove bounded lookups; broader entity-family operation/tenant matrix is still recommended because blast radius is repository-wide. |
| Account Reset rewrite | **DONE, PARTIAL VALIDATION** | The merged destructive-safety package tests the Reset graph, two-tenant noninterference, archive container removal, post-commit storage failures, immediate retry, and reset/archive admission. A transactional cleanup outbox, explicit retry endpoint/UI and PostgreSQL shared/exclusive account-maintenance leases cover API writes and background archives. Independent non-cooperating writers and live Azure smoke tests remain deployment validation boundaries. |
| Account archive projection compatibility | **DONE, PARTIAL VALIDATION** | Focused projection tests exist. Full `.tar.gz` generation/content/cancellation/pruning/restore semantics are under-tested. |
| Unsaved-change guard | **DONE + VALIDATED** | Edit/Suggest/Revise tests cover blocked navigation, keep-editing, discard, asset-only dirty state, and `beforeunload`. |
| Detail-page read-only Cave-owned state | **DONE + VALIDATED** | Cave detail exposes File viewing and the map but contains no direct File or line-plot publication path. Source review confirms the obsolete importer/modal wiring is gone; positive map and aggregate-authoring tests cover the behavior that remains supported. |
| Durable completion docs / final acceptance checklist | **DONE, PARTIAL VALIDATION** | Durable architecture/status docs are refreshed to current implementation truth. The plan remains intentionally marked active pre-merge until the remaining validation gates are completed. |

# 2. Architecture and code-quality assessment

## Architecture strengths

The strongest architectural result is the central **published Cave aggregate mutation boundary**.

`CaveMutationRepository` / `CaveMutationCoordinator` now provide a coherent model:

- outer workflow owns or joins a relational transaction deliberately;
- existing Cave mutation captures the expected revision and live-before state;
- concurrent/stale current-revision changes are detected;
- the resulting aggregate snapshot is built after mutation;
- semantic no-ops do not create history noise;
- publication writes the revision and advances the current pointer atomically;
- delete publication uses a tombstone snapshot captured before removal;
- tenant scope remains explicit even on deliberate `IgnoreQueryFilters()` paths.

The split between interactive publication and `CaveBulkRevisionRepository` is appropriate rather than accidental duplication. Interactive workflows need per-aggregate concurrency semantics; imports need set-oriented/batched behavior at supported scale.

The proposal architecture is also strong:

- proposal versions are immutable;
- each version is authored against an exact base revision;
- exact File intent and line-plot GeoJSON are frozen in the proposal version;
- approval reconstructs/publishes that immutable version;
- stale published-base and stale active-proposal conflicts are explicit;
- review events preserve workflow history;
- new logical line-plot IDs are allocated at a stable point in the proposal lifecycle.

File architecture is similarly coherent:

- provider-neutral object-storage boundary;
- upload once for authoring content;
- relational publication associates the existing File/object;
- ordinary historical removal retains private object coordinates separately from the live File row;
- external delete occurs after relational durability where atomic cross-system transactions are impossible.

Snapshot design is intentionally user/logical-state oriented:

- public revision snapshots do not expose storage partition/key;
- line plots store stable ID/name/content fingerprint rather than raw GeoJSON;
- TagType and County label administration publishes bounded `ManagerEdit` revision fan-out for affected Caves while preserving stable reference IDs and historical labels;
- batch snapshot projection remains bounded rather than N+1, including 10,000-Cave shared-reference rename, Tag merge, and County merge paths.

## Architecture weaknesses / validation boundaries still present

1. The global `SaveChangesInterceptor` is high-blast-radius infrastructure. The batching refactor looks sensible, but deserves broader compatibility proof outside the Cave-centric scenarios that naturally exercise it.
2. The production `AzureBlobObjectStorage` adapter has a passing manual local Azurite smoke for create-only collision handling, idempotent delete, and snapshot-aware deletion, but still lacks a committed automated Azurite contract suite; broader provider-level race/cancellation/content-metadata semantics remain a validation boundary.
3. Browser/runtime and full archive-generation behavior are still under-tested end to end. Reset Account now has durable cleanup debt/retry semantics, but independent non-cooperating writers and live Azure behavior remain deployment audit boundaries rather than known application-contract defects.

## Code quality

Backend code quality is generally **high**:

- persistence ownership is mostly kept in repositories;
- coordinator/services remain relatively thin around persistence boundaries;
- immutable records are used for plans/snapshots/proposals;
- concurrency and transaction ownership are explicit;
- bulk work is batched;
- cancellation is propagated through serious database work;
- unusual behavior is generally commented with intent;
- no broad generic CRUD/unit-of-work abstraction was introduced.

Frontend code quality is **good new code inside an older/noisier frontend**:

- authoring components are reasonably decomposed;
- mapping/diff helpers are separated;
- revision/proposal pages are not a single monolith;
- production build succeeds;
- there is still inherited lint/hook/API debt and a few changed-file warnings, but no systemic new frontend architecture problem was found.

A large service such as `CaveChangeRequestService` may eventually benefit from workflow-oriented splitting, but a pre-merge broad refactor would add more risk than value. Treat that as post-merge maintainability work unless a concrete bug requires it.

# 3. Test-quality / test-theatre assessment

Overall test theatre is **low**.

The strongest tests use real PostgreSQL/PostGIS and assert durable postconditions rather than merely mocked call order. The suite exercises:

- real EF migrations and constraints;
- transaction rollback after late injected failure;
- multiple `DbContext` instances for concurrency;
- `xmin`/revision races;
- tenant boundary attacks;
- immutable proposal publication;
- file retention/deletion semantics;
- dry-run persistence guarantees;
- preview/commit equivalence;
- large set-oriented imports.

Static review found:

- no changed tests intentionally skipped;
- no `.only`/equivalent focus left behind;
- no changed test files that contain tests but no obvious assertions;
- no conflict markers or material TODO/FIXME/HACK residue in changed human-authored files.

## Known test/runtime limitations

- The obsolete `GeoJsonSaveModal.test.ts` wiring test was removed with the detail-page mutation surface. No permanent negative test asserts that the old button remains absent; supported behavior is covered positively by aggregate-authoring and map tests.
- Frontend unit/component tests cannot prove real-browser behaviors such as cookies/CORS/preflight, PDF.js range traffic, CSP, hard refresh routing, or browser unload behavior end to end.

The principal remaining test risk is therefore **runtime integration coverage**, not fake assertions.

# 4. Validation already completed

The broad validation evidence below is preserved at the SHAs where it was actually collected rather than being silently relabeled as current. The latest reviewed completion code HEAD is `8a2c92ab7d6fde4f8a7fd4b784273daae63eb298`; the earlier dead-code and GAP-001 evidence remains pinned to the SHAs recorded in their respective refresh sections.

## 2026-10-26 shared-reference revision administration refresh

Implementation commit `8a2c92ab7d6fde4f8a7fd4b784273daae63eb298` changes shared County/Tag administration so an administrative label change that changes a serialized published Cave snapshot publishes one normal `ManagerEdit` revision per affected Cave in the same transaction. Stable County/Tag IDs remain unchanged; immutable prior revisions and proposal versions retain their historical labels. County/Tag merge semantics remain source-preserving, with explicit deletion still separate.

Adversarial validation for this package included:

- exact historical Tag-merge duplicate-preservation regression coverage across all 14 collection-backed relationship paths plus the scalar File Type and Location Quality paths;
- multi-source Tag and County merges, destination-preexisting relationships/Caves, source normalization/no-op behavior, foreign-account rejection, default/global Tag tenant isolation, proposal staleness, incomplete-rollout fail-closed behavior, and both writer-first/administration-first concurrency interleavings;
- deterministic failure on the second 1,000-revision publication chunk across 1,200 Caves, proving complete transaction rollback including exact pre-merge `CurrentRevisionId` restoration;
- 10,000-Cave County rename, Tag merge, and County merge scale tests with bounded command/tracking behavior and exactly one revision per affected Cave;
- focused final shared-reference merge safety package — **33/33 passed**;
- broad merge/concurrency/tenant/proposal matrix during adversarial review — **85/85 passed**;
- complete PostgreSQL/PostGIS integration suite on the exact final implementation tree — **438/438 passed**;
- unit/architecture suite — **415/415 passed**;
- Release solution build — **PASS, 0 errors**; a fresh post-merge target-worktree rebuild emitted **175 pre-existing nullable warnings** in unchanged code paths (the earlier incremental build emitted none);
- EF pending-model check — **PASS, no pending model changes**;
- repository documentation validator against current literal `main` `e342691c82f1bb6549be903d3a8f94a5d2560ac2` — **PASS**;
- `git diff --check`, staged-diff check, source-to-test Tag merge branch census, and stale shared-reference documentation scan — **PASS**.

The final review found no production data-loss defect. It did strengthen test assertions around same-parent multi-source collapse, scalar default-Tag tenant isolation, County incomplete-rollout failure, and exact multi-batch revision-pointer rollback. No frontend source changed in this package, so the previously recorded frontend validation remains the applicable frontend evidence.

## 2026-10-26 dead-code audit merge refresh

- Dead-code audit commit `c1e271a9` was merged into the current completion branch as `12a7794e`, on top of Cave asset-lifecycle commit `c5f4b9f3`.
- The only merge conflict was in `FileService.cs`; resolution preserved the newer `CleanupAssetRemovalTargetsAsync` lifecycle and removed only the audit-confirmed unused `DeleteBlobBestEffortAsync` and `EnsureFileManagerAccess` helpers.
- Release solution build — **PASS, 0 errors**; the repository's existing nullable/static-analysis warnings remain.
- Full backend unit suite — **415/415 passed**.
- Merge-conflict-sensitive Cave aggregate asset integration suite — **19/19 passed** on disposable PostgreSQL/PostGIS.
- Full frontend suite — **73 suites / 353 tests passed, 0 snapshots**; preflight client-route generator **6/6** and CSP configuration **5/5** also passed.
- EF pending-model validation — **PASS: no changes since the last migration**; the command still emits the repository's existing global-query-filter relationship warnings.
- Focused .NET dead-code analyzer set (`IDE0051`, `IDE0052`, `IDE0059`, `IDE0060`, `CA1811`, `CA1812`, `CA1823`) — **PASS, zero findings**.
- `git diff --check`, deleted-symbol reference scans, and the merged documentation diff — **PASS**.
- The full PostgreSQL/PostGIS suite was **not rerun specifically for this dead-code merge**. The prior **381/381** full-suite evidence remains pinned to the Cave asset lifecycle refresh below; the merge-specific backend conflict surface was rerun directly via the **19/19** asset suite above.

## 2026-10-26 focused completion refresh

- Earlier committed baseline `33191288` includes the merged Cave-authoring field-persistence package, Reset Account destructive-safety hardening, and the Cave-detail line-plot publication cleanup; GAP-001 was then committed as `08955058`.
- GAP-001 focused preservation validation passed **2/2** on disposable PostgreSQL/PostGIS: one test covers the complete 41-table current-main v32 migration boundary and one covers the separate revision-rollout boundary plus an idempotent no-op rerun.
- Adversarial test-quality review corrected the preservation class name so the repository's documented `FullyQualifiedName~Migration` filter discovers **both** GAP-001 tests; the temporary implementation-coupled rollout batch-count assertions were removed and FeatureSetting backfill provenance was strengthened for both generated rows.
- The final migration + PostgreSQL-foundation focused suite passed **34/34** with the complete census fixture and corrected test discovery.
- Release solution build passed **0 warnings / 0 errors** and the backend unit suite passed **415/415** during this review.
- The full PostgreSQL/PostGIS integration suite passed **368/368** before the final assertion-only strengthening that checks no extra `FeatureSettings` rows were inserted; the exact final source was then rebuilt and the affected preservation tests (**2/2**) plus the full migration/foundation group (**34/34**) were rerun green.
- EF pending-model validation reports **no changes since the last migration**; the command emits the repository's existing global-query-filter relationship warnings.
- The obsolete Cave-detail line-plot publication surface was removed without changing the aggregate editor or map read path.
- After removing the temporary negative regression, focused frontend validation passed **4 suites / 20 tests, 0 snapshots** across Cave detail, aggregate File/line-plot authoring, CaveMap, and `MapLinePlotLayer`.
- The prior full frontend run passed **75 suites / 356 tests, 0 snapshots** before that one temporary test was removed. Its preflight passed client-route generator **6/6** and CSP configuration **5/5**; the focused post-removal run revalidated the affected suites.
- The optimized production frontend build passed with the repository's existing CRA/ESLint/bundle-size warnings; its prebuild again passed client-route generator **6/6** and CSP configuration **5/5**.
- `python3 scripts/validate-repository-docs.py --base-sha e342691c82f1bb6549be903d3a8f94a5d2560ac2` passed after preserving the validator's machine-readable ledger SHA labels.

## 2026-10-26 Cave asset lifecycle alignment refresh

After the product decision changed from permanent removed-File byte retention to metadata-only historical evidence, the implementation and validation plan were updated before release so Files and line plots share the same published-history/removal semantics.

- Focused Cave/File/line-plot/proposal/tenant-safety integration matrix — **44/44 passed** on the final test source, including direct and proposal removals, approval rollback protection, mixed cleanup success/already-missing/failure handling, post-delete acknowledgement failure + idempotent retry, failed external cleanup/retry, stale cleanup protection for same-tenant and foreign-tenant live Files, failed-create compensation safety, and direct-save transaction ownership.
- Reset Account/import/maintenance integration suite — **24/24 passed**, including external cleanup failure/retry, cross-tenant safety, kitchen-sink reset, and proof that `CaveAssetRemoval` debt does not block unrelated writes.
- Cave revision/line-plot diff contract unit suite — **16/16 passed**.
- Full Release migration + PostgreSQL-foundation focused suite — **36/36 passed**, including conversion of pre-release retained File locators into `CaveAssetRemoval` cleanup debt, an intentional old-row-ID collision, and fail-closed downgrade with pending asset cleanup.
- Full backend unit suite — **415/415 passed**.
- Full Release PostgreSQL/PostGIS integration suite — **381/381 passed**.
- Live Azurite object-storage contract check — **PASS**: create-only upload rejects an existing immutable address without changing bytes, normal delete is idempotent, and base-blob cleanup succeeds with snapshots via `IncludeSnapshots`.
- EF pending-model check — **PASS: no pending model changes**.
- Release backend + integration-test build — **PASS, 0 errors**; a fresh rebuild still emits the repository's existing nullable/static-analysis warning set (154 warnings in the latest integration-project build).
- Optimized frontend production build — **PASS**; its prebuild passed client-route generator **6/6** and CSP configuration **5/5**, with the repository's existing CRA/ESLint/bundle-size warnings.
- `git diff --check` — **PASS**.
- Repository documentation validator — **PASS** for this refresh.

## Historical broad validation at `33ace6e7`

The following broad evidence was collected against completion HEAD `33ace6e7afb099751de0f9df977a92c5beb9aa9a` and current `main` `e342691c82f1bb6549be903d3a8f94a5d2560ac2`.

## 2026-10-25 rollout merge-reconciliation refresh

A second full validation pass was run against the resolved merge tree combining rollout commit `32c90ee43e0081f8105209d132cdcf20eef81679` with completion commit `fa4582ac93ebd36f8c8bfbb891ec713ce380b09c`, before creating the merge commit. Conflict resolution deliberately preserved the newer split migration-test suite, hardened FeatureSetting provenance migration, rollback-compatibility behavior, and migration documentation from the completion branch while layering the eager revision-history rollout on top.

- Release solution build — **PASS, 0 warnings / 0 errors**.
- Backend unit suite — **406 passed, 0 failed, 0 skipped**.
- Migration + PostgreSQL-foundation focused suite — **32 passed, 0 failed, 0 skipped**.
- Rollout/revision/import focused suite — **23 passed, 0 failed, 0 skipped**.
- Full PostgreSQL/PostGIS integration suite — **341 passed, 0 failed, 0 skipped**, including both 10,000-record scale workloads.
- Frontend suite — **71 suites / 328 tests passed, 0 snapshots**.
- Client-route generator — **6/6 passed**; CSP configuration — **5/5 passed**; `.env.example` mapping validation — **PASS**.
- Optimized frontend production build — **PASS**; only existing CRA/ESLint/bundle-size warnings were emitted.
- EF pending-model check — **PASS: no pending model changes**.
- Repository documentation validator — **PASS**; at that historical review point it warned that the then-recorded `Completion HEAD reviewed` SHA was older than the worktree HEAD.
- Staged/unstaged diff checks and conflict-marker scan — **PASS**.

This refresh supersedes the older test-count totals below for the reconciled rollout work. The older totals remain because they are SHA-pinned evidence from the earlier broad completion review.

## Git / static checks

- `git diff --check main...feature/cave-revisions-completion` — **PASS**.
- Merge base equals current reviewed `main`.
- Branch was 156 commits ahead / 0 behind current reviewed `main`.
- Local completion branch was also **21 commits ahead of `origin/feature/cave-revisions-completion`** at final ledger verification; the remote branch was stale relative to this worktree.
- Human-authored changed-file review found no conflict markers.
- No changed test skips/focus markers found.
- Static scan found no material TODO/FIXME/HACK/NotImplemented residue requiring a finding.
- Completion branch working tree was clean immediately before this ledger was added.

## Backend

- `dotnet restore Planarian/Planarian.sln` — **PASS**.
- `dotnet build Planarian/Planarian.sln --configuration Release --no-restore` — **PASS**, observed with 154 existing warnings / 0 errors.
- Backend unit suite — **406 passed, 0 failed, 0 skipped**.
- PostgreSQL/PostGIS integration suite — **327 passed, 0 failed, 0 skipped**.
- The integration run included real Testcontainers PostgreSQL/PostGIS and the 10,000-Cave/Entrance scale workload.

One disposable git-archive unit run initially failed only three tests that discover repository root from `.git`; initializing a disposable `.git` in the export resolved that environment artifact and the complete unit suite passed.

## Frontend

- `npm ci` — **PASS**.
- Frontend test suite — **71 suites passed, 326 tests passed, 0 snapshots**.
- Client-route generator tests — **6/6 passed**.
- CSP configuration tests — **5/5 passed**.
- `npm run build` — **PASS** with existing lint warnings.
- Changed Cave files still have some lint warnings such as missing hook dependencies / older equality style; none was demonstrated as a completion regression during review.

## EF/model

Using the migrations project as both target and startup project:

```bash
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project Planarian/Planarian.Migrations/Planarian.Migrations.csproj \
  --startup-project Planarian/Planarian.Migrations/Planarian.Migrations.csproj \
  --context PlanarianDbContext \
  --configuration Release
```

Result: **No changes have been made to the model since the last migration.**

The application project as startup project did not load `Microsoft.EntityFrameworkCore.Design`; the command above is the verified form and should be the documented command.

## Development infrastructure

- `docker compose config -q` — **PASS**.
- Shell syntax validation on the reviewed dev/scripts — **PASS**.
- Docker/Azurite/Postgres configuration reviewed; committed storage/database credentials are local emulator/development defaults, not production secrets.

## Dependency audit observation

A production dependency audit did not show the completion branch making the inherited advisory picture worse overall. This was informational, not treated as a feature acceptance gate.

Reviewed totals:

- current reviewed `main`: 96 advisories — 13 low, 40 moderate, 37 high, 6 critical;
- completion: 94 advisories — 12 low, 42 moderate, 34 high, 6 critical.

Direct vulnerable packages were materially the same: `axios`, `csv-parse`, `dompurify`, `maplibre-gl`, `react-router-dom`, `react-scripts`, and `uuid`; completion's upgraded `react-router-dom` reduced that direct advisory from high to moderate. The six critical advisories were inherited rather than introduced by this branch.

## Informational EF model warning to keep on the radar

The EF pending-model command emitted relationship warnings because `Cave` has a global query filter while it is the required end of several relationships (for example tag/Entrance/GeoJSON relationships). No completion-specific failure was demonstrated and this was not promoted to a blocker during review. Treat it as architectural debt worth revisiting separately, especially if future changes rely on required navigations plus filtered principals.

# 5. Migration validation

## VAL-001 — RESOLVED — completion cleanly extends current main v32

The supported migration chain keeps `20260831132210_v32` unchanged from `main`, then adds:

- `20261025150619_CaveRevisionsCompletion`;
- `20261025150758_BackfillEntranceOtherFeatureSetting`.

The structural migration includes the intentional data-preservation work that EF cannot infer safely: File tenant ownership is recovered fail-closed; Cave-owned Files whose non-null account disagrees with the owning Cave are rejected; legacy File metadata becomes `Name` + `Extension`; distinct legacy storage filenames that cannot be reconstructed from that pair are retained in a migration-owned rollback-compatibility table; and legacy reporter-user columns are intentionally removed. The separate Entrance Other backfill records the exact FeatureSetting rows it inserts in a migration-owned marker table so rollback removes only migration-created rows, including when a preexisting setting happens to use the deterministic candidate ID.

The migration tests are split by behavior (`MigrationUpgrade`, File metadata, File ownership, FeatureSetting backfill, legacy reporter cleanup, and legacy revision initialization) rather than one monolithic class. The required migration + PostgreSQL-foundation Release suite passes **32/32**. Coverage includes a normalized PostgreSQL catalog comparison proving v32 columns/types/nullability/defaults/constraints/indexes round-trip unchanged (physical column order intentionally excluded), rollback after the new revision/proposal/import tables contain valid data, distinct legacy `FileName` preservation, post-upgrade Name and Extension edits, Files created after upgrade, migration length/validation boundaries, the complete supported File ownership matrix with transactional rollback, and exact FeatureSetting backfill provenance. `dotnet-ef migrations has-pending-model-changes` also reports no pending model changes.

After the schema migration, a separate one-time application bootstrap creates the authoritative `SystemBaseline/Baseline` revision for every pre-existing Cave from the canonical snapshot projection and advances `CurrentRevisionId`. Normal API startup refuses to serve while any live Cave remains uninitialized. The rollout is resumable, reuses one history-boundary timestamp across retries, has no user actor, refuses to create a late baseline after ordinary revision publication begins, and has supported-scale coverage for 10,000 Caves.

The rollback contract is intentionally **schema-contract/data-compatible**, not a promise to reconstruct deliberately removed legacy `ReportedByUserId` values. Those columns were unused legacy data and are recreated nullable on downgrade without restoring their former contents. Migration chronology and the supported rollback contract are therefore no longer merge blockers.

# 6. Resolved implementation defect

## VAL-002 — RESOLVED — Cave detail is read-only for line-plot publication

The obsolete detail-page publication surface has been removed from the current working tree:

- `CaveComponent` no longer renders `CaveSpatialDataImport`;
- `CaveSpatialDataImport.tsx`, `GeoJsonSaveModal.tsx`, and the wiring test that codified direct publication are removed;
- the detail-only `lineworkRefreshToken`/`updateCave` refresh plumbing is removed;
- `CaveMap` continues to render `MapLinePlotLayer` and refresh linework on viewport movement;
- `CaveLinePlotAuthoringEditor` remains the aggregate add/rename/replace/remove surface.

Focused frontend validation on 2026-10-26 passed **4 suites / 20 tests** across `CaveComponent.polish.test.tsx`, `CaveAssetAuthoringEditors.test.tsx`, `CaveMap.test.tsx`, and `MapLinePlotLayer.test.tsx` after the temporary negative assertion for the removed `Import Line Plot` button was dropped as low-value coverage. Source review proves the obsolete path is gone, while the retained map/editor tests prove current line-plot retrieval/rendering and aggregate authoring remain active.

# 7. Validation/documentation drift

## VAL-003 — RESOLVED — migration validation documentation is current

Migration validation now uses the exact current-main baseline for deployment-safety claims, and the durable validation documentation uses the verified migrations-project startup command.


# 8. Destructive and data-loss risk review

The core publication tests are strong. Remaining safety work should focus on operations where failure may leave a durable result that differs from what the user thinks happened.

## RISK-004 — RESOLVED APPLICATION CONTRACT — deployment validation remains

Completion correctly changes Reset Account so the PostgreSQL purge commits before external storage cleanup.

Current sequence:

1. `Repository.ExecuteResetAsync()` performs and commits the relational reset.
2. provider-neutral File objects are cleaned up best-effort.
3. the legacy account storage container is removed.

The merged destructive-safety package adds a typed API result distinguishing committed
relational deletion from best-effort external cleanup. A pre-commit storage locator
preflight also rejects direct foreign-account containers and shared cross-tenant
objects before relational deletion, without touching either tenant. It tests container-delete failure
*after* commit, a safe immediate retry, provider-neutral object failure reporting and
two-tenant isolation. A failed delete is no longer reported as if the PostgreSQL
transaction had rolled back. The UI and SignalR progress surface the partial result.

The merged destructive-safety package persists cleanup objects and the account
container target transactionally, supports an idempotent admin retry endpoint and
UI, and blocks account mutations until cleanup is complete. PostgreSQL
cross-instance advisory shared/exclusive leases cover ordinary mutating HTTP
requests, reset and background archive jobs. An additional preflight rejects
foreign references to any bytes in the account container.

**Deployment validation still needed:** no live Azure lifecycle smoke and no committed automated Azurite contract suite yet; the manual local Azurite smoke covers only the affected immutable-create/delete semantics. Independent actors that bypass HTTP and the archive coordinator are
not automatically fenced; an unrelated other-tenant writer could create an
unsafe foreign reference after the last pre-delete check. Do not represent a
passing disposable-Postgres/fake-object-storage suite as exhaustive proof.

## RISK-005 — Reset also removes server-side account archives

`ArchiveJobCoordinator` writes account archives into `RequestUser.AccountContainerName` under `archives/...`.

`ResetAccount()` removes that account container. A previously generated Recent Archive therefore disappears during reset.

This whole-container behavior also exists on reviewed `main`; completion did not introduce the underlying behavior. Because completion substantially rewrites reset/storage lifecycle logic, this is the right point to make the contract explicit.

**Resolved product decision:** Reset intentionally removes stored account archives,
including temporary archives. The UI warns to download copies first. Tests verify
Account A archives disappear and Account B's remain. Archives are not complete
revision/proposal disaster-recovery backups.

Trip-photo storage was traced separately. Trip photos use `BlobService` and paths such as `projects/{projectId}/trips/{tripId}/photos/{photoId}` in its configured container. No evidence was found that the Reset Account container operation targets that BlobService container. Do not claim Trip photos are affected without new evidence.

# 9. High-value validation still missing

These items focus on unexpected data loss, unrelated regressions, recovery, and production/browser behavior rather than adding redundant Cave-publication tests.

## GAP-001 — RESOLVED — Full current-main database preservation census

The current working tree now contains a strict two-phase PostgreSQL/PostGIS preservation gate rather than another representative single-Cave migration test.

`CurrentMainV32CompletionMigrationPreservesCompleteDurableDatabase` builds the exact current-main v32 schema, asserts that its public durable-table manifest is exactly **41 tables**, seeds **every one of those 41 tables**, and fails if any table remains empty. The fixture includes three account shapes; normal, temporary/invited, confirmed and cross-account users; account/global permissions; all CavePermission scopes; default/non-default account and project TagTypes; rich/minimal/archived Caves; 3D and zero-valued Entrances; every Cave/Entrance tag relationship; Cave-owned/null-owner/account-only/expiring Files; line plots; Favorites; Project/Trip/Member/Photo/Lead data; message/email history; throttle logs; and fixed audit metadata.

Before and after migration, all **37 v32 tables that are not intentionally transformed** are compared as canonical whole-row JSON including PostgreSQL `xmin`, so an invisible rewrite is observable rather than reduced to a row-count check. `Caves`, `Entrances`, `Files`, and `FeatureSettings` use explicit transformation-aware projections. The test separately proves the expected File owner/name/extension conversions, FeatureSetting backfill/provenance, migration-owned filename compatibility rows, intentional reporter-column removal without deleting users/People tags, preservation of the `UserCavePermissions` view result, empty completion-owned revision/proposal tables before rollout, and correct migration history.

`RevisionRolloutOnlyCreatesBaselineHistoryAndAdvancesCavePointers` then treats rollout as a separate deployment boundary. Every non-`Caves`/non-`CaveRevisions` table is captured including `xmin` and must remain exactly unchanged. Cave user/audit state must remain unchanged apart from the deliberate `CurrentRevisionId`/row-version advance. The three generated `SystemBaseline/Baseline` revisions are compared against explicit full expected snapshots covering scalar state, references, every tag family, Entrances, Files and line-plot content hashes. A second rollout is required to be a complete database no-op, including `xmin`.

Validation on 2026-10-26: the two new focused tests passed **2/2** on disposable repository-owned PostgreSQL/PostGIS; after correcting documented migration-filter discovery, the migration + PostgreSQL-foundation suite passed **34/34** on the final fixture; and EF reported **“No changes have been made to the model since the last migration.”** No existing development/staging/production database was inspected or modified.

An optional higher-confidence predeployment extension remains: restore a sanitized real-world database copy into an isolated disposable PostgreSQL instance and run the same invariant approach. That is additional deployment evidence, not a remaining GAP-001 requirement, and does not authorize access to a shared/live database.

## GAP-002 — RESOLVED — Current-main rollback/deployment contract

The current-main-v32 regressions prove upgrade to completion and downgrade to the v32 schema contract, including migration history, File metadata behavior, absence of an accidental `FileName` default, removal of the migration-only filename compatibility table on downgrade, and fail-closed Cave/File account ownership validation. Deliberately removed legacy `ReportedByUserId` values are not part of the reversible data contract.

## GAP-003 — RESOLVED — Kitchen-sink Reset Account test

The merged destructive-safety package includes a two-tenant integration scenario covering all 14 Cave/Entrance tag relationships, Files (staged, temporary, published and pending asset-cleanup debt), line plots, Favorites, revisions, proposal versions, review events, Counties, custom TagTypes, AccountStates, permissions, and stored archives. Successful Reset removes the intended Account A graph, preserves the intentionally retained account identity/members/feature settings, leaves Account B's graph and blobs unchanged, and is safe to retry. Broader independent-writer/live-provider boundaries remain tracked under RISK-004 rather than weakening this graph test.

## GAP-004 — PARTIALLY RESOLVED — External-cleanup fault injection

**Priority: P1 for the remaining workflows**

Reset Account now has direct post-commit provider-failure coverage, truthful partial-success semantics, persisted cleanup debt, and retry behavior. Ordinary published File removal now also has direct provider-failure coverage proving the Cave revision/File-row mutation commits first, `CaveAssetRemoval` debt survives failure, unrelated writes are not blocked, and retry deletes the object/target. Equivalent failure-injection evidence is still needed where applicable for:

- hard Cave removal;
- expired staged-file cleanup;
- proposal cleanup;
- archive pruning/partial archive cleanup.

Those remaining tests should prove relational truth is not falsified by a later storage failure and that cleanup debt is safe/retryable.

## GAP-005 — Automated AzureBlobObjectStorage contract suite against Azurite

**Priority: P1**

Business-level File tests appropriately use `TestFileBlobStore : IObjectStorage`. A manual local Azurite smoke now validates the production adapter's create-only collision behavior, idempotent deletion, and deletion of a base blob with snapshots, but there is still no committed automated adapter contract suite.

The repository Docker environment already includes Azurite. Add an automated storage contract suite against real `AzureBlobObjectStorage` + Azurite covering:

- Put then OpenRead returns identical bytes;
- content type survives;
- ETag / last-modified are populated consistently;
- missing object returns the expected null/not-found semantics;
- remove existing object;
- remove missing object;
- concurrent first-use container creation;
- normalized partition/container behavior;
- object disappears between property lookup and stream opening;
- cancellation behavior where meaningful.

Also make the intended immutable-address assumption explicit. Either prove the application never reuses an immutable object key or make provider behavior reject accidental overwrite. Do not rely only on generated-ID improbability as protection against silent replacement.

## GAP-006 — Global SaveChangesInterceptor compatibility matrix

**Priority: P1**

The interceptor/batch ownership loader is repository-wide infrastructure. Existing command-count tests prove important performance properties but not the complete behavioral matrix.

Add parameterized/integration coverage across each entity family the interceptor explicitly validates:

- Added in same account → allowed;
- Modified in same account → allowed;
- Removed in same account → allowed;
- foreign-account Added/Modified/Removed → forbidden;
- failed attempt leaves both tenants unchanged;
- global/default entities retain intended special rules;
- registration/password-reset/invitation-cleanup exceptions remain narrow and intentional.

Particularly cover Cave/Entrance association entities whose ownership is now populated by reflection and batched FK lookup.

## GAP-007 — Small real-browser smoke suite

**Priority: P1**

No Playwright/Cypress-style browser suite was found in the reviewed completion branch.

Do not create hundreds of browser tests. Add a small set of high-value scenarios, ideally Chromium plus WebKit for the most important paths:

- authenticated bootstrap using real cookie behavior;
- frontend/API CORS and preflight;
- account switching;
- hard refresh on nested routes;
- browser back/forward around editor/proposal routes;
- unsaved-change browser navigation/unload behavior;
- authenticated File view/download;
- object-URL/download behavior where relevant;
- CSP does not block required application workers/assets;
- stale direct-edit conflict UX.

### PDF smoke scenario

The branch changed PDF streaming so PDF.js sends `X-Planarian-File-Stream-Session` rather than a query parameter.

Prove end to end:

**PDF.js → real browser → custom header → preflight/CORS if applicable → authenticated API → HTTP Range requests → PDF renders successfully.**

Unit tests already prove frontend request-option construction and backend throttling separately. This browser test proves their integration.

## GAP-008 — Mixed-version / stale-SPA deployment safety

**Priority: P1**

A user can keep an older SPA tab open while a new backend deploys.

The backend currently appears to do the safe thing for existing Cave edits: missing `ExpectedRevisionId` is rejected.

Add an explicit regression:

- send an old-client-shaped existing-Cave update without `ExpectedRevisionId`;
- expect controlled 400;
- assert Cave state, current revision, Files, line plots, and revision count remain unchanged.

Keep this invariant so a future compatibility change cannot silently restore last-write-wins behavior.

## GAP-009 — Ordinary read and authoring-context paths have no unintended writes

**Priority: P1**

Rollout baseline creation is deployment-only. Ordinary reads and authoring-context reads must remain read-only; a missing `CurrentRevisionId` on an existing Cave is an integrity error, not permission to manufacture history during a request.

Create a read-only contract test that fingerprints relevant database state, performs representative reads, and compares afterward:

- Cave detail;
- Cave list/search/advanced search;
- map/current line-plot retrieval;
- File view/download metadata path;
- revision/history read;
- other major ordinary GET flows touched by this branch.

Assert no timestamps, revision pointers, workflow rows, File rows, tag rows, or other durable state change. Authoring-context coverage should additionally prove that an uninitialized existing Cave fails closed rather than creating a revision as a request side effect.

## GAP-010 — Full archive generation/content validation

**Priority: P1**

Current archive tests mostly validate projections rather than a complete generated archive.

Add a test that calls `ExportService.WriteArchive()`, opens the resulting `.tar.gz`, and validates actual contents using difficult data:

- Unicode Cave/file names;
- filenames with multiple dots;
- path/name collision candidates;
- multiple Caves;
- line plots;
- zero-byte file;
- missing blob;
- ordinary binary/text/PDF files;
- enough Entrances/tags to exercise projections.

Validate:

- `caves.csv` and `entrances.csv` parse and match expected current state;
- expected file entries are byte-identical;
- missing-file manifest is correct;
- current line plots are represented as intended;
- staged/historical payload is not exported unless explicitly part of the contract;
- archive paths cannot escape the archive root.

Also test `ArchiveJobCoordinator`:

- cancellation cleans temporary archive;
- failure leaves no published corrupt archive;
- pruning retains the correct five;
- current/newest archive cannot be pruned accidentally;
- Account A cannot list/remove/download Account B archives;
- interrupted/stale temporary archive behavior is defined.

## GAP-011 — RESOLVED — Archive is a current-state export, not a disaster-recovery backup

The product/architecture decision is now explicit in `docs/architecture/destructive-operations.md`: the account Archive exports current Cave/Entrance state, current live Cave Files, current line plots, and the missing-file manifest where needed. It does not promise revision/proposal/review history or storage-cleanup-debt recovery, and no restore workflow is defined. Full generated-archive content/cancellation/pruning validation remains under GAP-010.

## GAP-012 — RESOLVED — Hidden/disabled feature fields preserve existing data

The cave-authoring field-persistence package merged before the current baseline. `AddCaveComponent.form-contract.test.tsx` proves pre-existing feature-gated Cave/Entrance/File/line-plot values remain in the submitted aggregate when controls are hidden. `EditCavePage.test.tsx` proves direct edit submits preserved unmounted values, and Suggest/Revise page tests prove preview/submission use the same complete draft rather than only mounted fields. The backend field-persistence matrix separately exercises full direct/proposal publication state.

## GAP-013 — Account switching and frontend cache isolation

**Priority: P1/P2**

Server-side tenant isolation is strong, but client caches can still show or submit stale account-specific reference/settings data.

Feature settings are keyed by account in the reviewed `AccountService`, which is good. Extend validation to the complete switch workflow:

- start in Account A;
- load feature settings/tags/counties/Cave authoring data;
- switch to Account B without a full reload;
- ensure Account A options/IDs are not actionable in Account B;
- switch back and verify correct reuse/invalidation;
- submissions after the switch always carry the current account context.

A real-browser test is preferable because context/provider/cache lifetime matters.

## GAP-014 — Abandoned authoring/staging lifecycle

**Priority: P1/P2**

Unpublished authoring Files expire after 10 days and cleanup currently runs opportunistically during later staging/temp uploads. Proposal-linked staged Files are deliberately protected from ordinary expiration cleanup.

Add lifecycle tests for:

- upload → close tab / never submit → eventually expires and is cleaned up;
- DB row cleanup followed by storage-provider failure leaves safe/retryable cleanup debt;
- proposal-linked staged File is not prematurely collected;
- rejected/resolved request cleanup follows the chosen retention policy;
- a published File has `ExpiresOn = null` and cannot be collected by staging cleanup;
- stale authoring rows are eventually collected even if that same user never uploads again, or explicitly document that cleanup is only opportunistic.

The final point may require an operations/product decision: opportunistic cleanup alone can leave stale storage indefinitely for inactive accounts.

## GAP-015 — File/object retry and race semantics

**Priority: P2 unless strict object immutability is chosen**

Make cross-system retry assumptions explicit for:

- repeated finalize/publish;
- simultaneous remove and publish;
- two requests attempting to attach the same staged File;
- retry after client timeout;
- duplicate HTTP submissions.

Existing proposal/File concurrency coverage is strong, but object-store idempotency assumptions deserve an explicit contract.

## GAP-016 — RESOLVED — Map/detail compatibility after old GeoJSON mutation removal

The obsolete detail mutation controls are removed without changing the map read path. Source review confirms `CaveComponent` still renders `CaveMap` for mapped Caves, `MapLinePlotLayer` still requests and renders current linework, and aggregate-authoring tests still cover line-plot changes. The temporary negative test for the removed button is intentionally not retained. No `CaveGeoJson` storage or map-service redesign was introduced.

# 10. Existing coverage that should not be duplicated unnecessarily

Do not spend pre-merge time multiplying variations of scenarios that already have strong evidence unless a fix changes their contract:

- stale direct revision conflicts;
- proposal base/proposal-version conflicts;
- proposal approval concurrency;
- semantic no-op revision suppression;
- immutable proposal versions;
- exact proposal File/line-plot intent;
- File-only/line-plot-only proposal validity;
- rejection creates no revision;
- tenant filters on revision/proposal workflow tables;
- cross-account staged File rejection;
- import dry-run persistence;
- import preview/commit equivalence;
- late import rollback;
- 10k import scale;
- removed File historical-metadata and post-commit cleanup semantics;
- File publication clearing expiration;
- staged File object availability checks;
- change-request review history;
- unsaved-change page navigation behaviors;
- PlanarianAdmin/account-admin permission separation and default-account capability resolution.

The branch already has meaningful evidence in these areas. Prefer tests that expand the risk surface instead of inflating counts.

The old `Planarian.Tests.EmailDelivery/Authentication/RequestThrottlePdfStreamTests.cs` file is removed on completion, but equivalent/newer PDF throttling coverage exists in `Planarian.Tests.Unit/Authentication/RequestThrottleServiceTests.cs`, `FileResponseSecurityTests.cs`, and `FileResultResponseTests.cs`. The removal was therefore not treated as lost coverage.

# 11. Required pre-merge work order

## P0 — Must resolve before merge

- [x] **VAL-001:** completion cleanly extends current-main v32, preserves intentional File data transformations, rejects cross-account Cave/File ownership, and has current-main upgrade/v32-schema-contract rollback coverage.
- [x] **VAL-002:** remove Cave-detail `CaveSpatialDataImport` / obsolete line-plot publication.
- [x] Remove/replace the `GeoJsonSaveModal` test that codified the obsolete mutation surface.
- [x] Prove current line-plot viewing/map retrieval still works after removal.
- [x] Add kitchen-sink destructive Reset Account test (merged destructive-safety package).
- [x] Add Reset Account post-commit external-storage failure test (merged destructive-safety package).
- [x] Decide, document, and test that reset intentionally removes stored account archives (merged destructive-safety package).
- [x] Persist failed provider-neutral storage cleanup targets for safe later retry (transactional outbox + endpoint/UI).
- [x] Add cross-replica account API mutation and archive coordination with PostgreSQL advisory leases (independent writers still need deployment audit).
- [x] Freeze/rename the v29 migration test so it is explicitly historical rather than “exact main”.
- [x] Refresh durable completion docs/checklists to current implementation truth. Keep the plan active until remaining validation gates are complete.
- [x] **GAP-001:** full current-main database preservation census, including a strict 41-table v32 migration boundary and separate rollout preservation boundary.

## P1 — Strongly recommended before merge because failures can be silent or cross-cutting

- [ ] Remaining external-cleanup fault injection for hard Cave/File/staging/archive workflows (GAP-004).
- [ ] Azurite contract tests for production `AzureBlobObjectStorage`.
- [ ] Global `SaveChangesInterceptor` operation/tenant compatibility matrix.
- [ ] Small real-browser smoke suite, including WebKit for critical workflows.
- [ ] Real-browser PDF.js custom-header/range/authentication test.
- [ ] Old-client/missing-`ExpectedRevisionId` no-mutation regression.
- [ ] Read-only GET/authoring-context no-side-effect contract plus fail-closed uninitialized-Cave coverage.
- [ ] End-to-end archive `.tar.gz` generation/content validation.
- [ ] Archive cancellation/failure/pruning/account-isolation tests.
- [x] Explicit archive-vs-backup product contract (current-state export; full archive runtime validation remains GAP-010).
- [x] Hidden/disabled feature-field preservation tests.
- [ ] Account-switch/client-cache isolation validation.

## P2 — Hardening that can be consciously deferred if documented

- [ ] Abandoned staging cleanup lifecycle / inactive-account orphan strategy.
- [ ] Object-store retry/race/idempotency hardening.
- [ ] Clean changed-file frontend lint warnings where low risk.
- [ ] Consider post-merge workflow-oriented split of very large services only if it materially improves maintenance.
- [ ] Broader browser coverage after the small critical smoke suite is established.

# 12. Recommended final validation sequence after fixes

After P0 implementation is complete:

1. Run narrow tests for each changed blocker/fix while developing.
2. Run all changed Cave revision unit tests.
3. Run all changed direct-edit/File/line-plot/proposal integration tests.
4. Run new destructive/reset/storage/migration-preservation tests.
5. Run changed frontend tests.
6. Run real-browser smoke tests if added.
7. Run `dotnet build Planarian/Planarian.sln --configuration Release --no-restore` because this branch reaches shared infrastructure beyond only the Cave graph.
8. Run the frontend production build.
9. Run EF pending-model validation with the verified migrations-project startup command.
10. Run current-main upgrade and v32 schema-contract rollback/deployment-policy tests.
11. Run the full PostgreSQL/PostGIS integration suite because shared migration/interceptor/reset/storage contracts justify it.
12. Run the full frontend test suite.
13. Run `docker compose config -q`.
14. Run relevant shell/config validation.
15. Run `git diff --check`.
16. Perform a fresh adversarial review against `main` for:
    - obsolete mutation bypasses;
    - tenant-filter bypasses;
    - direct `DbContext` ownership in services/workflow coordinators;
    - stale-write paths;
    - destructive/data-loss side effects;
    - migration history;
    - storage cleanup ordering;
    - documentation that claims validation not actually performed.

# 13. Final merge acceptance conditions

Do not mark this branch merge-ready until every applicable item below is true or an explicit documented decision accepts the residual risk.

## Migration/deployment safety

- [x] Completion migration history cleanly extends current main.
- [x] Current-main-with-representative-data upgrade is automated and green.
- [x] Full preservation census is green: every current-main v32 durable model table is explicitly classified, seeded, and checked across migration; rollout is validated as a separate preservation boundary.
- [x] Rollback to the supported prior schema is tested.
- [x] EF pending-model check is green.

## Mutation-boundary integrity

- [x] Cave detail cannot mutate Files or line plots.
- [x] Aggregate editor is the only normal interactive Cave-owned asset mutation surface.
- [ ] Existing-Cave edit without expected revision fails safely.
- [ ] Stale edits cannot silently overwrite.
- [x] No obsolete standalone GeoJSON publication endpoint/control remains.

## Destructive-operation safety

- [x] Reset behavior is tested against a dense dependency graph (merged destructive-safety package).
- [x] Reset does not affect the second tenant in the tested relational/blob graph (merged destructive-safety package).
- [x] Post-commit storage failure has truthful user/API semantics (merged destructive-safety package).
- [x] Fate of stored archives during reset is intentional and documented (merged destructive-safety package).
- [x] Durable cleanup retry and cross-replica API/archive admission tested; independent write paths still need deployment audit.
- [ ] Hard-delete/external-cleanup failure semantics are safe/retryable.

## Storage/File safety

- [ ] Fake-storage business tests remain green.
- [ ] Production object-storage adapter has at least one real Azurite contract suite.
- [x] Immutable Cave/File object creation is provider-enforced create-only; a retry/path bug cannot silently overwrite an existing object address, and failed create does not compensate-delete pre-existing bytes.
- [ ] Staged/published/removed-history/expired File lifecycle remains mutually exclusive and proven.

## Cross-cutting compatibility

- [ ] Global save-interceptor matrix is green or equivalent evidence exists.
- [x] Hidden feature fields survive unrelated edits.
- [ ] Account switching cannot reuse actionable foreign-account UI data.
- [ ] Ordinary reads do not unexpectedly mutate durable state.
- [x] Archive current-state/backup semantics are explicit; full generated-archive runtime validation remains open under GAP-010.

## Browser/runtime

- [ ] Critical authenticated browser smoke test passes.
- [ ] PDF viewer works through the real custom-header/range-request path.
- [ ] Nested-route hard refresh works.
- [ ] Stale-edit conflict UX works.
- [ ] Unsaved-change browser behavior works.
- [ ] Critical WebKit/Safari-equivalent smoke path passes if the browser harness supports it.

## Documentation/reporting

- [x] `docs/cave-revisions.md` describes implemented truth.
- [ ] completion plan README/checklist no longer claims an active incomplete state once actually complete.
- [x] this ledger is refreshed through the GAP-001 preservation-census package; refresh again after the branch's final adversarial validation.
- [ ] exact final test/build commands and results are recorded.
- [ ] no validation is claimed merely because a related test exists.

# 14. Important review limitations and boundaries

The completed adversarial review was extensive but was not equivalent to manually clicking every UI screen.

Specifically:

- no live production/staging/development application database was inspected or modified;
- disposable repository-owned PostgreSQL/PostGIS databases were used for integration/migration experiments;
- no Azure production resources were modified;
- real Azure Blob Storage behavior was not exercised; service tests used a fake object-storage boundary;
- no real-browser end-to-end suite was run;
- no full real account archive round-trip/restore was proven;
- the synthetic migration preservation census now explicitly seeds and checks every current-main v32 model table; it is still synthetic data rather than a sanitized restored real-world database;
- dependency-audit results were informational and inherited vulnerabilities remain a separate security-maintenance concern.

# 15. Notes for the next agent

Start by reading this ledger and the authoritative completion-plan documents. Re-check branch HEAD and `main` before trusting the SHAs/counts recorded above.

Do not begin with a broad refactor.

Recommended next work order:

1. add the production `AzureBlobObjectStorage` + Azurite contract suite;
2. add the global `SaveChangesInterceptor` operation/tenant compatibility matrix;
3. finish remaining external-cleanup fault injection for hard Cave/File/staging/archive workflows;
4. add the small critical browser/PDF smoke suite;
5. finish archive runtime validation and account-switch/cache isolation;
6. run the final adversarial validation sequence and refresh this ledger against the resulting committed SHA.


When working on Reset Account, do not assume the whole account container contains only current Cave Files. Current code also uses that container for account archives and import transport. Trace every producer/prefix before changing container cleanup behavior.

When adding tests, prefer **state/invariant assertions** over mock-call assertions. A passing test should answer “what durable/user-visible safety property is guaranteed?” rather than only “which method was called?”

# 16. Current overall verdict

**IMPLEMENTATION BLOCKERS RESOLVED; PRE-MERGE VALIDATION REMAINS.**

The branch's core architecture is not the reason it is blocked. The revision/proposal/import/File-publication model is generally strong, and the existing integration suite contains unusually meaningful coverage.

Remaining merge risk is concentrated in:

- real storage-provider behavior;
- broad shared-interceptor compatibility;
- real-browser integration;
- archive generation/runtime behavior;
- account-switch/client-cache isolation;
- independent-writer/live-provider deployment boundaries around destructive cleanup.

The P0 checklist is complete through the GAP-001 preservation-census package. Add the highest-value P1 safety tests and then perform one fresh adversarial pass rather than assuming the previous green suite proves the corrected branch.
