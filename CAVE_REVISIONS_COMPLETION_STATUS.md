# Cave Revisions Completion Status

**Branch:** `feature/cave-revisions-completion`
**Status:** Cave Revisions is integrated and validated. The coordinated deployment pipeline is implemented but not yet activated; Development environment setup and a Development rehearsal remain required before Production.
**Ledger last refreshed:** 2026-11-01
**Completion HEAD reviewed:** `c5d68f0d59d689ac63c67774de463a40ca9cf479` (includes the committed branch; additional staged/unstaged work is identified separately below)
**Current `main` reviewed:** `e342691c82f1bb6549be903d3a8f94a5d2560ac2`

## Current verdict

The Cave revision/proposal/File/import implementation is complete for this branch. No known P0/P1 feature blocker remains. The branch has not been merged into `main` and no hosted Development or Production deployment was performed by the validation work recorded here.

The remaining release work is operational rather than feature implementation:

- configure the required Development/Production GitHub Environment values and deploy-runner connectivity;
- run the coordinated deployment through Development, including the one-time Cave revision-history bootstrap;
- verify the stopped-App-Service deployment/maintenance sequence in Development before Production activation;
- deploy Production only from an approved `planarian-release-v*` tag when ready.

GAP-014 remains deferred: completely inactive accounts do not have a periodic staged-File/ordinary-cleanup sweeper. Active authoring and explicit destructive workflows retain durable retry behavior.

## Durable architecture

Current behavior is documented in:

- [`ARCHITECTURE.md`](ARCHITECTURE.md)
- [`docs/cave-revisions.md`](docs/cave-revisions.md)
- [`docs/authorization.md`](docs/authorization.md)
- [`docs/data-access-architecture.md`](docs/data-access-architecture.md)
- [`docs/architecture/files-and-storage.md`](docs/architecture/files-and-storage.md)
- [`docs/architecture/imports.md`](docs/architecture/imports.md)
- [`docs/architecture/destructive-operations.md`](docs/architecture/destructive-operations.md)
- [`docs/deployment.md`](docs/deployment.md)
- [`docs/testing.md`](docs/testing.md)

Historical implementation plans, resolved defect ledgers, and package-by-package narratives remain in Git history rather than being maintained as competing current-state documentation.

## Release boundary

### Database migration

The feature ships as one migration after the unchanged v32 baseline:

`20260831132210_v32` → `20261029194500_CaveRevisionsCompletion`

The earlier feature-only migration chain was consolidated before release. The final EF model snapshot has no pending model changes. Upgrade/downgrade preservation, proposal downgrade refusal, feature-setting provenance, permission identity, and v32 data-preservation behavior are covered by the migration integration suite.

### One-time Cave revision-history bootstrap

Legacy Cave revision history is initialized by `Planarian.Operations initialize-cave-revision-history`, invoked through the registered `cave-revision-history` deployment task. The API does not initialize rollout history during ordinary startup.

The task is resumable while only rollout baselines exist and fails closed after live revision publication has begun. After every required environment has completed the rollout, the Cave-specific task/operation can be retired; the generic Operations host remains reusable deployment infrastructure.

### Coordinated deployment

`.github/workflows/Planarian.Deploy.yml` and `scripts/deploy/plan.py` select only the API, web, migration, and registered release-task work required since the last successful coordinated release for the target environment.

Development may deploy `main` automatically or a trusted manually selected ref. Production is a separate manual release from a `planarian-release-v*` tag. Durable deployment state uses GitHub Deployment records so a failed or interrupted attempt cannot make already-applied migration files appear editable on the next release.

See [`docs/deployment.md`](docs/deployment.md) for setup, environment protection, secrets, runner requirements, rollback rules, and release procedure.

## Validation evidence

Validation claims below are tied to the exact committed or local staged state that produced them. Historical evidence is retained as historical evidence; a focused post-suite fix does not imply unrelated suites were rerun.

### November 1 working-tree validation

At `c5d68f0d` plus the staged File/import refactor as it stood at the comprehensive run, completed validation included:

