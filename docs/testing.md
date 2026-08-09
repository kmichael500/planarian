# Testing Planarian

## Test philosophy

> Production behavior changes must include appropriate automated tests. A feature, bug fix, persistence change,
> migration, import rule, authorization change, concurrency change, or architectural contract is not complete until
> its relevant tests are added or updated and run.

Documentation, comments, formatting, and provably mechanical moves may not need a new case, but affected existing
tests must still run and the completion report must say why no new test was needed. Definition of done answers: what
changed, which test proves it, which regression would previously fail where applicable, and which suites ran.

## Taxonomy

- `Planarian.Tests.Unit`: database-free domain, snapshot/diff, pure Cave/Entrance planner, preview, and compiled
  architecture tests.
- `Planarian.Tests.Integration`: behavior requiring real EF translation, PostgreSQL/PostGIS, tenant filters,
  constraints, transactions, locks, `xmin`, imports, revisions, and repositories.
- Migration tests build historical schema conditions with named historical SQL helpers and upgrade them.
- Golden tests execute the fixture anchored to `main@11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae`.
- Scale tests protect the normal 10k Cave / 15k Entrance workload with structural metrics. Optional larger tests may
  be separately categorized, but do not replace the supported-workload tests.

## Database and lifecycle policy

Production uses PostgreSQL/PostGIS, so database behavior is tested against PostgreSQL/PostGIS—not EF InMemory or
SQLite. Integration tests share one pinned `postgis/postgis:16-3.4` Testcontainers server and create a random isolated
database per test, migrate it, clear Npgsql pools, and drop it on disposal. Parallel execution is safe where each test
owns its database. Colima socket discovery does not disable Ryuk. If a developer explicitly disables Ryuk, that
environment owns external Docker cleanup.

## Test data and raw SQL

Ordinary arrangement uses small typed EF builders/scenarios and creates only relevant state: account, county,
published Cave, Entrance, tag, request, or file as needed. Avoid universal mega-seeds and hidden unrelated entities.
High-volume setup belongs in a dedicated scale seeder.

Raw SQL is appropriate for PostgreSQL catalogs/constraints/PostGIS/locks/`xmin`, named historical-schema seeders,
exact JSON/`xmin` no-write observations, and dedicated high-volume seeders (including test-only binary COPY when EF
setup dominates). It is not appropriate for routine `INSERT INTO Accounts`, `Caves`, `Tags`, or `Files` arrangement.
An unavoidable model/filter bypass belongs in one narrow documented helper.

## Import coverage

Use fast unit tests for pure parsing/planning rules, matching, defaults, validation, sync/no-change intent, primary
calculation, and preview. Use PostgreSQL integration tests for planning repository projections and tenant scoping,
dry-run write rejection/state equality, commit atomicity, locks/concurrency, revisions, PostGIS, and deferred cleanup.
The golden fixture remains the compatibility authority; never update it merely to make a refactor pass.

## Readability and performance

Tests use descriptive condition/operation/result names and readable Arrange/Act/Assert structure. Extract containers,
CSV plumbing, interceptors, and normalization while keeping meaningful inputs and expectations visible. Tests are
deterministic, independent, and never depend on execution order.

Scale diagnostics separate parse, state-load, pure planning, and execution time and report SQL commands, writes,
`SaveChanges`, tracked-entry high-water marks, plan counts, and revisions. Prefer structural limits over flaky wall
clock limits. Compare small and large workloads to catch N+1 growth; bounded chunk growth is expected. Keep change
tracking bounded and do not measure scale seeding as importer execution.

## Running tests

Docker must be available for integration tests. Local Colima is discovered automatically when `DOCKER_HOST` is unset.
Some Colima versions cannot mount the host socket into Ryuk. In that environment, a developer may explicitly run with
`TESTCONTAINERS_RYUK_DISABLED=true`; that developer is then responsible for cleaning orphaned Docker resources.

```bash
dotnet restore Planarian/Planarian.sln
dotnet build Planarian/Planarian.sln --configuration Release --no-restore
dotnet test Planarian.Tests.Unit/Planarian.Tests.Unit.csproj --configuration Release --no-restore
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj --configuration Release --no-restore
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj --filter FullyQualifiedName~Migration
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj --filter FullyQualifiedName~Golden
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj --filter FullyQualifiedName~Scale
dotnet test Planarian/Planarian.sln --configuration Release --no-restore
dotnet tool run dotnet-ef migrations has-pending-model-changes --project Planarian/Planarian.Migrations/Planarian.Migrations.csproj --startup-project Planarian/Planarian.Migrations/Planarian.Migrations.csproj --context PlanarianDbContext --configuration Release --no-build
```

xUnit v2 is intentionally retained. A v3 migration is a future, separate testing-infrastructure change.
