# .NET/backend rules

- Application services, planners, workflow coordinators, and similar orchestration types do not directly own `PlanarianDbContext`, `DbContext`, `DbSet`, `NpgsqlConnection`, or `NpgsqlCommand` objects.
- Feature-oriented repository and data-access infrastructure owns persistence; repositories return materialized results, never `IQueryable`.
- Every `IgnoreQueryFilters` use remains explicitly account/tenant qualified.
- Published mutation and revision workflows preserve their required transaction, concurrency, and relational atomicity boundaries.
- Large imports remain set-oriented or intentionally bounded/chunked. Fully populated imports of 10,000 Caves and roughly 15,000 Entrances are a normal supported workload.
- See `../docs/data-access-architecture.md` for the reasoning and detailed persistence contract.
