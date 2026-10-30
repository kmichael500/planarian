# Cave Revisions Completion Status and Validation Ledger

**Branch:** `feature/cave-revisions-completion`
**Status:** **CAVE FEATURE INTEGRATED AND VALIDATED; COORDINATED DEPLOYMENT AWAITS ENVIRONMENT SETUP AND DEVELOPMENT REHEARSAL BEFORE ACTIVATION.** The backend/browser feature acceptance recorded below remains SHA-specific. GAP-014 inactive-account sweeping remains intentionally deferred.
**Ledger last refreshed:** 2026-10-29
**Completion HEAD reviewed:** `161b3294287d72c550f0896a855efbf22a3ff825` (committed Cave Revisions and one-migration source baseline; the new coordinated deployment workflow is currently an uncommitted implementation, with separate local static/unit evidence and outstanding hosted-environment acceptance). Agent 2/3 accepted `52f2c593` before GAP-015, whose integrated source commit is `f004ae48fcd88fbc415bb9cb4363794c2d52f418`. Earlier package evidence remains SHA-pinned to its own source.
**Typed-identifier refresh:** 2026-10-29; the original `dc34b128` package is merged, then post-merge adversarial review fixed serialized revision-diff path coupling and aligned the raw-member architecture guard with the documented exact-`nameof` exception in `455386de`.
**Permission-policy refresh:** 2026-10-29; `e0b90a60` centralizes Account/Cave role semantics, hardens unsupported-key handling, moves current browser authorization to a versioned server-effective projection, and retains a version-gated compatibility adapter for independently deployed older APIs.
**Current `main` reviewed:** `e342691c82f1bb6549be903d3a8f94a5d2560ac2`
**Migration validation refreshed:** 2026-10-28; migration + PostgreSQL-foundation coverage is green for the nullable proposal-base migration, including explicit fail-closed downgrade behavior while creation proposal versions exist. Final exact-tree suite counts are recorded in the package section below.
**Rollout-baseline validation refreshed:** 2026-10-26; the preservation census separately validates rollout against the complete v32 fixture; earlier merge-reconciliation evidence remains recorded below
**Merge base:** current `main` above
**Historical branch-distance/diff statistics:** omitted from the live summary because they drift with each completion merge; use `git rev-list --left-right --count main...feature/cave-revisions-completion` and `git diff --stat main...feature/cave-revisions-completion` to obtain current values.

## 2026-10-29 — Coordinated deployment pipeline (implementation, not activated)

The four independent API/web GitHub Actions deployment workflows are replaced
in the feature branch by `.github/workflows/Planarian.Deploy.yml` and the
change planner in `scripts/deploy/plan.py`. It builds and deploys only the API,
web, and EF migrations required since the last successful coordinated release
for the selected environment. `main` still deploys Development automatically,
while a manual Development dispatch may use any trusted selected repository
branch/ref. Production is independent of Development history: it requires a
manual `planarian-release-v*` tag dispatch and no longer has an exact-commit
Development-promotion gate. Production still refuses divergent-history
rollback implicitly and requires a recoverability acknowledgment when database
changes are selected.

Durable selective-deployment state is recorded with GitHub's native Deployment
objects rather than Actions workflow-run history. Immediately before the first
hosted mutation, the gated deploy job creates a task-scoped
`planarian-coordinated-release` record in an internal Development/Production
tracking environment. The newest `SUCCESS`/`ACTIVE` tracking Deployment is the
component-diff baseline; every newer tracking record, including a failed,
in-progress, statusless, or interrupted attempt, freezes its migration files
because that attempt may already have crossed the database boundary. The plan
job reads the full paginated Deployment connection, validates pagination and
commit availability, and fails closed rather than guessing state. This closes
a real partial-failure gap where a migration applied during a failed release
could otherwise have been edited before the next successful release.

One-off deployment behavior is not encoded as Cave-specific logic in the
permanent workflow or planner. `scripts/deploy/release_tasks.py` loads strict,
fail-closed manifests and `scripts/deploy/run_tasks.py` executes generic
preflight/run plus optional maintenance phases. The Cave history bootstrap is
isolated to `scripts/deploy/tasks/cave-revision-history.{json,sh}` and the small
`Planarian.Operations` console host. Permanent canonical snapshot persistence
remains in the existing `Planarian.Library`; rollout-specific persistence does not. The prior `Planarian.Model -> Planarian.Library`
dependency was inverted: Model is now independent, Library references Model, and
both the API and Operations reference Library. Operations does not reference
`Planarian.csproj` or load the web host. Its generic `Program.cs` only
dispatches registered operations; the Cave-specific operation and
`CaveRevisionRolloutRepository` live entirely in `Planarian.Operations`.
The Operations-only repository owns the `PlanarianDbContext`/Npgsql setup from
the same scoped `ConnectionStrings__default` secret used by the EF bundle. Its manifest
uses the completion migration plus the isolated
`Planarian.Operations/CaveRevisionHistory/**` folder as domain triggers and
declares API/database requirements, first-release behavior, and maintenance
requirements. Manifest/script/Cave-operation edits reselect the task, while
unrelated Operations or shared Library changes do not reopen Cave maintenance. The task owns
quiescence, idempotent
`Planarian.Operations initialize-cave-revision-history` execution, and API
resume. The registry rejects stray/nested/symlink task content, noncanonical paths,
duplicate IDs/scripts/triggers/requirements, unknown requirements, dead tasks,
multiple maintenance owners, and path escape. The Cave preflight also verifies
the exact completion migration is present. After the one-off succeeds in every
environment that needs it, its manifest/script and Cave-specific Operations
command/rollout repository can be retired; the generic Operations host and
normal shared snapshot repository remain. The API contains no rollout command,
readiness repository, or deployment-history startup check; the coordinated
deployment owns sequencing and will not start the new API until the Operations
rollout succeeds.

Security/operational hardening in the final pass includes explicit least-
privilege `GITHUB_TOKEN` permissions, non-persisted checkout credentials, full
commit-SHA pins for every action touched by this package, Azure Login v3.1.0 on
the verified peeled action commit, suppressed default Azure CLI stdout,
trusted-ref guidance for privileged manual Development runs, protected release-
tag guidance for Production, and fail-before-mutation checks for required
runner tools/configuration. Stage 1 intentionally keeps the existing Azure
service-principal secret; OIDC/federated Azure login is documented as a later
hardening step once the Azure identity exists. The scoped database migration
secret is supplied to both the EF bundle and `Planarian.Operations` as
`ConnectionStrings__default`, rather than being placed on either process
command line. GitHub no longer needs an Azure App Configuration connection
string for the Cave operation.

**Final deployment validation:** `python3 -m unittest discover -s
scripts/deploy -p 'test_*.py'` passes **72/72**. The matrix covers selective
component planning, arbitrary Development refs, independent Production
history, divergent rollback refusal, durable GitHub Deployment pagination and
state parsing, statusless/interrupted attempts, partial-failure migration
immutability, branch-only migration removal refusal, snapshot-without-migration
refusal, strict task registration/execution/retirement, maintenance ordering,
exact-commit maintenance-resume gating, artifact-before-mutation ordering,
least-privilege workflow contracts, immutable
action references, runner preflight, EF pending-model enforcement, PR-base
migration protection, environment-based migration-secret handling, the separate
`Planarian.Operations` artifact boundary, narrow one-off task reselection, and
regressions that keep rollout/deployment knowledge out of web `Program.cs`
and generic command details out of Operations `Program.cs`, while requiring the
Cave rollout implementation itself to remain isolated in `Planarian.Operations`.

The dependency-inversion pass previously validated **42/42** relevant
unit/contract tests, **15/15** permission/save-interceptor integration tests,
and **5/5** Cave rollout/scale integration tests. A fresh adversarial pass over
the final isolation design then tightened the trusted snapshot escape hatch to
an internal friend-assembly boundary, made the deployment-only rollout types
internal, removed unnecessary DbContext ownership/disposal ceremony, and
qualified every rollout `IgnoreQueryFilters` bypass explicitly by authoritative
Account IDs. Exact-tree revalidation passes **12/12** directly affected
rollout/migration/reference-scale integration tests with zero skips, including
the production connection-string entrypoint and the 10,000-Cave consumers. The
full deployment suite passes **72/72**. The affected Library and Operations
projects build with zero warnings/errors; the integration project builds with
only pre-existing Model/API nullable warnings. The exact Operations publish
produces an approximately **9.1 MiB** artifact containing no `Planarian.dll`,
no Azure App Configuration/Identity/Storage dependency, and no
`Microsoft.AspNetCore.App` runtime requirement. Direct dependency inspection
confirms Model has no project references, Library references only Model and no
longer carries a direct Npgsql package, and Operations references only Library
plus Model while owning its EF/Npgsql rollout dependencies. The Library exposes
its trusted non-request snapshot entry point only to `Planarian.Operations`
through the SDK-supported `InternalsVisibleTo` assembly boundary; neither
Library nor the API takes a project reference on Operations.

The earlier, broader pre-simplification validation remains useful historical
evidence (522/522 unit/architecture, 46/46 migration/foundation/rollout, and
302/302 Cave Revisions integration), but those broad suites were intentionally
not rerun after the final dependency inversion because the user requested only
tests relevant to this refactor. Targeted formatting, registered-task shell
syntax, repository documentation validation, stale-reference scans, and
`git diff HEAD --check` are the final static closeout gates.

