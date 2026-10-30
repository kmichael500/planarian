# Coordinated Planarian deployments

**Status:** Implemented in `feature/cave-revisions-completion`; **not live until merged,
pushed, and the GitHub environment settings are configured**. This is stage 1:
no App Service slots or cross-service atomic swap.

## Contract

[Planarian.Deploy.yml](../.github/workflows/Planarian.Deploy.yml) replaces the
independent API/web development and production deployment workflows. It runs
automatically on **pushes to `main`** (Development). A manual Development
dispatch may use **any selected branch/ref**. Production uses a **manual
`workflow_dispatch` from a `planarian-release-v*` tag** and is independent
of Development deployment history. A single workflow serializes deployments
per environment with `cancel-in-progress: false`.

Release state is stored in GitHub's native **Deployment** records rather than
workflow-run history. Immediately before the first hosted mutation, the deploy
job creates a task-scoped `planarian-coordinated-release` record in an
internal tracking environment
(`planarian-release-state-development` or
`planarian-release-state-production`) and marks it in progress. The last
record whose current state is `SUCCESS` or GitHub's equivalent `ACTIVE` is the component-diff baseline.
Every newer record is conservatively treated as an attempted mutation boundary
for EF-migration immutability. This remains safe if a job fails or is cancelled
before it can write a final status: a missing/in-progress state is not success.

GitHub retains a deployment's **current state** on the Deployment object even
after older Deployment Status records age out, so this state is not coupled to
the shorter workflow-run/status-log retention window. The planner paginates and
validates the complete tracking-environment Deployment connection before using
it.

The release planner, [`scripts/deploy/plan.py`](../scripts/deploy/plan.py),
calculates changes from the latest successful coordinated Deployment SHA to
the current release SHA, not only the preceding Git commit. It compares the
Git tree; no separate deployment database or application-side release state is
introduced. If there is no prior successful tracking Deployment, the workflow
conservatively rebuilds/deploys API, web, and pending EF migrations.
Development may move
between branches: the planner compares the previously deployed tree directly
with the selected ref. Already-deployed EF migration files still cannot be
modified or removed, so a branch-only migration can intentionally make a later
switch back to a ref without that migration fail closed until the schema/ref is
reconciled. Production remains stricter: the previously successful Production
SHA must be an ancestor of the selected release tag, preventing an accidental
divergent-history rollback.

The human-facing run-name still identifies the selected Development/Production
ref, but it is **not** deployment state. The planner reads only task-scoped
GitHub Deployment records from the selected environment's internal tracking
environment. Records are ordered by Deployment creation time; a rerun creates
a new tracking record at the time that attempt can actually mutate the
environment. If a recorded release SHA is not available in the full-history
checkout, or GitHub returns incomplete/inconsistent Deployment pagination, the
planner fails closed rather than guessing deployment state.

| Change | Deployment |
| --- | --- |
| `Planarian.Web/**` | Web only |
| `Planarian/Planarian/**` | API only |
| `Planarian/Planarian.Library/**` | API; a registered release task may add requirements only for paths explicitly declared by that task |
| `Planarian/Planarian.Operations/**` | No standalone hosted component; only explicitly registered operation subpaths (such as `CaveRevisionHistory/**`) select their task |
| `Planarian/Planarian.Migrations/**` | Database migration bundle only |
| `Planarian/Planarian.Model/**` | API and database (conservative) |
| `.config/dotnet-tools.json` | Database migration bundle |
| `Contracts/**`, shared client-route generator | API and web |
| Frontend CSP/PDF asset build scripts | Web |
| Registered one-off release task | Whatever API/web/database requirements its manifest declares |
| Coordinated workflow or generic executable deployment tooling | API and web (database only when schema/model changes require it) |
| Deployment tests, documentation, and unrelated paths | No deployment |

`force_all` is *additive* for API, web and migrations. It deliberately does
not re-run one-off release tasks unless their trigger, manifest, or script
changed (or the task explicitly opts into the environment's first coordinated
release).

