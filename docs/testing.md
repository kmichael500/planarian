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

Unit-test pure parsing, planning, validation, matching, defaults, preview, and domain rules. Integration-test real
translation, tenant isolation, transaction/atomicity behavior, locking/concurrency, PostGIS, revision publication,
and deferred cleanup. Import harnesses may remove wiring while keeping CSV inputs and persistence expectations visible.

## Commands

Integration tests require a real PostgreSQL/PostGIS server. By default the fixture starts one with Testcontainers.
Inside the repository compose stack, `dc-agent-planarian` supplies `PLANARIAN_TEST_POSTGRES_CONNECTION_STRING` pointing
at the compose PostGIS administrative database; the fixture still creates a unique disposable database for each test
and never uses the development database as scenario state. Account File-import integration tests also start the same
Azurite version pinned by Compose so chunk/session behavior exercises the real Azure Blob SDK. Business-level durable
object-storage workflows continue to use the focused in-memory `IObjectStorage` fake for deterministic failure injection,
while a separate production-adapter contract suite runs the real `AzureBlobObjectStorage` implementation against the same
isolated Azurite fixture to verify provider semantics such as immutable create, metadata/read behavior, deletion, snapshots,
and container initialization/recreation. Use a focused filter during development:

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

The complete cross-stack pull-request workflow also installs/builds/tests the web client, validates generated client routes,
and validates the frontend environment example. Treat [`.github/workflows/pr-validation.yml`](../.github/workflows/pr-validation.yml)
as the canonical CI sequence instead of duplicating every frontend step here.