The exact deployment EF bundle command previously built a self-contained
`linux-x64` migration bundle successfully at **119 MiB**; the final refactor
did not modify the migration project or migration files.
An additional
ad-hoc macOS bundle/environment-variable smoke using an emulated amd64 PostGIS
container was inconclusive because the emulated host connection dropped; it is
not counted as validation evidence. No hosted Azure release or existing
Development/Production application database was touched.

**Activation boundary:** Existing Development/Production GitHub Environment
values must still be configured; a self-hosted deploy runner, if used, must be
Linux x64, current enough for Node 24 actions, and able to reach PostgreSQL and
the Azure control plane. The Cave operation itself no longer needs Azure App
Configuration access. Verify any separately installed GitHub deployment
integration ignores the internal `planarian-coordinated-release` tracking
records unless intentionally configured otherwise. The stopped-App-Service ZIP
deployment mechanism for the initial Cave rollout still requires a safe
Development rehearsal before Production activation. Stage-2 deployment slots
remain intentionally outside scope.

## 2026-10-29 — Single post-v32 Cave Revisions migration consolidation

The feature-only migration chain was consolidated into **one migration after the unmodified
`20260831132210_v32` main baseline**: `20261029194500_CaveRevisionsCompletion`.
The seven interim Cave revisions, feature-setting backfill, Reset cleanup, unified storage
cleanup, new-Cave proposals, permission projection, and permission-identity migrations
were not shipped/applied to any application database per the release constraint. Their
custom SQL transformations, fail-closed checks, and reverse-order downgrade operations
are preserved in the new migration. The final EF model snapshot remains unchanged.

The exact v32 → feature migration and feature migration → v32 transition is now the only supported
feature deployment/downgrade boundary. Migration tests that previously paused at
intermediate unshipped schema steps now arrange legacy seed data in v32 before applying
the feature migration. The pre-release-only retained-File/Reset-debt conversion
intermediates are not a deployed upgrade path; final cleanup storage schema is tested
directly from v32 instead. Existing v32 census/preservation, filename round trip,
proposal downgrade refusal, feature-setting provenance, and permission-identity
fail-closed coverage remain required.

This changes only migration assembly and migration-test compatibility, not the
application's Cave revision, proposal, File, or authorization runtime contracts.
No existing application database was accessed. **Final migration-only validation:**
`dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj -c Release --no-build --no-restore
--filter 'FullyQualifiedName~MigrationUpgradeIntegrationTests|FullyQualifiedName~FeatureSettingMigrationIntegrationTests|FullyQualifiedName~FileMetadataMigrationIntegrationTests|FullyQualifiedName~FileOwnershipMigrationIntegrationTests|FullyQualifiedName~CurrentMainDatabasePreservationMigrationIntegrationTests'`
passed **33/33** against disposable PostgreSQL/PostGIS. `dotnet build
Planarian.Tests.Integration/Planarian.Tests.Integration.csproj -c Release --no-restore` passed
with zero errors/warnings. EF `migrations list` identifies only v32 plus
`20261029194500_CaveRevisionsCompletion`; `migrations has-pending-model-changes`
reports no pending changes. Both forward and reverse EF-generated SQL scripts were
compared to the seven-migration chain at pre-consolidation HEAD `95db8e6d`:
after removing only history-row DML and transaction markers, the up (443 nonempty
SQL lines) and down (166 nonempty SQL lines) are **exactly identical**.
The final migration records a single `__EFMigrationsHistory` row.
Repository docs validation and `git diff --check` also passed.
The earlier full frontend/browser suites were not rerun because runtime, frontend,
and browser implementation are unchanged by this migration-only adjustment.

## 2026-10-29 — Agent 4 final integration and acceptance — DONE + VALIDATED

**Integration code SHA:** `f004ae48fcd88fbc415bb9cb4363794c2d52f418` on baseline
`52f2c5935958c2ab484e779b140533bacd21eb5f`. Durable documentation was reconciled at
`62bf08ba06b0afafa655b643155d0be1b4317942`. This status-only closeout records exact
run provenance, without claiming that any older broad suite was rerun.

Integrated GAP-015's seven-file source/test package and one additional repository boundary. Object creation,
relational publication, and staging persistence may each succeed despite a lost acknowledgement. On any
upload-once failure after object-creation attempt, the service now queues `UnpublishedFileRemoval` debt,
discards EF's possibly rolled-back tracked changes before separately persisting that debt, and performs
live-reference-checked cleanup. The repository explicitly ignores read/permission filters while qualifying
same-account live Files by AccountId; it also protects cross-account live references. No direct compensation
can delete the bytes of a File whose PostgreSQL transaction committed but whose response was lost.

**Tests run on final integrated code, all green:**

- `dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj -c Release --no-restore --filter 'FullyQualifiedName~CaveAggregateAssetIntegrationTests|FullyQualifiedName~CaveChangeRequestConcurrencyIntegrationTests|FullyQualifiedName~CaveChangeRequestStagedFileIntegrationTests|FullyQualifiedName~ExternalFileCleanupIntegrationTests|FullyQualifiedName~CaveChangeRequestFilePublicationIntegrationTests' --logger 'console;verbosity=minimal'` — **61/61 passed**. Includes concurrent approval/claim assertions, ambiguous create, confirmed commit with lost acknowledgement, and staged SaveChanges acknowledgement lost.
- `dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj -c Release --no-restore --filter 'FullyQualifiedName~UnprivilegedCleanupCannotDeleteBytesReferencedByHiddenLiveCaveFile' --logger 'console;verbosity=minimal'` — **1/1 passed**, explicitly validating live-File protection despite missing Cave View permissions. The first version of this added test used an overlength fixture ID; it was corrected before the passing final run.
- `dotnet test Planarian.Tests.Unit/Planarian.Tests.Unit.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~ApplicationArchitectureTests|FullyQualifiedName~TypedIdentifierUsageContractTests|FullyQualifiedName~FileResponseSecurityTests|FullyQualifiedName~FileNamePolicyTests|FullyQualifiedName~CaveRevisionMetadataDiffTests' --logger 'console;verbosity=minimal'` — **69/69 passed**.
- `dotnet build Planarian/Planarian.sln --configuration Release --no-restore` — **passed, 0 warnings, 0 errors**.
- `python3 scripts/validate-repository-docs.py --base-sha e342691c82f1bb6549be903d3a8f94a5d2560ac2` and `git diff --check` — **passed** following ledger reconciliation.

**Accepted independent audit evidence at `52f2c593`:** Agent 2 found no separate P0/P1
architecture, tenant, migration, import, archive, storage, or mutation-path blocker;
architecture tests **14/14**, EF pending-model validation and documentation/build checks passed.
Agent 3 ran the supported `npm run test:browser:e2e`: **20/20 passed, no failures/retries**
(**10/10 Chromium**, **10/10 WebKit**), browser-harness safety **1/1**, and reviewed the matching
PR workflow's engine installation, changed-path triggers, failure behavior, and report retention.
No CI implementation change was necessary.

**Historical broader suites (not final-tree reruns):** the earlier `4328ad55` tree passed backend
unit **521/521**, disposable PostgreSQL integration **651/651**, frontend Jest **395/395**,
TypeScript and frontend production builds. The `52f2c593` pre-integration audit independently
passed Release build/EF checks. No frontend, migration, or browser runtime implementation was
modified in the final GAP-015 integration; repeating the full suites was not justified.

**Residual boundaries:** GAP-014's periodic discovery for completely inactive accounts is
intentionally deferred; active/opportunistic cleanup is covered and retryable. The precise mixed
published-File removal vs staged approval interleaving has source/locking review but no retained
deterministic test and is not misrepresented as executed. A total database outage can prevent
new cleanup-debt recording and requires operational recovery; live Azure and independent
non-cooperating writer deployment cases remain separate operational boundaries. No code or
migration is merged into `main` by this feature-branch integration.

## 2026-10-29 — Typed identifier post-merge adversarial validation — DONE + VALIDATED

The typed package was merged into `feature/cave-revisions-completion` as `4328ad55`. A fresh adversarial review of that actual merged completion tree found no runtime, authorization, tenant-isolation, migration, or data-preservation regression, but did find two maintainability/compatibility issues worth fixing before the package was considered closed. Those follow-up fixes are commit `455386de` (`fix(caves): preserve revision diff wire contracts`).

Review findings fixed:

- Reference-metadata diff paths such as `State`, `County`, `Tags/...`, `Entrances/...`, and `Files/.../FileType` are serialized API values parsed independently by the frontend. The original package had mechanically derived several of those path segments with `nameof(...)`, which would have coupled a future CLR rename to an otherwise stable wire contract. They now use explicit `CaveRevisionDiffWireContract` constants with literal compatibility-sentinel coverage; the serialized values themselves are unchanged.
- `TypedIdentifierUsageContractTests` originally rejected both raw-string CLR member lookups and exact-contract `nameof(...)` lookups. That was stricter than `docs/data-access-architecture.md`, which deliberately permits `nameof` for a known exact CLR contract while requiring generic relationship code to use EF model metadata. The architecture guard now rejects duplicated raw member strings, while the generic ownership paths remain directly covered by EF-metadata unit tests and the real-PostgreSQL ownership matrix.

Broad validation on the merged `4328ad55` tree before the narrow follow-up:

- Full backend unit suite: **521/521 passed**, **0 skipped**.
- Full disposable PostgreSQL/PostGIS integration suite: **651/651 passed**, **0 skipped**. This included the 10,000-Cave reference merge/rename scale tests as well as the full permission, revision, storage, import, concurrency, migration, and tenant-isolation matrix.
- Full frontend Jest suite: **78/78 suites, 395/395 tests passed**.
- Client-route generator: **6/6 passed**; CSP configuration: **5/5 passed**.
- TypeScript `tsc --noEmit`: **passed**.
- Production React build and CSP postbuild: **passed**. Remaining CRA/Browserslist/ESLint output is existing repository warning debt rather than a package failure.

Focused validation after `455386de`:

- Revision metadata/scope, typed-identifier architecture, and cross-stack contract unit slice: **16/16 passed**.
- Real PostgreSQL reference-rename integration slice: **22/22 passed**.
- Frontend revision-diff/presentation slice: **2/2 suites, 26/26 tests passed**, with route-generator **6/6** and CSP **5/5** rerun by the frontend pretest.
- Official EF Core guidance confirms that relationships are represented by foreign keys in the EF model with navigations layered over them, matching the model-metadata approach used by the generic ownership code. Official Azure Storage error documentation also confirms `ContainerBeingDeleted` as the typed 409 condition used by the Blob adapter.
- Repository documentation validation and `git diff --check` passed after this ledger update.

## 2026-10-29 — Typed identifier and cross-stack contract hardening — DONE + VALIDATED

The original audit work was preserved as commit `28a350a3` (`refactor: harden typed identifier contracts`) and reconciled onto completion HEAD `499f255f` as `dc34b128`. The 65-file package cherry-picked without manual conflict resolution, including the files also changed by the newer new-Cave, storage, browser-E2E, and permission-policy packages.

Implemented behavior:

- Generic EF/reflection code no longer duplicates known CLR member names where typed access, `nameof`, or EF relationship metadata can express the contract. Reusable relationship metadata mechanics live in `Planarian.Model/Database/Extensions`, while domain-specific ownership policy remains in the interceptor/repositories.
- A repository architecture contract prevents new raw EF/reflection member-name strings in the covered production paths and separately verifies the SaveChanges interceptor entity classification.
- Finite backend-owned wire vocabularies that are mirrored in TypeScript are explicit and covered by `CrossStackStringContractTests`, including permissions, feature keys, revision/proposal states, tag roles, upload statuses, query operators, SignalR names, and API error types.
- Client-only computed Cave distance is separated from persisted/server `FeatureKey` values, project-member selection is separated from database TagType values, and File type display names remain intentionally open-ended because accounts can configure them.
- Export-field parsing accepts only defined server `FeatureKey` values, rejects numeric enum text/unknown keys/client pseudo-features, and preserves the existing case-insensitive compatibility behavior.
- Settings State/County permission-filter endpoints validate the small supported filter vocabulary rather than allowing an unknown value to fall through to the unfiltered path. This remains a narrow endpoint-input contract layered on top of the newer centralized `PermissionPolicy`.
- SignalR route/event names and Azure Blob error codes use centralized/typed contracts where appropriate, while external protocol/database identifiers that are compatibility boundaries remain explicit strings.
- Durable data-access/testing guidance now records the typed-member/EF-metadata boundary and the cross-stack finite-string contract rule.

Validation evidence on the exact reconciled tree:

- Focused backend unit/architecture/permission slice: **56/56 passed**, covering typed identifier usage, cross-stack mirrors, EF metadata helpers, Settings permission-filter validation, SaveChanges interceptor coverage, and the current centralized permission policy.
- TypeScript `tsc --noEmit`: **passed**.
- Focused frontend Cave/Tag Jest slice: **30/30 passed**; frontend pretest also passed client-route generator **6/6** and CSP configuration **5/5**.
- Real PostgreSQL/PostGIS SaveChanges/interceptor compatibility and ownership slice: **58/58 passed**.
- Real PostgreSQL Settings permission-scope slice: **4/4 passed**.
- Real PostgreSQL Cave read-integrity slice: **4/4 passed**, including the broad search/map/files/history/proposals/authoring no-domain-write contract.
- Real PostgreSQL direct Cave-tag mutation: **1/1 passed**.
- Export-field contract tests: **2/2 passed**.
- No migration/model change was introduced. The focused validation was intentionally used instead of redundantly rerunning the already-green full backend/browser suites because the package's high-blast-radius interactions are directly exercised by the slices above.

## 2026-10-29 — Permission-policy centralization — DONE + VALIDATED

The validated package is commit `e0b90a60` (`refactor(auth): centralize permission policy semantics`), based directly on completion HEAD `d07458ee`.

Implemented behavior:

- `Planarian.Model.Shared.PermissionPolicy` is the executable source of truth for the persisted Account/Cave role vocabulary, Account Admin → Manager → View inheritance, Cave Manager → View inheritance, resource-scope bypass classification, and server-effective client role projection.
- `RequestUser`, account permission management, creation-request visibility/review queries, Settings regional selectors, and session projection consume that shared policy while retaining their feature-specific tenant/resource-scope queries.
- Unknown semantic permission keys now fail closed. A malformed or future same-named persisted `UserPermission` cannot authorize either the coarse capability check or the public scoped-capability probe.
- Client projection version 2 returns effective coarse capabilities from the server; the browser performs exact membership checks for v2 sessions. A narrowly isolated version-1 adapter preserves rolling deployment/rollback compatibility with older APIs that returned persisted roles rather than effective capabilities.
- Account-scoped projection uses an explicit supported-role allowlist, so malformed cross-domain, reserved synthetic/platform, and future persisted keys do not leak into browser authorization state. Global `PlanarianAdmin` remains separately validated and does not imply Account/Cave authority.
- The PostgreSQL `UserCavePermissions` view necessarily repeats persisted role names in SQL. A source contract test compares its Cave-role/Admin semantics with `PermissionPolicy` so SQL and C# policy cannot silently drift.
- Account-switch session application keeps the projection version atomic with account identity, permissions, feature settings, and pending-invitation state; a failed switch cannot partially activate the target account's legacy/current projection semantics.

Validation evidence:

- Before the final fail-closed review hardening, the frozen refactor tree passed the full backend unit suite **496/496**, full disposable PostgreSQL/PostGIS integration suite **649/649** with **0 skipped**, frontend Jest **78/78 suites / 394/394 tests**, route-generator **6/6**, CSP configuration **5/5**, Release solution build, production React build, EF pending-model check, repository-doc validation, and `git diff --check`.
- The final review then found and fixed the reachable unknown-key authorization gap above and strengthened AppService/account-switch/SQL-contract tests. The affected final tree passed **27/27** permission-policy unit tests, **7/7** targeted real-PostgreSQL integration tests, and **15/15** targeted frontend tests; frontend pretest also reran route-generator **6/6** and CSP **5/5**.
- Per the requested validation scope, the long-running full integration suite was not redundantly rerun after that narrow final hardening. The final review changes are directly exercised by the focused PostgreSQL and frontend tests above.
- Repository documentation validation and staged/unstaged `git diff --check` pass. Focused formatting passes for the new/refactored policy and matrix files; the remaining `RequestUserPermissionIntegrationTests` formatter findings are unchanged pre-existing whitespace debt also present on untouched completion HEAD.

## Browser-E2E integration validation (2026-10-28)

The `test/cave-revisions-browser-e2e` package adds a small Chromium/WebKit Playwright suite, disposable E2E dataset provisioning, production frontend/CSP serving, account-switch/cache isolation changes, and file-action `PlanarianButton` compatibility. It was first reconciled with completion HEAD `3bdb3146` on merge commit `16c4ecd3`, then reconciled with archive-runtime completion HEAD `87b093ba` on merge commit `31899b4b` before final integrated validation. GAP-007 and GAP-013 are therefore resolved by executable browser evidence rather than by implementation presence alone.

Validation on 2026-10-28 ran the repository-owned browser-E2E stack from a stopped environment, including production frontend builds and headed Chromium/WebKit acceptance runs. Each acceptance run reset and verified the isolated `e2e` Postgres/Azurite dataset and exercised the real local HTTPS/CORS/CSP topology. The final acceptance run completed **20/20 Playwright tests green** (10 scenarios in both Chromium and WebKit) before clean teardown with exit code 0. Validation exposed and fixed a real account-switch race: the global switch-event listener was registered in a passive React effect, so a sufficiently fast interaction could dispatch before the listener existed; it now registers in `useLayoutEffect`, and the previously failing proposal/account-switch workflows pass in both engines. PDF validation also removed browser-engine-specific assumptions: Chromium may issue byte ranges while WebKit may fetch this small fixture in one response, so the test proves real PDF.js rendering, authenticated cross-origin access/CORS, and the stream-session header in each browser while a deterministic authenticated probe proves exact `206`/`Content-Range` semantics. Chromium's local test hostname is pinned to loopback through Playwright launch options to avoid `.local`/mDNS startup flakiness without changing the production hostname, TLS, CSP, or routing under test. Focused account-isolation tests were **4/4 green**, proposal navigation was **2/2 green**, coordinator unit tests were **4/4 green**, TypeScript checking passed, and the earlier runner active-Compose refusal regression remained green. The reconciled completion package's `CaveReadIntegrityIntegrationTests` plus `CaveStaleClientSafetyIntegrationTests` were **10/10 green** after a documented solution restore. The initial `--no-restore` invocation failed only because local NuGet/.NET 8 reference artifacts were absent and did not execute tests.

## 2026-10-28 — New-Cave proposal and permission package — DONE + VALIDATED, merged

The validated package was committed as `ab6da0e1` (`feat(caves): support new-cave proposals and harden permissions`) and merged into `feature/cave-revisions-completion` as `f5e40668`. It was based on `70bff4ec72b864668f1e4e9c43fe72ea57a9a219` and reconciled against completion HEAD `d5546088` before integration.