One-off operations live under `scripts/deploy/tasks/`. Each task has one small
JSON manifest declaring its trigger paths, component requirements, whether it
needs an API maintenance window, and whether it must run on the first
coordinated release. The permanent planner/workflow is task-agnostic and runs
registered scripts through generic lifecycle phases. Every file in the task
directory must belong to a registered task; stray or path-escaping task files
fail closed.

A task script receives `preflight` and `run` phases. A maintenance task also
receives `before-migrations` and `after-api-deploy`. `preflight` validates
configuration without mutating hosted resources; `run` performs the
repeat-safe one-off operation. Maintenance phases own any intentional
quiescence/resume behavior. Keep domain-specific work in the task or
application command rather than teaching the permanent planner what the task
means.

A completed one-off should be retired after it has successfully run in every
environment that still needs it. For the Cave rollout, that means deleting its
manifest/script and then removing the Cave-specific Operations command and
`CaveRevisionRolloutRepository`; the generic `Planarian.Operations` host and
permanent `CavePublishedSnapshotRepository` remain. The API has no rollout-specific
startup code to clean up. Git history preserves exactly what executed.

All selected components are **built before any hosted mutation**. Then the
order is: environment/task preflight -> Azure login/read-only hosted checks (if
API) -> create the durable coordinated Deployment record -> optional registered
maintenance preparation -> EF migrations -> registered one-off tasks -> deploy
API -> finish registered maintenance -> verify API health -> deploy web ->
verify web health -> mark the durable Deployment successful.
An API deployment or health-check failure blocks the frontend deployment.
A web-only release skips Azure API sign-in and all database operations.

The website and API are not atomically swappable across two Azure services.
Maintain compatibility between adjacent API/frontend versions, including
already-open browsers. Normal database releases apply migrations **before**
the new API is deployed while the previous API may still serve requests, so
those migrations must be compatible with both adjacent API versions (the
usual expand/contract pattern). A migration that cannot satisfy that contract
must use an explicitly reviewed maintenance/quiescence release instead of the
ordinary path.
Do not automatically reverse database migrations when application deploys
fail. A migration file seen by **any coordinated tracking Deployment newer
than the last success** is treated as immutable, regardless of whether that
attempt's current state is failure, in-progress, or missing. The tracking
record is created before maintenance/migration/application deployment begins,
so once that boundary exists the workflow cannot prove that an interrupted
attempt did not already commit to PostgreSQL. Edit neither that migration nor
its designer; add a new migration instead. This deliberately errs on the safe
side after a partial release.

The model snapshot may change with a new migration, but a snapshot-only change
is rejected because it can hide a missing database upgrade. This check is
applied against both the last successful coordinated release and every newer
attempted release, so a migration that may already have run cannot be reused to
justify a later snapshot change. PR validation also rejects edits to migration
files already present on the PR's `main` base. A failed release is *not* the
component-selection baseline: the next run still revisits every
application/web/database change since the previous full success, while
preserving migration safety from every newer tracking Deployment.

## GitHub environment setup (required before enabling release)

Keep environments named exactly `Development` and `Production`. Each
environment needs the following values; their names align with the earlier
API/web workflows where possible.

| Environment value | Type | Purpose |
| --- | --- | --- |
| `AZURE_CREDENTIALS` | Secret | Existing Azure login credentials (API deployment and quiescence) |
| `AZURE_WEBAPP_NAME` | Secret | Existing target API App Service name |
| `AZURE_STATIC_WEB_APPS_API_TOKEN` | Secret | Existing Static Web App deployment credential |
| `DATABASE_MIGRATION_CONNECTION_STRING` | Secret | **New:** limited-scope PostgreSQL deployment identity used by the EF bundle and database-backed `Planarian.Operations` commands |
| `AZURE_RESOURCE_GROUP` | Variable | Existing Azure resource group |
| `REACT_APP_API_ORIGIN_MAPPINGS` | Variable | Existing frontend API origin mapping |
| `REACT_APP_UMAMI_WEBSITE_ID` | Variable | Existing production website analytics ID; required for production web |
| `AZURE_API_HEALTH_URL` | Variable | **New:** HTTPS unauthenticated API health/smoke URL; required for API and database releases |
| `AZURE_WEB_HEALTH_URL` | Variable | **New:** required HTTPS website smoke URL (only for web releases) |
| `DEPLOY_RUNNER` | Repository variable (optional) | Trusted **Linux x64** self-hosted runner label when Azure PostgreSQL is private; default is `ubuntu-latest` |