- Backend unit/architecture tests: **533/533 passed**.
- Disposable PostgreSQL/PostGIS integration tests (ordinary persistence, migrations, golden compatibility, and supported scale): **730/730 passed**, no skips.
- Frontend Jest: **405/405 passed** across 80 suites; TypeScript checking and production frontend build passed (with existing tooling warnings).
- Deployment planner/task tests: **72/72 passed**; deployment shell syntax, example environment, generated client routes, and repository documentation validated.
- Release solution build passed with **0 errors, 144 compiler warnings**; EF reported **no pending model changes**.
- The later static review found a downgrade edge case for a migrated CSV source whose original File type was legitimately deleted after upgrade. Its regression first failed with a PostgreSQL FK violation, then passed after fallback/fail-closed handling was added; **3 focused migration tests passed**. The full 730-test suite was run *before* this narrow migration fix and was not repeated after it.
- A subsequent audit confirmed two additional narrow edge cases with failing-before/passing-after PostgreSQL regressions: concurrent File-import finalizations shared a mutable cached response (the HTTP controller changes its trace ID), and retained CSV download leaked a `FileNotFoundException` instead of 404 when the object disappeared between storage metadata and stream opening. The affected File-import class passed **10/10** after response isolation; the retained-source download slice passed **2/2** after the missing-object correction. These focused results do not replace another full-suite run.
- An additional broad review reproduced a cancel-all/finalization race: a session selected for cancellation could enter `Finalizing` before its gate was acquired, causing the batch cancellation to fail. Cancel-all now rechecks under the gate and skips the publishing session while canceling other eligible sessions. The complete File-import integration class passed **11/11** after the fix and after expanding the race regression. The prior 730-test run predates this fix.

This evidence does not include a fresh Chromium/WebKit browser-E2E run, hosted Development rehearsal, live Azure validation, or Production deployment. The staged refactor and subsequent local corrections are not committed merely because they passed tests.

### Deployment and rollout

- Deployment planner/task/workflow suite: **72/72 passed**.
- Directly affected rollout/migration/reference-scale integration slice on the final isolation design: **12/12 passed**, no skips.
- Cave rollout/scale integration package: **5/5 passed**.
- Relevant permission/save-interceptor integration package: **15/15 passed**.
- Relevant dependency/architecture contract package: **42/42 passed** before the final isolation tightening.
- `Planarian.Library` and `Planarian.Operations` affected projects built without warnings/errors during the final deployment validation.

### Migration

- Final v32 → Cave Revisions migration/foundation slice: **33/33 passed** against disposable PostgreSQL/PostGIS.
- EF reports no pending model changes.
- Forward/reverse SQL generated by the consolidated migration was compared with the pre-consolidation feature chain; after migration-history/transaction boilerplate, the SQL bodies matched.

### Final feature integration

At integration commit `f004ae48fcd88fbc415bb9cb4363794c2d52f418`:

- focused Cave asset/proposal/cleanup concurrency integration slice: **61/61 passed**;
- hidden-live-File cleanup protection regression: **1/1 passed**;
- focused architecture/identifier/File/revision unit slice: **69/69 passed**;
- Release solution build: **0 warnings / 0 errors** for that validated tree.

The final GAP-015 hardening covers ambiguous object-create/database acknowledgements by reconciling through durable cleanup debt and live-reference checks instead of direct compensation deletion.

### Browser/runtime

At audited commit `52f2c593`:

- Playwright acceptance: **20/20 passed** — **10 Chromium**, **10 WebKit**;
- browser-harness safety regression passed;
- authenticated account switching/cache isolation, stale-edit conflicts, unsaved navigation/reload protection, nested-route refresh, PDF.js authenticated delivery/CORS/range behavior, proposal navigation, CSP/session behavior, and dirty-account switching were exercised.

### Broader historical confidence

At validated pre-final-hardening trees, the branch also passed:

- backend unit suite: **521/521**;
- disposable PostgreSQL/PostGIS integration suite: **651/651**, no skips;
- frontend Jest: **395/395**;
- TypeScript check and production frontend build.

These counts are historical evidence, not claims about execution after every later narrow or documentation-only change.

## Accepted limitations and follow-up

- **Inactive-account cleanup discovery:** no periodic sweeper in this release. Durable cleanup debt remains safe and retryable; completely inactive accounts may retain expired staging or queued cleanup until later activity or an explicit destructive workflow.
- **Live Azure deployment behavior:** business semantics and the Azure storage adapter are covered with deterministic tests/Azurite, but live Azure policy/network/retention behavior remains a deployment boundary.
- **Independent writers:** correctness assumes Planarian's supported writers participate in the documented locking/concurrency contracts. A separately deployed non-cooperating writer remains outside the validated boundary.
- **One precise interleaving:** simultaneous published-File removal versus staged proposal approval was reviewed against the locking/source design, but no dedicated deterministic interleaving test is claimed.
- **Database outage during cleanup-debt creation:** if PostgreSQL itself is unavailable, new cleanup responsibility cannot be persisted and requires operational recovery; the code does not pretend otherwise.

## Release closeout

Before activating Production:

1. confirm the GitHub Environment configuration and deployment runner satisfy [`docs/deployment.md`](docs/deployment.md);
2. execute and inspect a Development coordinated release from the intended ref;
3. verify migration, Cave history bootstrap, API restart, web deployment, and post-deployment smoke behavior;
4. retain the one-time Cave bootstrap task until every environment that needs legacy-history initialization has completed it;
5. use the protected Production tag release path only after the Development rehearsal is accepted.

No additional Cave Revisions feature work is required solely because older resolved GAP/P0/P1 entries existed in previous versions of this ledger.