Implemented behavior:

- Any user with effective Cave `View` capability in the current Account can author a proposal for a new Cave in any configured State/County, even when they cannot view an existing Cave in that destination.
- Direct creation remains destination-Manager-authorized. A user who manages one destination can still use the same Add Cave surface to propose in another destination they do not manage.
- A creation request reserves the future Cave ID without creating a placeholder Cave or fake revision; request/proposal `BaseRevisionId` is null until publication.
- Current Managers of the proposed destination review creation requests, including empty Counties. Submitter rerouting changes the review audience immediately, and loss of destination Manager authority removes review/read/decision access.
- Automatic and first-available County Cave Number allocation uses authoritative account+County data, including hidden and archived Caves, while the `IgnoreQueryFilters()` lookup remains explicitly account-qualified.
- Approval atomically creates the Cave, publishes its first `UserSubmission` / `Create` revision, and approves the request. The submitter receives exact Cave `View` only when still an Account member; approval remains valid if the submitter has left.
- Account removal serializes on the target `AccountUser` row with every account-scoped permission grant path: proposal-approval Cave `View`, ordinary Cave `View`/`Manager`, and Account `Admin`. Grant transactions use `FOR KEY SHARE`; revocation uses `FOR UPDATE`, removes all grants plus membership atomically, and grant paths fail closed if revocation wins first.
- Creation-proposal rollback is explicit: downgrade across `SupportNewCaveProposals` refuses to proceed while null-base proposal versions exist rather than relying on an incidental NOT NULL failure.
- The frontend route, empty-state CTA, direct-vs-proposal mode, reviewer wording, and hidden Manager-only County-number controls all match the server authorization model.

Adversarial review fixes in this package included a real Manager-only `/caves/add` route defect, an unconditional empty-state create affordance, unnecessary Manager-only County-number probes from View-only authoring, account-removal/grant races across proposal approval and ordinary Cave/User permission management, missing transaction preconditions, stale durable null-base documentation, gaps in destination-reviewer revocation and County-number tenant-isolation coverage, missing empty-Account View-capability coverage, and fail-open authorization/projection when malformed persisted rows crossed the User/Cave permission-type or reserved role-key families. The final permission-domain hardening covers `RequestUser`, the `UserCavePermissions` SQL view/global Cave filter, creation-request visibility/review/decision queries, regional Manager selectors, scoped capability probes, permission-management writes, account provisioning, and client projection (including rejecting account-scoped `PlanarianAdmin`). Permission definitions are resolved by `(PermissionType, Key)` and a forward migration makes that pair unique while explicitly refusing same-domain legacy duplicates; cross-domain identical textual keys remain valid.

Validation recorded before commit:

- `Planarian.Tests.Unit`: **472/472 passed**.
- Frontend Jest: **74/74 suites, 373/373 tests passed**, plus client-route generator **6/6** and CSP configuration **5/5**.
- Frontend production build: **passed**; remaining CRA/Browserslist/React/ESLint messages are existing repository warnings rather than package failures.
- Focused Cave creation/authorization/permission-management/settings PostgreSQL slice: **54/54 passed** before the final two County-number repository tests were added.
- Migration + PostgreSQL-foundation slice: **38/38 passed**.
- Hardened permission matrix: **29/29 passed** after converting the scope/role grids to xUnit theories, including empty-Account View capability and malformed cross-type/cross-role rows failing closed.
- Final permission-management/settings slice: **52/52 passed**; this includes Cave/User grant-vs-revoke serialization, unsupported-role/domain rejection, and platform account creation selecting the User-domain `Admin`.
- Permission-domain migration regression slice: **3/3 passed**, including pristine latest migration, cross-domain identical-key preservation, and explicit fail-closed handling of same-domain duplicate definitions.
- Final full PostgreSQL integration suite for the exact final test assembly: **649/649 passed**, **0 skipped**.
- EF pending-model check: **passed**; no model changes remain outside the generated permission-domain migration and its updated snapshot.
- Repository-doc validator plus staged/unstaged `git diff --check`: **passed**.

The exact package commit is `ab6da0e1`. Historical evidence below remains attached to the SHAs and package states that actually produced it.

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

The completion-plan README now preserves the implemented design/acceptance history; durable architecture documents describe current behavior. Merging this feature branch to `main` remains a separate action.

## Status legend

- **DONE + VALIDATED** — implemented and supported by direct review/test evidence.
- **DONE, PARTIAL VALIDATION** — implementation appears complete, but one or more important scenarios remain unproven.
- **MOSTLY DONE** — implementation is substantially present but a known path still contradicts the target state.
- **BLOCKER** — should be fixed before merge.
- **VALIDATION GAP** — no known implementation defect yet, but risk is material enough to require explicit proof before merge.
- **FOLLOW-UP** — useful hardening that may be post-merge only if the merge gate explicitly accepts the risk.

# Executive assessment

**Final implementation verdict: no independent P0/P1 blocker remains on the integrated completion branch.**
The completed revision/proposal/File/import architecture has passed its final architecture and browser audits, with
the separate GAP-015 File retry/race patch reviewed and hardened during final integration. The branch is ready for
a separate PR/merge into `main`; it has not been merged into `main` by this work.

The audit evidence is scoped: Chromium and WebKit browser E2E passed 10/10 each; unit/integration/frontend suites
recorded at earlier commits are historical, not automatically rerun on every later documentation change. The
final integration adds targeted real-PostgreSQL tests for an acknowledged object with an uncertain database commit
and for staged-row persistence with a lost save acknowledgement. Live Azure policy/network/retention behavior
and independent non-cooperating deployment writers are genuine external validation boundaries, not known
application defects. GAP-014's inactive-account periodic sweeper is deliberately deferred as storage hygiene.

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
| Direct manager edit optimistic concurrency | **DONE + VALIDATED** | Existing Cave edits require client-loaded `ExpectedRevisionId`; stale edits conflict instead of silently overwriting. Explicit missing-token and malformed-PUT regression coverage is now in GAP-008. |
| Semantic no-op suppression | **DONE + VALIDATED** | Snapshot comparison avoids published revisions/proposal versions for semantically unchanged aggregate state. |
| Revision history query + diff UI | **DONE + VALIDATED** | Unit/frontend coverage exists for nested diffs, line-plot presentation, historical labels, false/zero preservation, etc. |
| Suggest Cave Changes flow | **DONE + VALIDATED** | Immutable proposal-version architecture and authoring flow have broad integration/frontend coverage. |
| Review / approve / reject / request-changes / resubmit | **DONE + VALIDATED** | Includes stale base/proposal concurrency, review-event history, rejection/no-publication behavior, and `xmin` race coverage. |
| Proposal version immutability | **DONE + VALIDATED** | Proposal versions freeze exact proposed state including assets; later mutable state does not redefine older proposal meaning. |
| File authoring in aggregate Cave editor | **DONE + VALIDATED** | Stage/add/edit/remove flows and unsaved-change behavior have frontend/integration coverage. |
| Upload-once staged File lifecycle | **DONE + VALIDATED** | New content is written once; publication associates the existing object rather than copying to a new published key. |
| Published Cave asset removal/history | **DONE + VALIDATED** | Files and line plots now share the same semantic contract: revision history retains lightweight identity/metadata while removed payloads are not deliberately retained as historical Cave content. File bytes use durable post-commit `CaveAssetRemoval` cleanup; `CaveGeoJson` deletion is transactional. |
| Provider-neutral object storage abstraction | **DONE + VALIDATED** | Application logic uses `IObjectStorage` for the affected lifecycle. Business behavior remains heavily tested with a deterministic fake, while the real production `AzureBlobObjectStorage` adapter has an automated isolated-Azurite contract covering byte/metadata round-trip, create-only immutability, missing/delete semantics, concurrent first-use, normalization, lazy-read disappearance, snapshot-inclusive delete, cancellation, and container recreation. Live Azure remains a deployment boundary. |
| Line-plot aggregate authoring | **DONE + VALIDATED** | `CaveLinePlotAuthoringEditor` is the normal interactive authoring surface. The obsolete Cave-detail `CaveSpatialDataImport` / `GeoJsonSaveModal` path is removed; focused frontend coverage proves aggregate authoring and map rendering still work. |
| Line plots in revision/proposal semantics | **DONE + VALIDATED** | Stable IDs, normalized content hash, metadata diffs, proposal exact GeoJSON payloads, deterministic snapshot behavior covered. |
| Cave hard deletion / proposal blocking | **DONE + VALIDATED** | Core Cave deletion and unresolved-proposal blocking have dedicated integration coverage, including durable cleanup debt and idempotent retry when post-commit object deletion fails. |
| Bulk Cave/Entrance import rewrite | **DONE + VALIDATED** | Immutable plan/execute flow, preview/commit equivalence, dry run, rollback, tenant adversarial behavior, golden compatibility, and supported-scale test passed. |
| Import performance architecture | **DONE + VALIDATED** | 10,000-Cave/Entrance workload passed; set-oriented/bounded architecture reviewed. |
| Tenant isolation | **DONE + VALIDATED** | Fail-closed workflow filters, explicit account qualification when bypassing filters, adversarial foreign-account tests. |
| Tag/County administration revision interaction | **DONE + VALIDATED** | Rename/merge behavior has direct rollback, tenant-isolation, proposal-staleness, no-op, lock-order/writer-interleaving, 13-family/16-path Tag coverage, historical duplicate-preservation regression coverage, late multi-batch rollback, and 10,000-Cave rename + Tag/County merge scale coverage. |
| Account settings atomicity / state constraints | **DONE + VALIDATED** | Integration tests prove atomic settings/state update and rollback on invalid state removal. |
| File Name + Extension model | **DONE + VALIDATED** | Completion extends unchanged current-main v32 with a clean post-v32 migration. PostgreSQL coverage proves representative v32 data upgrades safely, preserves distinct legacy `FileName` values across an untouched upgrade/downgrade round trip, respects post-upgrade renames, and restores the v32 `FileName` schema contract without an accidental default. |
| File/PDF/vector read behavior | **DONE + VALIDATED** | Real Chromium/WebKit browser E2E (20/20) includes authenticated PDF.js, CORS/session headers, byte-range probe, and current map/line-plot retrieval. |
| App/global permission semantics | **DONE + VALIDATED** | Platform-admin/account-admin/default-account/export capability integration coverage exists. |
| Global `SaveChangesInterceptor` batching refactor | **DONE + VALIDATED** | Mechanical EntityBase coverage contract plus real-PostgreSQL matrix cover side-effect-free current/original AccountId and Cave/County/Entrance ownership, staged Files, foreign-tenant Added/Modified/Deleted states, mixed-batch atomicity, laundering/smuggling attempts, narrow AccountUser/User invitation exceptions including the unique null-`InvitationSentOn` recovery case, protected global/default writes, and bounded lookup counts. Unchanged Cave→County relationships avoid a redundant ownership SELECT while actual County moves remain validated. |
| Account Reset rewrite | **DONE, PARTIAL VALIDATION** | The merged destructive-safety package tests the Reset graph, two-tenant noninterference, archive container removal, post-commit storage failures, immediate retry, and reset/archive admission. A transactional cleanup outbox, explicit retry endpoint/UI and PostgreSQL shared/exclusive account-maintenance leases cover API writes and background archives. Independent non-cooperating writers and live Azure smoke tests remain deployment validation boundaries. |
| Account archive projection/runtime compatibility | **DONE + VALIDATED** | GAP-010 now covers actual `.tar.gz` generation/content, difficult File/line-plot data, explicit missing-file diagnostics, cancellation/retry, publication conflicts/ambiguity reconciliation, retention, tenant isolation, Reset/delete coordination, and pinned historical old/new publication behavior. Archive remains a current-state export with no restore contract. |
| Unsaved-change guard | **DONE + VALIDATED** | Edit/Suggest/Revise tests cover blocked navigation, keep-editing, discard, asset-only dirty state, and `beforeunload`. |
| Detail-page read-only Cave-owned state | **DONE + VALIDATED** | Cave detail exposes File viewing and the map but contains no direct File or line-plot publication path. Source review confirms the obsolete importer/modal wiring is gone; positive map and aggregate-authoring tests cover the behavior that remains supported. |
| Durable completion docs / final acceptance checklist | **DONE + VALIDATED** | Completion-plan historical guidance, final acceptance checklist, current-state architecture, browser/CI evidence, and GAP-014 deliberate deferral are reconciled during final integration. |

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