The workflow also creates GitHub Deployment records in internal tracking
environment names `planarian-release-state-development` and
`planarian-release-state-production`. These records hold release-state
history only; they are not where application secrets/variables or protection
rules live. The workflow's `GITHUB_TOKEN` has `deployments: read` in the
planning job and `deployments: write` only in the environment-gated deploy
job. Do not manually delete/inactivate these task-scoped records: they are the
durable source of truth for selective deployment and migration immutability.
Creating Deployment records emits GitHub's standard `deployment` and
`deployment_status` events. This repository has no workflow listening to
either event; before activation, verify that any separately installed GitHub
deployment integration also ignores the
`planarian-coordinated-release` task/internal tracking environments unless it
is intentionally meant to observe them.

Migration bundles and registered release tasks execute **on the GitHub runner**;
the runner must have network access to every selected task's dependencies. The
current Cave task needs only PostgreSQL plus Azure control-plane access for the
App Service maintenance window. It does **not** receive Azure App Configuration,
blob, email, authentication, or other normal web-application configuration.
A private PostgreSQL server may require a trusted
self-hosted runner on the VNet, selected by the repository-level
`DEPLOY_RUNNER` variable. That runner must also have the Azure CLI, GitHub CLI, Bash, `curl`, `zip`,
and GitHub Actions runner v2.327.1 or later for Node 24 action support. Azure
Login v3 uses the Node 24 action runtime; keep self-hosted runners current before selecting them
with `DEPLOY_RUNNER`. Restrict self-hosted deployment runners to trusted,
reviewed release refs. **Never open a private production database to the public
internet just to make GitHub-hosted runners work.**

The pipeline fails before mutation if required values are missing. All action
dependencies touched by this deployment/PR package are pinned to reviewed full
commit SHAs rather than mutable major-version tags. Checkout credentials are
not persisted in the deployment jobs. Azure CLI's default stdout is disabled
for release jobs so routine command output is not copied into Actions logs;
commands that intentionally read a value request an explicit output format.
The Static Web Apps action is not given a GitHub repository token because this
deployment workflow never manages PR comments. The release workflow
intentionally supports Linux x64 deploy runners and checks that platform before
any selected component is deployed. Database releases additionally build a
self-contained `linux-x64` EF bundle. At execution time, the scoped migration
secret is exposed to the EF bundle and `Planarian.Operations` only as
`ConnectionStrings__default`; it is not placed in either process command line.
The database schema credentials must not be added to frontend builds, committed,
or installed in the normal API process.

The former `AZURE_WEBAPP_PUBLISH_PROFILE` secret is not used by the
coordinated workflow: `az webapp deploy` uses the existing Azure login
identity. Stage 1 preserves the existing `AZURE_CREDENTIALS` service-principal
secret so activation does not depend on a simultaneous Azure identity
reconfiguration. Azure Login currently recommends OIDC/federated
authentication because it avoids a long-lived cloud credential; migrating this
workflow to OIDC is a worthwhile follow-up once the Azure federated identity is
configured. That change would add `id-token: write` and replace `creds` with
secret-backed client/tenant/subscription IDs. Do not enable those permissions
before the Azure side exists. After the Development rehearsal confirms the new
deployment path, remove the old publish-profile secret if no other workflow
still consumes it.

