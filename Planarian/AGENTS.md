# .NET/backend rules

- Follow `../docs/data-access-architecture.md` and `../docs/testing.md`.
- Services/planners/coordinators do not own DbContext or Npgsql objects; repositories own persistence and transactions.
- Database-dependent tests use real PostgreSQL/PostGIS. Behavior changes are not done until their tests run.
- Preserve tenant predicates, import locking/atomicity, bounded tracking, and normal 10k-Cave scalability.
- Raw SQL is limited to provider contracts, historical migration setup, specialized observation, or scale seeding.
- Before completion, build the solution, run unit and relevant integration/golden/scale suites, and check pending models.