1. The global `SaveChangesInterceptor` remains high-blast-radius infrastructure, but its Cave/account-domain switch coverage is now mechanically classified and the ownership mechanisms have a real-PostgreSQL compatibility/tenant-isolation matrix. Legacy Trip/Lead/Project entities and explicitly global/system records remain deliberately outside the Cave-completion ownership contract.
2. The production `AzureBlobObjectStorage` adapter now has an automated isolated-Azurite contract covering the relevant byte/metadata, immutable-create, missing/delete, concurrent first-use, normalization, lazy-read race, snapshot, cancellation, and container-recreation semantics. Azurite is still an emulator, so live Azure account policy, retention, networking, and service-specific behavior remain deployment boundaries.
3. Critical authenticated browser/runtime behavior is now covered by the real 20/20 Chromium/WebKit E2E suite, including account switching, conflicts, PDF.js, CSP/CORS, and hard refresh. Broader optional user journeys remain outside this smoke suite. Archive runtime has separate generated-content, cancellation/retry, tenant, retention, Reset/delete, and old/new publication coverage. Independent non-cooperating writers and live Azure remain deployment audit boundaries.

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

The critical real-browser contract is also covered by Playwright (20/20 across both engines). These limitations describe component tests in isolation, not the integrated test portfolio.

# 4. Validation already completed

The broad validation evidence below is preserved at the SHAs where it was actually collected rather than being silently relabeled as current. The `SaveChanges` hardening package was validated on reviewed HEAD `f9ef92a337805a351a96a236b4ae6caa35e3cf7a` before commit and is recorded first as explicitly pre-commit working-tree evidence; older committed-package, dead-code, and GAP-001 evidence remains pinned to the SHAs recorded in the corresponding refresh sections.

## 2026-10-27 SaveChanges ownership hardening working-tree refresh

The package validated on reviewed HEAD `f9ef92a337805a351a96a236b4ae6caa35e3cf7a` completes the shared `SaveChangesInterceptor` ownership audit: side-effect-free batched current/original Cave/County/Entrance validation, missing current-domain entity coverage, narrowed AccountUser/invitation and authenticated-User exceptions, explicit PlanarianAdmin boundaries for Permission definitions and global UserPermission/FeatureSetting/default-TagType writes, an explicit synchronous-`SaveChanges` rejection so async authorization cannot be bypassed, and a reflection-based EntityBase coverage contract.

Final validation of the production/test source before this ledger-only evidence update:

- Release solution restore/build — **PASS, 0 warnings / 0 errors** in the final run. An earlier fresh rebuild emitted **152 pre-existing nullable/compiler warnings in unchanged files; 0 warnings referenced changed C# files**.
- backend unit/architecture suite — **417/417 passed**;
- full disposable PostgreSQL/PostGIS integration suite — **494/494 passed**, including migration, Golden, Scale, tenant-isolation, invitation, permission, synchronous-SaveChanges rejection, and destructive-operation coverage;
- focused affected ownership/permission/migration matrix — **64/64 passed**;
- focused global-system + FeatureSetting compatibility package after fixture migration — **21/21 passed**;
- EF pending-model validation — **PASS, no pending model changes**; the command still emits the repository's existing required-navigation/global-query-filter warnings;
- repository documentation validator against current literal `main` `e342691c82f1bb6549be903d3a8f94a5d2560ac2` — **PASS**;
- focused dead-code analyzer set (`IDE0051`, `IDE0052`, `IDE0059`, `IDE0060`, `CA1811`, `CA1812`, `CA1823`) — **PASS, zero findings**;
- changed-C# whitespace and unused-using formatting checks, staged/unstaged `git diff --check`, conflict-marker/skip scans, and debug/TODO residue scan — **PASS**.

The full-suite pass caught only stale test setup caused by the newly enforced global-system boundary: global Permission/UserPermission and FeatureSetting prerequisites had to move to the narrow typed `GlobalSystemTestData` bootstrap path. After that migration and additional adversarial Permission/FeatureSetting mutation coverage, the exact runtime source is green. No additional production defect was found in the final pass.

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

After the schema migration, a separate one-time `Planarian.Operations` bootstrap creates the authoritative `SystemBaseline/Baseline` revision for every pre-existing Cave from the canonical snapshot projection and advances `CurrentRevisionId`. The coordinated deployment keeps the API quiesced and refuses to deploy/start the new API until the operation succeeds. The API itself contains no rollout/readiness behavior. The rollout is resumable, reuses one history-boundary timestamp across retries, has no user actor, refuses to create a late baseline after ordinary revision publication begins, and has supported-scale coverage for 10,000 Caves.

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

**Deployment validation still needed:** no live Azure lifecycle smoke has been run. The automated Azurite production-adapter contract materially improves provider evidence but cannot prove live-account policy, retention, networking, or Azure-service-specific behavior. Independent actors that bypass HTTP and the archive coordinator are
not automatically fenced; an unrelated other-tenant writer could create an
unsafe foreign reference after the last pre-delete check. Do not represent a
passing disposable-Postgres/Azurite/fake-storage suite as exhaustive proof.

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

## GAP-004 — RESOLVED — External-cleanup fault injection

Published and unpublished File cleanup now use explicit durable post-commit responsibility rather than best-effort deletion after relational success:

- ordinary published removal, hard Cave deletion, and sync-import Cave deletion queue `CaveAssetRemoval` targets in the same relational transaction that removes the live File/Cave state;
- explicit unpublished-file deletion, expired staging, proposal rejection, and unused staged Files discarded during approval queue `UnpublishedFileRemoval` targets before their File rows commit as removed;
- provider failure leaves the exact target pending while the relational/revision result remains truthful; successful/already-missing deletion acknowledges the target idempotently;
- explicit cleanup targets created by the owning commit are all processed after commit in bounded target/reference-query batches; later Cave/File authoring retries only a small bounded batch of older ordinary cleanup debt, with a live-File-reference guard before deletion;
- Reset Account explicitly absorbs both ordinary File-cleanup purposes into its stronger reset cleanup responsibility before purging the original targets;
- sync import returns committed cleanup-target IDs, not transient blob-delete instructions;
- cleanup-target acknowledgement detaches same-scope persisted tracked targets after set-based deletion so EF state cannot resurrect stale local debt, while unsaved Added debt is never detached;
- the cleanup migration downgrade fails closed for both `CaveAssetRemoval` and `UnpublishedFileRemoval`.