`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is an App Service prerequisite for
API releases. Configure it once in Azure; the release workflow verifies it
rather than rewriting the setting on every deployment, because changing App
Service settings restarts the application.

For Production, configure the GitHub Environment's available reviewers,
branch/tag restrictions and secret access as the repository plan permits.
A Production release that **includes database changes** also requires
`backup_confirmed=true`; a web-only or API-only release does not. This
is an explicit **operator attestation, not automatic verification of a
database backup**. Validate the backup/recovery point and migrations before
approving a release. The Azure App Service database and blob-store rollback
are separate concerns.

## Manual Development releases

The normal Development source remains `main`, which deploys automatically.
For an ad-hoc Development release, after the workflow exists on the repository's
default branch, open **Planarian: Coordinated deployment**, choose any branch in
GitHub's **Run workflow** ref selector, and use `target=Development`. The
selected ref becomes the actual successful Development baseline. The next
Development run compares its tree against that deployed ref, even when the
histories diverge. GitHub Environment branch/tag protection still applies; if
the Development environment restricts deployment refs, configure that policy to
allow only the trusted repository branches you intend to deploy manually.
Because a manually selected ref supplies the workflow and application code that
will receive Development environment secrets, do not deploy unreviewed/fork
refs through this privileged workflow. Keep the last successfully deployed
Development ref/commit reachable until a later successful Development release
supersedes it; a tracking Deployment whose commit is unavailable in the
full-history checkout fails closed.

Switching branches does not make database migrations reversible. If an ad-hoc
branch applies a migration that a later ref does not contain unchanged, the
later deployment fails closed rather than pretending the shared Development
database rolled back. Use branch-only migrations against the shared Development
database only when that consequence is intentional.

## Production release procedure

1. Finish the release's checks, especially EF pending-model and isolated
   PostgreSQL migration/rollout tests. Verify backup and recovery readiness.
2. Create a `planarian-release-v*` tag on the exact commit you intend to
   release and push it. **Tags alone do not trigger Production deployment.**
   Production does not require that commit to have deployed to Development.
   Protect the `planarian-release-v*` namespace with repository rules against
   force-updates/deletion, and configure the Production Environment to permit
   only that tag pattern.
3. Dispatch the GitHub workflow **Planarian: Coordinated deployment** at
   that tag with `target=Production`, and with
   `backup_confirmed=true` when database changes are included. Example:

   ```bash
   gh workflow run Planarian.Deploy.yml \\
     --ref planarian-release-v1 \\
     -f target=Production \\
     -f backup_confirmed=true
   ```

4. Monitor the single deployment job. Confirm the API health check and
   frontend deployment succeed. The run summary lists selected components
   and the exact commit. Use `force_all=true` for a complete redeploy.
   Selective Production deployments compare to the **last successfully
   deployed Production release**, never to Development history.

The historical `planarian-api-v*` and `planarian-web-v*` tags remain valid
Git history markers; they are not rewritten or deleted. The first coordinated
Production release has no task-scoped Production tracking Deployment, so it
intentionally performs a conservative full release and starts both the new
`planarian-release-v*` tag history and durable Production deployment-state
history from there.

An API-only or web-only change does not touch the database. A migration-only
release doesn't rebuild or restart the API unless a registered task also
requires it; after the migration it still checks API health before declaring
the release successful. A modified EF snapshot without an actual migration must be
rejected by PR migration-model validation, not fixed by arbitrary deploy SQL.

## First Cave Revisions rollout

This is an intentional **maintenance-window release**, not an ordinary
backward-compatible migration. The existing API's Cave/Entrance/File,
line-plot and import writes must be quiesced while creating system
baseline revisions. The migration is
`20261029194500_CaveRevisionsCompletion`. Its one-off behavior is registered
only in [`cave-revision-history.json`](../scripts/deploy/tasks/cave-revision-history.json)
and [`cave-revision-history.sh`](../scripts/deploy/tasks/cave-revision-history.sh).
The permanent planner and workflow have no Cave-specific path or step.

The registered task causes the generic release lifecycle to do the following
**only when that task is selected**:

1. Publish the new API, publish the `Planarian.Operations` artifact, and
   create the EF migration bundle before touching hosted resources.
2. Verify the API is currently running and that App Service is not configured
   to stop or merge SCM/Kudu with the main site (`scmSiteAlsoStopped` and
   `WEBSITE_DISABLE_SCM_SEPARATION` must not be `true`), then stop the old
   Azure App Service to stop Cave writers.
3. Apply pending EF migrations from an EF Core bundle with the scoped
   schema connection string (EF tracks applied versions).
4. Run `dotnet Planarian.Operations.dll initialize-cave-revision-history`
   from the newly built operations artifact using the same scoped database
   connection secret as the EF bundle. The Cave operation's Operations-only
   repository owns its `PlanarianDbContext`/Npgsql setup and rollout SQL; it does
   not boot the web host or load Azure App Configuration. This command is resumable,
   idempotent, and verifies that no Cave remains without a current revision before
   it exits.
5. Only after that command succeeds, deploy the new API ZIP without requesting
   an early restart, start the App Service, and verify API health. The API itself
   contains no rollout/readiness behavior. Only after API health succeeds does the
   workflow publish the new web.

**Azure stopped-site ZIP deployment must be rehearsed in Development**
before scheduling the production rollout. Microsoft's App Service documentation states that stopping the main app
normally leaves the separate SCM/Kudu app running; this is why the workflow
fails before stopping an App Service configured to stop Kudu too or to disable
SCM separation. Kudu ZIP deployment can
still fail for other environmental reasons. If it fails, the workflow
intentionally leaves the old API stopped and does not publish the frontend. Do not remove
the write-quiescence safety boundary just to make the deploy action pass.
Choose an approved stopped-site package installation or maintenance
mechanism if the Development rehearsal proves it is needed.

If any step after quiescing fails, do not automatically start the old app:
its schema may already have changed and creating late baseline revisions
would corrupt the historical boundary. First verify the actual App Service
state. A failure before maintenance completion leaves the API stopped; a later
health/web failure can occur after the matching new API has already been
installed and started. In either case, recover by manually re-dispatching a
ref that resolves to the **exact same release commit** with
`resume_maintenance=true` (and the normal Production backup acknowledgment
when applicable). The planner rejects that flag unless the selected commit is
the newest unsuccessful coordinated release. The task accepts an already-
stopped API or re-quiesces a running matching API, reruns the migration bundle
and Cave bootstrap idempotently, deploys the matching API, and restores traffic
only after the new artifact is in place. A normal run still refuses an
unexpectedly stopped API. After this one-off has completed successfully in
every environment that still needs it, remove its JSON manifest and shell
script; no permanent Cave special case is required in the deployment planner.
Do not consider an account archive to be a full database backup. See
[Cave publication](architecture/cave-publication.md#rollout-deployment-sequence).

## Validation and limits

Run planner tests with:

```bash
python3 -m unittest discover -s scripts/deploy -p 'test_*.py' -v
find scripts/deploy/tasks -type f -name '*.sh' -print0 |
  while IFS= read -r -d '' task; do bash -n "$task"; done
