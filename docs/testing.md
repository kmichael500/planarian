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
file, change-request, and import setup live in focused helpers under `Infrastructure/Data`. `GlobalStateTestData` is the
narrow global-reference bypass; dedicated high-volume setup belongs in `ImportScaleSeeder`.

Raw SQL in tests is reserved for provider contracts and observations (catalogs, constraints, PostGIS, locks, `xmin`,
or exact no-write checks), historical migration setup, and dedicated scale seeding. Routine domain arrangement uses
typed builders.

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

Docker must be available for integration tests. Use a focused filter during development:

```bash
dotnet test Planarian.Tests.Unit/Planarian.Tests.Unit.csproj \
  --configuration Release --no-restore \
  --filter FullyQualifiedName~RelevantTestOrNamespace

dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj \
  --configuration Release --no-restore \
  --filter FullyQualifiedName~RelevantTestOrNamespace
```

The normal comprehensive PR/release validation is:

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
