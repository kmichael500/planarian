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

Use small, scenario-specific typed builders for test data, not a universal seed. `TestDataBuilder` composes ordinary
account, reference, Cave, Entrance, request, and file state. `GlobalStateTestData` is the narrow global-reference
bypass; dedicated high-volume setup belongs in `ImportScaleSeeder`.

Raw SQL in tests is reserved for provider contracts and observations (catalogs, constraints, PostGIS, locks, `xmin`,
or exact no-write checks), historical migration setup, and dedicated scale seeding. Routine domain arrangement uses
typed builders.

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