```

Validate the workflow using `actionlint`, then test a Development run
for each component pattern and a release rollback/failure path with
test-only credentials. Initial pipeline activation and special maintenance
mechanisms should be rehearsed safely in Development; Production itself does
not depend on a prior Development deployment. GitHub-hosted runners,
static-web tokens, App Service startup settings, network reachability, Azure-specific
restores, and real API/frontend health remain **environment acceptance
checks**; static validation alone is not sufficient.

Authoritative references:

- [EF Core: applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying)
- [GitHub manual workflow runs](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow)
- [GitHub workflow concurrency](https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency)
- [GitHub Deployments REST API](https://docs.github.com/en/rest/deployments/deployments)
- [GitHub Deployment Statuses retention/current-state contract](https://docs.github.com/en/rest/deployments/statuses)
- [GitHub GraphQL Repository deployments](https://docs.github.com/en/graphql/reference/repos)
- [GitHub CLI API pagination](https://cli.github.com/manual/gh_api)
- [GitHub Environment protection](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments)
- [GitHub Actions secure use](https://docs.github.com/en/actions/reference/security/secure-use)
- [Azure Login](https://github.com/Azure/login)
- [Azure App Service deployment guidance](https://learn.microsoft.com/en-us/azure/app-service/deploy-best-practices)
- [Azure Static Web Apps skip build](https://learn.microsoft.com/en-us/azure/static-web-apps/build-configuration)
