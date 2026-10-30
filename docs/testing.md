# Testing Planarian

Behavior changes require appropriate automated tests. Run the narrowest useful validation while developing and the
appropriate completion suite before finishing.

## Test contract

- `Planarian.Tests.Unit` covers database-free parsing, pure planning, domain behavior, snapshots/diffs, previews, and
  compiled architecture contracts.
- `Planarian.Tests.Integration` covers behavior that depends on real EF Core translation, PostgreSQL/PostGIS, tenant
  filters, constraints, transactions, locks, concurrency, migrations, imports, revisions, or repositories. Do not use
  EF InMemory or SQLite as substitutes for database-dependent behavior.
- `Migration` tests exercise upgrades from genuinely supported historical schemas.
- `Golden` tests protect observable import compatibility with the fixture anchored to
  `main@11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae`. Never rewrite that fixture merely to make a refactor pass.
- `Scale` tests protect the normal 10,000-Cave / roughly 15,000-Entrance workload. Assert command, write,
  `SaveChanges`, batching, and tracked-entry structure rather than flaky wall-clock thresholds.

Use small, scenario-specific typed factories for test data rather than a universal seed. Account, Cave, reference,
file, change-request, and import setup live in focused helpers under `Infrastructure/Data`. `GlobalStateTestData` remains
the narrow raw-SQL global-reference bypass. Protected global system prerequisites such as Permission definitions, default TagTypes, global FeatureSettings, and platform
permission assignments use the typed, interceptor-free `GlobalSystemTestData` seed context, mirroring `DataGenerator`;
do not use that bypass for tenant-owned setup. Dedicated high-volume setup belongs in `ImportScaleSeeder`.

Raw SQL in tests is reserved for provider contracts and observations (catalogs, constraints, PostGIS, locks, `xmin`,
or exact no-write checks), historical migration setup, and dedicated scale seeding. Routine domain arrangement
uses typed builders. For Cave read-integrity contracts, select domain tables through EF's mapped CLR entity
metadata (not hard-coded SQL table names), then use a fresh PostgreSQL connection to fingerprint the isolated
test database's relevant domain tables and `xmin` before/after each read. Do not count legitimate observability
or authentication activity (including File access throttling or `User.LastActiveOn`) as Cave mutations.

## Readable test architecture

Tests are executable documentation. An ordinary test should make its starting state, actor, primary operation, and
expected outcome apparent without requiring the reader to follow several helpers. Shared helpers hide mechanical
plumbing such as database creation, authentication, and valid service composition; business-significant setup such as
permission grants, file staging, reference removal, stale-base creation, and competing transactions remains visible.

Keep test classes cohesive by behavior. Prefer one primary act per ordinary test, split independently meaningful
failures, and use theories when several inputs exercise the same rule. Longer multi-actor transaction tests are the
exception when their visible interleaving is the behavior under test.

Production services used as systems under test must have complete, non-null dependency graphs. Service integration
tests use real repositories and the current test `PlanarianDbContext`, with explicit test implementations only at true
external boundaries. Never pass `null!` for a required dependency because one test path is believed not to use it.

Each ordinary integration test owns an isolated database. The PostgreSQL/PostGIS server may be shared across test
classes, but mutable scenario state may not be shared. Do not serialize a whole feature to compensate for hidden shared
state; use an existing nonparallel collection only for the smallest resource that genuinely requires it.

Prefer small typed factories, actor setup, permission helpers, and focused assertions over a general scenario DSL.
Readable duplication is better than an abstraction that conceals why the behavior occurs.

When a finite backend-owned wire value (for example an enum, permission key, feature key, tag role, or status)
must be mirrored in TypeScript, keep the frontend type explicit and cover the mirror in
`CrossStackStringContractTests`. Do not rely on two independently maintained string lists remaining synchronized.

Unit-test pure parsing, planning, validation, matching, defaults, preview, and domain rules. Integration-test real
translation, tenant isolation, transaction/atomicity behavior, locking/concurrency, PostGIS, revision publication,
and deferred cleanup. Import harnesses may remove wiring while keeping CSV inputs and persistence expectations visible.

## Commands

Integration tests require a real PostgreSQL/PostGIS server. By default the fixture starts one with Testcontainers.
Inside the repository compose stack, `dc-agent-planarian` supplies `PLANARIAN_TEST_POSTGRES_CONNECTION_STRING` pointing
at the compose PostGIS administrative database; the fixture still creates a unique disposable database for each test
and never uses the development database as scenario state. Account File-import and archive-publication integration tests also
start the same Azurite version pinned by Compose so chunk/session behavior and server-side block-from-URI publication exercise
the real Azure Blob SDK. The current pin is Azurite 3.37.0 because that release added `Put Block From URL`, which the archive
publisher uses for Azure-to-Azure staging. Business-level durable object-storage workflows continue to use the focused in-memory
`IObjectStorage` fake for deterministic failure injection,
while a separate production-adapter contract suite runs the real `AzureBlobObjectStorage` implementation against the same
isolated Azurite fixture implementation/version to verify provider semantics such as immutable create, metadata/read behavior,
deletion, snapshots, and container initialization/recreation. Use a focused filter during development:

