# Planarian agent rules

- Read `docs/testing.md` for test work and `docs/data-access-architecture.md` for backend persistence work.
- Behavior changes require appropriate tests; never silently omit testing, and run relevant suites before completion.
- Never inject `PlanarianDbContext` directly into application services, planners, or workflow coordinators. Keep
  EF/Npgsql access in feature-oriented repository/data-access infrastructure; do not expose `IQueryable`.
- Preserve explicit tenant qualification, particularly with `IgnoreQueryFilters`.
- Preserve set-oriented/batched imports. A fully populated 10,000-Cave import is a normal supported workload.
- Use raw SQL only under the categories in `docs/testing.md`; migrations and historical tests are intentionally special.
