# Data-access architecture

Planarian uses this permanent dependency direction:

```text
Controller
    ↓
Service / workflow coordinator
    ↓
Pure domain or planning logic + feature repository capabilities
    ↓
Repository / data-access layer
    ↓
EF Core / Npgsql / PostgreSQL
```

Application orchestration depends on feature capabilities, not database infrastructure. Cohesive, feature-oriented
repositories own EF Core, Npgsql, and PostgreSQL access; there is no generic CRUD repository. Repositories return
materialized projections, never `IQueryable`.

Repository implementations may use EF projections, tracking where mutation needs it, set-based operations, explicit
PostgreSQL SQL, row locks, and bounded batches. Provider-specific behavior is appropriate when it protects correctness
or throughput. `IgnoreQueryFilters` requires an explicit account predicate and behavioral tenant-isolation coverage.

Import parsing is database-free. Planning repositories preload only the active account's materialized immutable state.
Pure planners consume typed rows plus that state and produce immutable plans. Write transactions begin only when an
execution repository applies a completed plan. Execution retains stable `FOR UPDATE` ordering, concurrency checks,
bounded `SaveChanges` and tracking, and relational/revision atomicity. Blob storage is outside the database transaction:
new writes require compensation on failure, while destructive cleanup is deferred until after commit.

A fully populated 10,000-Cave import, and roughly 15,000 Entrances for 10,000 Caves, are normal supported workloads.
Large operations must remain set-oriented or intentionally chunked. Performance-sensitive changes require structural
measurement (commands, saves, batches, tracking) before changing thresholds or batch sizes.
