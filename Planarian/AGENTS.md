# .NET/backend rules

- Application services, planners, workflow coordinators, and similar orchestration types do not directly own `PlanarianDbContext`, `DbContext`, `DbSet`, `NpgsqlConnection`, or `NpgsqlCommand` objects.
- Feature-oriented repository and data-access infrastructure owns persistence; `IQueryable` may be composed within the data-access layer when needed, but it must not escape to services, workflows, or controllers.
- Every `IgnoreQueryFilters` use remains explicitly account/tenant qualified, except narrowly trusted cross-account safety queries that must detect any live owner of a shared external storage address before destructive deletion; those queries expose only the minimum reference data and require cross-account regression coverage.
- Published mutation and revision workflows preserve their required transaction, concurrency, and relational atomicity boundaries.
- Large imports remain set-oriented or bounded/chunked. Fully populated imports of 10,000 Caves and roughly 15,000 Entrances are a normal supported workload.
- See `../docs/data-access-architecture.md` for the reasoning and detailed persistence contract.