Archive cleanup remains Azure-specific but no longer falsifies a completed archive when temporary-object deletion or pruning fails. Archive jobs acquire the shared maintenance lock plus a separate exclusive per-account archive-job lock on one PostgreSQL session/lease, so new-version replicas serialize per account without consuming two long-lived lock-pool connections; archive deletion takes the same lease. Abandoned temporary cleanup requires a conservative seven-day age before deleting another job's temp key and will not delete the source of a genuine pending legacy Blob Copy. Per-object prune/delete failures are isolated and retried by later jobs. Full generated-archive/content/publication/cancellation/tenant/mixed-version runtime validation is now resolved under GAP-010.

Final validation on the reviewed working tree includes **40/40** focused cleanup/aggregate/import tests after the final scale hardening, **9/9** archive-cleanup/maintenance-lock tests, **2/2** Reset partial-locator fail-closed regressions, **37/37** migration/PostgreSQL-foundation tests, **417/417** backend unit tests, and **511/511** full PostgreSQL/PostGIS integration tests with no skips. The Release solution build succeeds with **0 warnings / 0 errors**, EF reports no pending model changes, and repository documentation validation plus `git diff --check` pass.

## GAP-005 — RESOLVED — Automated AzureBlobObjectStorage contract suite against Azurite

Business-level File tests appropriately continue to use `TestFileBlobStore : IObjectStorage` for deterministic workflow failure injection. The integrated package adds a real production-adapter contract that runs `AzureBlobObjectStorage` against the repository's isolated Azurite Testcontainer and covers:

- Create then OpenRead returns identical bytes and preserves content type, ETag, and last-modified metadata;
- immutable create is provider-enforced and an accidental key reuse cannot overwrite existing bytes;
- missing-object read/delete semantics and idempotent deletion;
- concurrent first-use container creation;
- normalized partition/container behavior;
- object disappearance between metadata lookup and lazy stream opening;
- base-blob deletion when snapshots exist;
- canceled create without object publication;
- container-cache invalidation and recreation after external container deletion.

The archive/Azurite validation pass additionally exposed and fixed a cross-endpoint initialization-cache hazard: same-named containers on different Blob endpoints can no longer reuse another endpoint's successful initialization state, and cache keys exclude query credentials. A focused unit contract covers the key shape, while the File-import/Azurite regression set exercises the behavior across isolated fixture endpoints.

The standalone production-adapter contract passed **10/10**. After the external-cleanup package was adopted, the combined storage/cleanup/proposal/import/reset/archive/migration matrix passed **81/81** with no skips on the integrated working tree. The same Azurite fixture implementation is also used by the account File-import integration path, avoiding a second emulator harness.

Final merge reconciliation against import-parity completion HEAD `bb45e4a2` found and corrected one stale mixed-batch test assertion that still expected the retired transient `BlobDeletes` result; the test now proves the committed `StorageCleanupTargetIds`/`CaveAssetRemoval` contract and that the updated Cave's live File is not queued. On the exact resolved merge tree, the Release solution build passed **0 warnings / 0 errors**, the focused mixed-batch regression passed **1/1**, the backend unit suite passed **424/424**, and the full PostgreSQL/PostGIS integration suite passed **542/542** with **0 skips**. Repository documentation validation, changed-file formatting, `git diff --check`, `docker compose config -q`, and EF pending-model validation also pass; EF reports no pending model changes.

Azurite remains an emulator. Live Azure policy, retention, networking, account configuration, and service-specific behavior remain deployment-smoke boundaries rather than application-contract gaps.

## GAP-006 — RESOLVED — Global SaveChangesInterceptor compatibility matrix

The repository-wide ownership interceptor now has both a mechanical coverage contract and real PostgreSQL behavioral coverage. The audit found five concrete tenant-safety/ownership gaps:

- `CaveChangeRequestReviewEvent`, `CaveGeoJson`, `CavePermission`, `Favorite`, and `UserPermission` were current Cave/account-domain `EntityBase` types with no interceptor switch case;
- Added `AccountUser` rows bypassed Account validation entirely, allowing a selected Account A context to construct membership for Account B;
- Cave/permission rows could carry a current-account `AccountId` with a foreign County ID because the ownership loader batch-resolved Cave/Entrance parents but not County parents;
- modified rows could hide their pre-save owner by changing a direct `AccountId` or mutable parent FK before `SaveChanges`; deleted/severed aggregate children also demonstrated that authorization cannot safely depend on mutating navigations back into place;
- global/system writes needed an explicit authority boundary: ordinary account contexts could construct global `UserPermission`, global `FeatureSetting`, or global/default `TagType` rows because `AccountId = null` had no tenant to compare against, and Permission definitions were previously accepted outside the switch; authenticated no-Account contexts also had overly broad `User` mutation authority.

A final commit-readiness pass also caught a compatibility regression in the first hardening draft: invitation claims required a non-null `InvitationSentOn`, even though invitation creation deliberately keeps a successfully submitted invitation usable when persisting that convenience timestamp fails. The corrected matcher preserves the stronger identity proof without turning that documented resilience path into an unusable invitation.

The implementation now batch-validates current and original Cave, County, and Entrance parent IDs in bounded 500-ID chunks without mutating EF navigations when those relationships are introduced, changed, or independently carry ownership. A modified Cave with an unchanged `CountyId` relies on the separately validated current/original Cave `AccountId`, avoiding the redundant County query seen in bulk revision publication; actual County moves still validate both current and original County IDs. It also preserves the staged-File ownership check, validates the missing current-domain entity families, and narrows the `AccountUser` exception to same-selected-account writes, verified invitation claims, platform-admin creation of a brand-new Account, or deletion-only invitation cleanup. Verified invitation claims are matched to a pending temporary invite by Account, normalized email, and the copied invitation-sent timestamp. If Mailgun accepted the invitation but the optional `InvitationSentOn` persistence failed, a null timestamp still matches only a pending invite whose timestamp is also null, and only when that Account/email/null-timestamp set resolves to exactly one temporary invitee; ambiguity fails closed. Cave/User permission transfer must originate from that exact temporary invitee. Permission definitions, global `UserPermission` and `FeatureSetting` rows, and global/default `TagType` writes require PlanarianAdmin. Authenticated `User` writes require self, same-selected-Account membership, or the verified temporary-invitation transition rather than treating a missing selected Account as blanket permission. The intentionally unauthenticated User registration/email-confirmation/password-reset paths remain explicit exceptions.

Validation includes:

- foreign-account Added/Modified/Deleted checks across every directly validated Account entity family and every Cave/Entrance association family;
- pre-populated foreign navigations as well as missing-navigation FK resolution, so validation cannot be bypassed by controlling tracking/navigation state;
- Account-A rows carrying Account-B Cave/County IDs, foreign rows attempting to relabel `AccountId` to Account A, and mutable child relationships attempting to move from an Account-B parent to Account A before save;
- atomic mixed owned + foreign batches;
- current-tenant success for direct AccountId, Cave, Entrance, File, and staged-File ownership mechanisms;
- platform-admin account creation, global/account permission behavior, rejection of arbitrary cross-account membership, verified invitation acceptance even while a different Account is selected, transfer only of the temporary invitee’s Cave/User permissions, successful acceptance with either a persisted or legitimately missing invitation-sent timestamp, fail-closed rejection when a missing timestamp would match multiple temporary invitees, rejection of unrelated permission theft, deletion-only invitation cleanup, rejection of no-account invitation modification, and focused registration/email-confirmation/password-reset/arbitrary-edit lifecycle cases;
- ordinary-account rejection for Permission-definition mutation plus global `UserPermission`, global `FeatureSetting`, and global/default `TagType` creation, global-to-tenant laundering, tenant-to-global promotion, and deletion, with direct platform-admin compatibility retained; authenticated no-Account User edits are also proven self-only rather than cross-user;
- bounded ownership-query tests for 600 Cave associations, 120 Entrance associations, and 600 County associations;
- an architecture contract that fails when a current Cave/account `EntityBase` family disappears from the interceptor switch or a newly added entity is not explicitly classified.

## GAP-007 — RESOLVED — Small real-browser smoke suite

**Priority: P1**

A deliberately small Playwright suite now covers the high-value browser contracts in both Chromium and WebKit. The final integrated `npm run test:browser:e2e` acceptance run completed 20/20 tests green against the production-built frontend and isolated real backend/database/storage topology:

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

The PDF smoke test proves the changed streaming contract end to end: real PDF.js rendering in Chromium/WebKit, the custom `X-Planarian-File-Stream-Session` header, cross-origin authenticated access/CORS, a deterministic authenticated `206`/`Content-Range` range probe, download behavior, and unauthorized/missing-file responses. It deliberately does not require every browser to issue a Range request for the small fixture; that choice is browser/PDF.js dependent. The corrected test passed in both browsers and in the final 20/20 integrated suite.

## GAP-008 — RESOLVED — Mixed-version / stale-SPA deployment safety

**Priority: P1 — validated on `test/cave-revisions-read-integrity`**

`CaveStaleClientSafetyIntegrationTests` round-trips old-client-shaped web JSON (including an omitted
`ExpectedRevisionId`) through controller/service boundaries and tests null, empty, whitespace-only,
malformed, and stale identifiers alongside valid current-client edits and new-Cave creation.
Its controller-action contract checks HTTP 400/409 mapping and
refuses a malformed PUT without a Cave ID. Real PostgreSQL snapshots assert rejected requests preserve
Cave fields, Entrances, Tags, Files, line plots, proposal/revision state, and current pointers across accounts.
An additional regression exposed opportunistic File cleanup running before a rejected update; older cleanup
debt now runs only after a successful direct Cave save, with both rejection and successful-retry paths tested.

