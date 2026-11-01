# .NET/backend rules

- Application services, planners, workflow coordinators, and similar orchestration types do not directly own `PlanarianDbContext`, `DbContext`, `DbSet`, `NpgsqlConnection`, or `NpgsqlCommand` objects.
- Data-access components do not depend on application/service-layer types or perform external service/network work; services coordinate those concerns above persistence.
- Feature-oriented data-access infrastructure owns persistence; reserve `Repository` for resource/aggregate-style abstractions (using `RepositoryBase` / `AccountScopedRepositoryBase`) and name narrower capabilities by their role (`Reader`, `Store`, `Locker`, `Publisher`, `Persistence`). `IQueryable` may be composed within the data-access layer when needed, but it must not escape to services, workflows, or controllers.
- Data-access components may compose lower-level data-access capabilities only for one cohesive database algorithm on the same scoped DbContext/transaction. Keep the dependency graph acyclic, avoid independent nested transaction owners, and do not hide application/service workflow inside data access.
- Prefer constructor injection for lower-level data-access dependencies. Lazy/manual construction requires a documented scope/lifetime reason; do not hide dependencies merely for convenience.
- Every `IgnoreQueryFilters` use remains explicitly account/tenant qualified, except narrowly trusted cross-account safety queries that must detect any live owner of a shared external storage address before destructive deletion; those queries expose only the minimum reference data and require cross-account regression coverage.
- Published mutation and revision workflows preserve their required transaction, concurrency, and relational atomicity boundaries.
- Large imports remain set-oriented or bounded/chunked. Fully populated imports of 10,000 Caves and roughly 15,000 Entrances are a normal supported workload.
- See `../docs/data-access-architecture.md` for the reasoning and detailed persistence contract.