```bash
dotnet test Planarian.Tests.Unit/Planarian.Tests.Unit.csproj \
  --configuration Release --no-restore \
  --filter FullyQualifiedName~RelevantTestOrNamespace

dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj \
  --configuration Release --no-restore \
  --filter FullyQualifiedName~RelevantTestOrNamespace
```

The normal comprehensive **backend** validation sequence is:

```bash
dotnet restore Planarian/Planarian.sln
dotnet build Planarian/Planarian.sln --configuration Release --no-restore
dotnet test Planarian.Tests.Unit/Planarian.Tests.Unit.csproj --configuration Release --no-restore
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj --configuration Release --no-restore
dotnet tool run dotnet-ef migrations has-pending-model-changes \
  --project Planarian/Planarian.Migrations/Planarian.Migrations.csproj \
  --startup-project Planarian/Planarian.Migrations/Planarian.Migrations.csproj \
  --context PlanarianDbContext --configuration Release --no-build
```

Migration/model changes require the Migration category and pending-model check; golden-compatibility changes require
Golden; batching or supported-workload changes require Scale. A full integration invocation already includes ordinary
PostgreSQL, Migration, Golden, and Scale tests, so do not redundantly append their filtered invocations after it.

The primary non-browser pull-request workflow also installs/builds/tests the web client, validates generated client routes,
and validates the frontend environment example. Treat [`.github/workflows/pr-validation.yml`](../.github/workflows/pr-validation.yml)
as the canonical non-browser PR validation sequence instead of duplicating every frontend step here.

## Real-browser E2E validation

Browser-specific contracts use Playwright against the disposable `e2e` development profile. The harness resets only
`planarian_dev_e2e` and the E2E-owned blob containers in local Azurite, seeds deterministic local test accounts, builds the
production web client, serves its generated CSP, and routes the frontend at `https://planarian.local` to the API at
`https://planarian.local:8443`. Using the same host on a different HTTPS port is intentional: it remains cross-origin
for CORS while preserving same-site cookie semantics consistently in Chromium and WebKit. This makes authentication cookies,
credentialed CORS/preflight, PDF.js workers and Range requests, nested routing, unload prompts, concurrent browser sessions,
and account switching observable in real browsers.
It never uses development, staging, or production application data. The disposable E2E backend also writes its
.NET build artifacts under the container's temporary filesystem instead of the bind-mounted source tree, preventing
host IDE/build activity from racing the browser harness. The E2E profile raises only its local login IP/email throttle
budgets so repeated real-browser logins across Chromium and WebKit do not exhaust the normal production limits; the
generic development profile and application defaults remain unchanged.

The runner requires exclusive ownership of the entire Planarian Compose project and refuses to run if any Compose service (including the repo editor, Postgres, or Azurite) is already running. It reports teardown failures rather than silently ignoring them. Stop a stack you intentionally own before using
it; do not use browser E2E as a way to reset another agent's environment. Install the pinned browser engines once, then
run either both supported engines or a focused project:

```bash
npm run test:browser:e2e:install
npm run test:browser:e2e
npm run test:browser:e2e:chromium
npm run test:browser:e2e:webkit
```

The non-destructive runner safety regression is `node --test scripts/run-browser-e2e.test.mjs` and does not start Docker or alter application data.

Pull requests also expose a separate **Browser E2E / Chromium + WebKit** check from `.github/workflows/browser-e2e-pr.yml`. The workflow intentionally has no top-level path filter so the check is always reported and can safely be required by branch protection. Its job performs a cheap changed-file check first and reports success without provisioning browsers for documentation or tests outside browser-relevant changed paths. Runtime, frontend, backend, shared-contract, E2E-harness, Docker/dev, script, or browser-workflow changes install only the pinned Chromium and WebKit engines plus their Linux dependencies, map the local HTTPS test hostnames to loopback, run the repository-owned `npm run test:browser:e2e` command, and upload `.playwright` reports/artifacts for 30 days when the job is not cancelled. CI keeps one Playwright worker and retries once for browser-level flake tolerance; a Playwright global timeout is shorter than the GitHub job timeout so Playwright can finish its own report before GitHub terminates the job.

Keep this suite intentionally small. Put timing/state-machine matrices in unit/component tests and reserve Playwright for
contracts that require an actual browser, cookie jar, navigation stack, CORS implementation, CSP enforcement, download,
or PDF.js renderer. Playwright traces, screenshots, videos, and HTML output are written under
`Planarian.Web/.playwright/` on failures and are ignored by Git.