## GAP-009 — RESOLVED — Ordinary read and authoring-context paths have no unintended writes

**Priority: P1 — validated on `test/cave-revisions-read-integrity`**

`CaveReadIntegrityIntegrationTests` fingerprints relevant PostgreSQL durable domain tables from an isolated
multi-account test database via an independent connection, including row versions (`xmin`) to detect
same-value writes. A negative-control regression proves the fingerprint detects such a rewrite, and the
read matrix also asserts that EF change trackers remain clean. It exercises Cave detail, list/search/advanced
search, map/line plots, File metadata and authorized response setup, revision history, proposal
list/detail/version, authoring contexts, County and Tag lookups, plus denied foreign-account reads.
Legitimate File access throttling, `User.LastActiveOn`, and other diagnostics are excluded from this domain fingerprint.

For an existing Cave without `CurrentRevisionId`, detail and list remain safe reads, while both authoring
contexts and revision-history operations fail closed without making any baseline. A rejected write also
cannot create late history. The tests include a separately initialized foreign account to catch cross-tenant
mutations. Cave list/search now explicitly constrains the Cave row by account when bypassing its global
query filter, matching the data-access architecture's tenant-qualification rule.

## GAP-010 — RESOLVED — Full archive generation/content and runtime safety

The archive runtime now has end-to-end content and coordination coverage against disposable PostgreSQL/PostGIS plus isolated Azurite. `ArchiveGeneratedContentIntegrationTests` calls `ExportService.WriteArchive()`, opens the actual `.tar.gz`, and validates parsed CSV/current-state semantics plus byte-identical File/line-plot payloads using multiple Caves/Entrances/tags, Unicode and multi-dot names, duplicate/path-collision candidates, a `..` path segment, zero-byte/binary/text/PDF-like/large payloads, missing blobs, staged Files, rejected/removed payloads, and a second tenant with overlapping storage keys. The large-payload fixture is above the 8 MiB memory-prefetch threshold, proving the bounded-memory direct Azure-stream path; small prefetch remains capped at eight such buffers, while large blobs are streamed one at a time with ETag protection. Archive listing is also proven not to create an absent account Blob container. Current File rows with a missing BlobKey, missing BlobContainer, or foreign BlobContainer now appear explicitly in `missing-files.csv` instead of being silently omitted or reading another tenant's same-key object.

Archive publication/runtime hardening adds job-scoped temp/final keys, eliminating the historical one-second timestamp key collision between rolling-deployment replicas. Current publication no longer relays the completed archive through the Planarian process. It stages source ranges Azure-to-Azure with `StageBlockFromUriAsync`, pins every range to the completed temp blob's ETag, and exposes the final archive only through a conditional `CommitBlockListAsync` with `If-None-Match: *`. Source authorization is a backend-only, exact-blob, read-only service SAS generated fresh per block with a maximum one-hour validity interval; it is never returned to the frontend and does not restore the removed browser-facing SAS access model. The block planner supports archives beyond the single `Put Blob From URL` limit and stays within Azure's 50,000-block / 4,000-MiB-per-block current service limits. If the SDK reports an error after publication may have reached storage, the coordinator compares temp/final bytes: identical content makes the retry idempotently successful, a missing final is a definite failure, different content fails closed, and an unverifiable state preserves the completed temp for later recovery/age-gated cleanup. The old async Blob Copy path is still understood: list/download/delete ignore non-success copy destinations, prune protects genuine pending copies while cleaning failed/aborted legacy destinations best-effort, and stale-temp cleanup will not delete the source of a pending legacy copy.

Runtime integration coverage proves cancellation cleanup and retry, successful readable publication, publication-conflict non-overwrite plus fresh-key retry, same-account serialization across coordinator instances, different-account concurrency, archive-vs-Reset exclusion, archive-vs-delete exclusion across current-version replicas, retention of four prior archives plus the current fifth, best-effort prune failure, tenant-scoped list/download/delete, and stale/recent/current temp behavior. The mixed-version storage test executes the exact historical timestamp-key + `StartCopyFromUriAsync`/wait algorithm pinned from `78dfce40^` concurrently with the current job-scoped publisher; it does not substitute two current-version coordinator instances.

Current exact-source archive-focused validation passes **45/45** unit policy/authorization/runtime tests and **26/26** archive integration tests, including the bounded-memory >8 MiB payload path and non-creating archive-list read. A fresh Release rebuild succeeds with **0 errors**; emitted warnings remain the repository's existing nullable/static-analysis class recorded elsewhere in this ledger, including unchanged `AccountRepository` lines rather than the archive-projection additions. Before the final bounded-memory/read-side-effect corrections, the package full backend unit suite passed **469/469** and the full disposable PostgreSQL/PostGIS + Azurite integration suite passed **558/558** with **0 skipped**; the integrated completion branch is intentionally rerun in full after this package is merged. Provider validation uses Azurite **3.37.0**, which added `Put Block From URL`; the prior 3.36.0 pin returned the expected emulator `APINotImplemented` response for the new publication path and was upgraded in both Compose and the integration fixture. The File-import and production-storage regression slice passes **21/21** after endpoint-scoping the static container-initialization caches, and the dedicated cache-key/archive-policy unit slice passes **39/39**.

Explicit product boundary: archive generation uses a shared account-maintenance lease, so ordinary Cave authoring is allowed to continue during generation. The export is current-state oriented but is not a transactionally frozen point-in-time snapshot across PostgreSQL and Blob Storage; a payload removed during generation can be reported in `missing-files.csv` rather than silently disappearing. Remaining deployment boundaries are live Azure account policy/networking/throttling/provider timing, and the mixed-version test executes the historical storage algorithm pinned from source rather than launching a compiled historical Planarian server binary.

## GAP-011 — RESOLVED — Archive is a current-state export, not a disaster-recovery backup

The product/architecture decision is explicit in `docs/architecture/destructive-operations.md`: the account Archive exports current Cave/Entrance state, current live Cave Files, current line plots, and the missing-file manifest where needed. It does not promise revision/proposal/review history or storage-cleanup-debt recovery, and no restore workflow is defined. GAP-010 now provides the generated-content and runtime validation for that contract.

## GAP-012 — RESOLVED — Hidden/disabled feature fields preserve existing data

The cave-authoring field-persistence package merged before the current baseline. `AddCaveComponent.form-contract.test.tsx` proves pre-existing feature-gated Cave/Entrance/File/line-plot values remain in the submitted aggregate when controls are hidden. `EditCavePage.test.tsx` proves direct edit submits preserved unmounted values, and Suggest/Revise page tests prove preview/submission use the same complete draft rather than only mounted fields. The backend field-persistence matrix separately exercises full direct/proposal publication state.

## GAP-013 — RESOLVED — Account switching and frontend cache isolation

**Priority: P1/P2**

Account-dependent settings/reference caches are now scoped to the active account, and account switching refreshes target-account state before committing the new runtime account. Unit/component tests and the Chromium/WebKit browser suite validate the complete switch workflow:

- start in Account A;
- load feature settings/tags/counties/Cave authoring data;
- switch to Account B without a full reload;
- ensure Account A options/IDs are not actionable in Account B;
- switch back and verify correct reuse/invalidation;
- submissions after the switch always carry the current account context.

The real-browser tests additionally prove that dirty Account A authoring state cannot remain actionable during a switch and that submissions after switching use the target account context. Cross-browser validation also exposed and fixed the event-listener timing race by registering the switch coordinator before paint with `useLayoutEffect`.

## GAP-014 — Safety-critical lifecycle complete; inactive-account sweeping explicitly deferred

**Priority: P2 storage/operations hygiene; not a merge blocker.**

Expired unlinked staged Files are cleaned **opportunistically** on subsequent staging/temp uploads. Relational
removal commits durable `UnpublishedFileRemoval` responsibility before external deletion; failed provider
deletion remains retryable. Proposal-linked staging is protected from ordinary expiration discovery, rejected/
resolved request cleanup follows the current retention contract, and published Files have `ExpiresOn = null`
and cannot be selected for staging expiration cleanup. Focused PostgreSQL tests cover expiration, pending
references, provider failure/debt, published immutability, and post-commit cleanup.

There is **no periodic sweep for completely inactive accounts**. Such an account may retain expired unlinked
staging until another cleanup opportunity arises. This is an accepted post-merge storage-hygiene/operations
limitation, not an unproven Cave revision correctness or integrity property; durable
`docs/architecture/files-and-storage.md` and `docs/cave-revisions.md` record the same limitation.

## GAP-015 — RESOLVED — File/object retry and race semantics

**Scope:** Publication, ownership, duplicate decisions, immutable object writes, and durable post-commit cleanup. GAP-014's inactive-account sweeper remains deferred.

The publication contract is **at-most-once relational mutation**, not replaying a successful HTTP result. If the response is lost after commit, a duplicate approval or terminal decision receives a controlled conflict/already-reviewed response; the client can reload the request's terminal status and approved revision. Exactly one revision and one review decision event are committed. The existing-Cave revision guard and PostgreSQL row locking serialize competing publications; creating a new Cave also locks the request before allocating its publication. Repeated identical direct saves use semantic no-op suppression.

