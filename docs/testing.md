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
database per test. Per-test databases provide correctness isolation: ordinary tests cannot interfere through tenant or
domain data. They do not provide resource isolation. Every database still consumes the same container's memory, CPU,
I/O, connections, and catalog/schema resources, so database-heavy integration concurrency is intentionally bounded.

At process startup, the fixture creates one data-free database from PostgreSQL `template0`, applies the complete EF
migration chain once, verifies that no migrations are pending, clears its Npgsql pools and backend sessions, and seals
it as a PostgreSQL template with ordinary connections disabled. Ordinary integration tests verify application and
repository behavior against the current PostgreSQL/PostGIS schema, so each receives a fresh clone of that template.
They do not need to replay Planarian's migration history. Dedicated migration tests instead create pristine databases
from `template0` and explicitly apply latest or historical migrations, preserving genuine migration coverage.

The checked-in integration runner policy is:

```text
Ordinary integration collections: max 2 concurrent
Database provisioning:            serialized template clone creation
Scale tests:                       non-parallel
Migration tests:                   non-parallel
Ordinary latest-schema database:  clone pre-migrated Planarian template
Migration-test database:          pristine template0 + explicit EF migration
```

The provisioning gate covers only the short `CREATE DATABASE ... TEMPLATE ...` operation; cloned databases may run
test logic concurrently. This retains real PostgreSQL/PostGIS semantics and per-test isolation while avoiding repeated
migration work, reducing memory/CPU pressure, improving local iteration, and making bounded parallel execution
reliable. A developer with a larger PostgreSQL environment may intentionally override runner settings for throughput
benchmarking, but the repository default is deterministic and independent of CPU count.

Each test clears only its own Npgsql pool and drops its database with `DROP DATABASE ... WITH (FORCE)`. Colima socket
discovery does not disable Ryuk. If a developer explicitly disables Ryuk, that environment owns external Docker
cleanup.

## Test data and raw SQL

Ordinary arrangement uses small typed EF builders/scenarios and creates only relevant state: account, county,
published Cave, Entrance, tag, request, or file as needed. Avoid universal mega-seeds and hidden unrelated entities.
`TestDataBuilder` provides those composable capabilities. The only routine model/filter bypass is
`GlobalStateTestData`, because `State` is shared global reference data. High-volume setup belongs in the dedicated
`ImportScaleSeeder`; ordinary tests must not use it.

Raw SQL is appropriate for PostgreSQL catalogs/constraints/PostGIS/locks/`xmin`, named historical-schema seeders,
exact JSON/`xmin` no-write observations, and dedicated high-volume seeders (including test-only binary COPY when EF
setup dominates). It is not appropriate for routine `INSERT INTO Accounts`, `Caves`, `Tags`, or `Files` arrangement.
An unavoidable model/filter bypass belongs in one narrow documented helper.

## Import coverage

Use fast unit tests for pure parsing/planning rules, matching, defaults, validation, sync/no-change intent, primary
calculation, and preview. Use PostgreSQL integration tests for planning repository projections and tenant scoping,
dry-run write rejection/state equality, commit atomicity, locks/concurrency, revisions, PostGIS, and deferred cleanup.
The golden fixture remains the compatibility authority; never update it merely to make a refactor pass.
Integration imports use the test-only `CaveImportTestHarness` and `EntranceImportTestHarness` to compose the real
parser, planning repository, pure planner, execution repository, snapshot repository, and revision repository. These
harnesses remove plumbing without hiding CSV inputs or persistence assertions.

## Readability and performance

Tests use descriptive condition/operation/result names and readable Arrange/Act/Assert structure. Extract containers,
CSV plumbing, interceptors, and normalization while keeping meaningful inputs and expectations visible. Tests are
deterministic, independent, and never depend on execution order.

Scale diagnostics separate parse, state-load, pure planning, and execution time and report SQL commands, writes,
`SaveChanges`, tracked-entry high-water marks, plan counts, and revisions. Prefer structural limits over flaky wall
clock limits. Compare small and large workloads to catch N+1 growth; bounded chunk growth is expected. Keep change
tracking bounded and do not measure scale seeding as importer execution. Scale CSV generation, seeding, metrics, and
execution live separately in `ImportScaleDataFactory`, `ImportScaleSeeder`, `ImportScaleMetrics`, and
`ImportScaleRunner`.

## Running tests

Docker must be available for integration tests. Local Colima is discovered automatically when `DOCKER_HOST` is unset.
For that detected Colima socket, the fixture also defaults `TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE` to
`/var/run/docker.sock`, which is the Docker socket path inside the Colima VM used by Ryuk. Explicit developer-provided
values for either setting are preserved. Ryuk remains enabled by default.

### Validation tiers

Run the narrowest suite that covers the risk of the change. Fast tests belong in the normal edit loop; PostgreSQL,
migration, golden, and scale tests add fidelity at increasing cost. A broader suite supersedes its filtered subsets
for that validation pass: running full integration and then separately running Migration, Golden, and Scale repeats
tests that already passed. Likewise, `dotnet test Planarian/Planarian.sln` is an alternative aggregate invocation,
not an additional requirement after both test projects have already passed.

Use a focused filter while developing:

```bash
dotnet test Planarian.Tests.Unit/Planarian.Tests.Unit.csproj \
  --configuration Release \
  --no-restore \
  --filter FullyQualifiedName~RelevantTestOrNamespace

dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj \
  --configuration Release \
  --no-restore \
  --filter FullyQualifiedName~RelevantTestOrNamespace
```

Before completion, choose validation by affected behavior:

- Pure domain, parsing, or planning changes: run the unit project and the relevant focused tests during development.
- PostgreSQL repository, transaction, tenant, locking, or PostGIS changes: run unit tests plus the relevant integration
  subset. Run full integration once when the change crosses several database features or is ready for PR validation.
- Migration or model changes: run the Migration subset and the EF pending-model check.
- Golden compatibility changes: run the Golden subset. Do not update the fixture merely to make it pass.
- Import batching, execution, structural metrics, or supported-workload changes: run the Scale subset once after the
  focused correctness tests pass. Scale is intentionally not part of every local edit cycle.
- Testcontainers lifecycle, database provisioning, parallel scheduling, or suspected flaky resource behavior: run
  full integration. Repeat it only when the purpose is specifically to establish reliability across independent
  processes, or when an acceptance criterion explicitly requires repetition.

The normal comprehensive PR/release gate is one pass through each distinct layer:

```bash
dotnet restore Planarian/Planarian.sln
dotnet build Planarian/Planarian.sln --configuration Release --no-restore
dotnet test Planarian.Tests.Unit/Planarian.Tests.Unit.csproj --configuration Release --no-restore
dotnet test Planarian.Tests.Integration/Planarian.Tests.Integration.csproj --configuration Release --no-restore
dotnet tool run dotnet-ef migrations has-pending-model-changes --project Planarian/Planarian.Migrations/Planarian.Migrations.csproj --startup-project Planarian/Planarian.Migrations/Planarian.Migrations.csproj --context PlanarianDbContext --configuration Release --no-build
```

The full integration invocation already includes ordinary PostgreSQL, Migration, Golden, and Scale tests. Filtered
commands remain useful for targeted work, but should not be appended to the comprehensive gate. CI follows this
single-pass model. If PR latency becomes materially worse, move unaffected large tests to a scheduled gate while
keeping them required for changes to the behavior they protect; do not gain speed by weakening their workload or
assertions.

xUnit v2 is intentionally retained. A v3 migration is a future, separate testing-infrastructure change.