A staged File can be claimed by one request only. Competing claims lock the File row, recheck existing request bindings, and fail the loser without publishing or copying its object. Published File identity and object address do not change on promotion; no duplicate File row/association is created. Concurrent publication/removal uses the same Cave revision conflict boundary; stale approval/direct-edit concurrency and published-File removal/cleanup have focused PostgreSQL tests. A dedicated mixed-operation File removal versus staged approval test was not retained because the attempted synchronization did not establish a deterministic interleaving; do not count that exact combination as independently tested.

Object creation is **create-only**, never replace-in-place. The provider-neutral `IObjectStorage` adapter's Azure implementation uses `IfNoneMatch = ETag.All`; an ambiguous successful create followed by a repeated upload receives a non-success condition response, rather than overwriting stored bytes. Existing Azurite adapter contract coverage proves immutable bytes and idempotent missing/delete behavior. If object creation or the subsequent relational publication appears to fail, upload-once File paths now queue tenant-qualified `UnpublishedFileRemoval` cleanup debt and perform a safe live-reference check before deleting bytes. An uncertain commit may in fact have published a File; cleanup preserves that object's bytes and clears stale debt without replaying tracked rolled-back mutations. Failed external cleanup retains debt; cleanup checks live references with explicit cross-account and unfiltered same-account queries. A database outage preventing cleanup-target persistence remains an external coordination limitation and is logged, not mistaken for a successful upload.

Post-commit object deletion is best effort with durable tenant-qualified `StorageCleanupTarget` debt recorded in the relational publication transaction. Missing objects are successful idempotent cleanups, while failed acknowledgement leaves debt for later bounded retries. Cleanup rechecks live File references before deletion, including cross-account references, so stale debt cannot delete a live object. No background sweeper is added.

Additional GAP-015 PostgreSQL tests: `ConcurrentDuplicateApprovalsCommitOneRevisionAndOneDecision` (including File ownership/bytes and a fresh retry after successful commit), `ConcurrentProposalsCannotClaimTheSameStagedFile`, and `AmbiguousStagedObjectCreateDoesNotLeaveUntrackedBytes` (immediate cleanup and durable debt/retry), plus `AmbiguousDirectCaveUploadCannotPublishOrLoseCleanupResponsibility` (failed direct upload does not publish or lose cleanup debt). Final integration also adds `LostCommitAcknowledgementDoesNotDeletePublishedFileOrReplayTrackedMutation` (confirmed commit with lost acknowledgement keeps File bytes/revision) and `LostStagingSaveAcknowledgementPreservesCommittedFileAndObject` (confirmed staged row keeps bytes despite lost acknowledgement). Existing focused coverage includes approval/rejection races, stale/direct publication conflicts, File publication without copy, stale cleanup/live-object guards, acknowledgement failure and retry, and immutable create-only Azure adapter behavior. This resolves the specified relational race/retry contract without a production redesign.

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
- [x] Refresh durable completion docs/checklists to accepted implementation truth; archive the plan as historical execution guidance.
- [x] **GAP-001:** full current-main database preservation census, including a strict 41-table v32 migration boundary and separate rollout preservation boundary.

## P1 — Strongly recommended before merge because failures can be silent or cross-cutting

- [x] External-cleanup failure/retry safety for hard Cave/File/staging/proposal/archive-cleanup workflows (GAP-004), with full archive runtime covered by resolved GAP-010.
- [x] Azurite contract tests for production `AzureBlobObjectStorage`.
- [x] Global `SaveChangesInterceptor` operation/tenant compatibility matrix.
- [x] Small real-browser smoke suite, including WebKit for critical workflows.
- [x] Real-browser PDF.js custom-header/range/authentication test.
- [x] Old-client/missing-`ExpectedRevisionId` no-mutation regression.
- [x] Read-only GET/authoring-context no-side-effect contract plus fail-closed uninitialized-Cave coverage.
- [x] End-to-end archive `.tar.gz` generation/content validation (GAP-010).
- [x] Full end-to-end archive runtime cancellation/failure/pruning/account-isolation and mixed-version publication tests (GAP-010).
- [x] Explicit archive-vs-backup product contract (current-state export), with the runtime contract validated under GAP-010.
- [x] Hidden/disabled feature-field preservation tests.
- [x] Account-switch/client-cache isolation validation.

## P2 — Hardening that can be consciously deferred if documented

- [ ] **Explicitly deferred GAP-014:** periodic cleanup/discovery for completely inactive accounts (not safety-critical active lifecycle).
- [x] GAP-015 object-store retry/race/idempotency hardening, including ambiguous object creation and post-commit acknowledgement.
- [ ] Clean changed-file frontend lint warnings where low risk.
- [ ] Consider post-merge workflow-oriented split of very large services only if it materially improves maintenance.
- [ ] Broader browser coverage after the small critical smoke suite is established.

# 12. Historical final validation sequence

The steps below were the intended completion-wide validation plan. Recorded commands and results in the
final integration evidence section distinguish actual final-tree executions from earlier SHA-pinned
broad suites; no broad run is claimed merely because it appeared on this checklist.

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
- [x] Existing-Cave edit without expected revision fails safely.
- [x] Stale edits cannot silently overwrite.
- [x] No obsolete standalone GeoJSON publication endpoint/control remains.

## Destructive-operation safety

- [x] Reset behavior is tested against a dense dependency graph (merged destructive-safety package).
- [x] Reset does not affect the second tenant in the tested relational/blob graph (merged destructive-safety package).
- [x] Post-commit storage failure has truthful user/API semantics (merged destructive-safety package).
- [x] Fate of stored archives during reset is intentional and documented (merged destructive-safety package).
- [x] Durable cleanup retry and cross-replica API/archive admission tested; independent write paths still need deployment audit.
- [x] Hard-delete/external-cleanup failure semantics are safe/retryable; generated-archive runtime behavior is validated under resolved GAP-010.

## Storage/File safety

- [x] Fake-storage business tests for the affected cleanup workflows remain green in the focused and aggregate-cleanup regression suites.
- [x] Production object-storage adapter has a real automated Azurite contract suite.
- [x] Immutable Cave/File object creation is provider-enforced create-only; a retry/path bug cannot silently overwrite an existing object address, and failed create does not compensate-delete pre-existing bytes.
- [x] Staged/published/removed-history/expired File lifecycle remains mutually exclusive and proven by PostgreSQL lifecycle, cleanup, and GAP-015 tests. Periodic discovery for fully inactive accounts is explicitly deferred under GAP-014.

## Cross-cutting compatibility

- [x] Global save-interceptor matrix is green or equivalent evidence exists.
- [x] Hidden feature fields survive unrelated edits.
- [x] Account switching cannot reuse actionable foreign-account UI data.
- [x] Ordinary reads do not unexpectedly mutate durable state.
- [x] Archive current-state/backup semantics are explicit and the generated-archive/runtime contract is validated under GAP-010.

## Browser/runtime

- [x] Critical authenticated browser smoke test passes.
- [x] PDF viewer works through the real custom-header/range-request path.
- [x] Nested-route hard refresh works.
- [x] Stale-edit conflict UX works.
- [x] Unsaved-change browser behavior works.
- [x] Critical WebKit/Safari-equivalent smoke path passes if the browser harness supports it.

## Documentation/reporting

- [x] `docs/cave-revisions.md` describes implemented truth.
- [x] Completion plan README/checklist describes accepted implementation and retains historical normative gates.
- [x] Ledger reconciled against Agent 2 architecture/safety and Agent 3 browser/CI audits.
- [x] Final integration test/build commands and results are recorded separately from historical package runs.
- [x] Validation claims distinguish source/test existence, earlier SHA-pinned execution, and final-tree runs.

# 14. Important review limitations and boundaries

The completed adversarial review was extensive but was not equivalent to manually clicking every UI screen.

Specifically:

- no live production/staging/development application database was inspected or modified;
- disposable repository-owned PostgreSQL/PostGIS databases were used for integration/migration experiments;
- no Azure production resources were modified;
- live Azure Blob Storage was not exercised; business workflows use a deterministic fake and the production adapter is validated separately against Azurite;
- real-browser E2E ran 20/20 green on audited `52f2c593` (10 Chromium, 10 WebKit), not rerun merely for these docs/integration hardening changes;
- no full real account archive round-trip/restore was proven;
- the synthetic migration preservation census now explicitly seeds and checks every current-main v32 model table; it is still synthetic data rather than a sanitized restored real-world database;
- dependency-audit results were informational and inherited vulnerabilities remain a separate security-maintenance concern.

# 15. Future maintenance / release handoff

The completion branch is now ready for an independently reviewed PR/merge to `main`. Do not treat
this historical package as an instruction to re-add browser/PDF tests, repeat account-switch work,
or build GAP-014's periodic sweeper before merge. The existing archive/Reset warning remains important:
account containers include archives and import transport, not only Cave Files.

Potential post-merge work: optional inactive-account staged-File discovery/sweep (GAP-014);
independent-writer and live Azure deployment integration; broader browser user journeys and
low-risk existing frontend lint debt. GAP-015's mixed simultaneous published-File removal versus
staged approval was reviewed, but a deterministic dedicated interleaving test was not retained;
do not misrepresent it as a passed test.

# 16. Final integration verdict

**ARCHITECTURE/SAFETY/BROWSER ACCEPTANCE COMPLETE FOR `feature/cave-revisions-completion`.**
No independent P0/P1 blocker remains after GAP-015's object-create and uncertain-database-commit
compensation hardening. The supported upload-once File cleanup is durable, reference-checked, and
tested against lost acknowledgements. Historical broad suites and browser E2E retain their
exact commit provenance; final focused integration evidence is listed separately.

**Not merged into `main`.** Merging to `main` is a separate release action.
