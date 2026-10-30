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

When EF Core inspection or reflection targets one known CLR member, use typed access or `nameof` on that exact
entity/contract rather than duplicating the spelling in a string. Generic cross-entity code must not borrow `nameof`
from a representative concrete entity just because several entities currently share the same member spelling; use a
real shared CLR contract or derive the relationship/member from EF model metadata instead. Reusable EF relationship
metadata mechanics belong in `Planarian.Model/Database/Extensions` so feature repositories and interceptors keep only
their domain-specific ownership/mutation policy. When code needs a physical database table or column name, use the EF
model metadata when practicable: CLR names are **not** guaranteed to match
mapped SQL names. Explicit SQL, historical migrations, serialized API values, and external protocol identifiers are
compatibility contracts; do not rename or derive those mechanically without verifying their consumers. When maintained
SQL must embed finite application-owned values, keep the SQL explicit and add a contract test tying those literals back
to the canonical application constants instead of attempting to generate SQL from CLR names. For finite
application-owned states, prefer C# enums or TypeScript string-literal unions, while treating newly received external
values defensively.

For ordinary tracked mutations, a service may coordinate application intent through its feature repository and use that
repository's unit-of-work helpers such as `Add`, `Delete`, and `SaveChangesAsync`; those calls are not themselves a persistence
leak. Planarian's ownership interceptor intentionally rejects synchronous `SaveChanges`, because its authorization checks
perform asynchronous database access. Prefer cohesive resource- or capability-oriented repositories over one repository per command. Extract persistence
behind a stronger repository capability when the operation requires provider-specific SQL, row-lock ordering, set-based
rewrites, provider exception translation, or another database algorithm that application orchestration should not know.

Repository implementations may use EF projections, tracking where mutation needs it, set-based operations, explicit
PostgreSQL SQL, row locks, and bounded batches. Provider-specific behavior is appropriate when it protects correctness
or throughput. `IgnoreQueryFilters` requires an explicit account predicate and behavioral tenant-isolation coverage.

Import parsing is database-free. Planning repositories preload only the active account's materialized immutable state.
Pure planners consume typed rows plus that state and produce immutable plans. Write transactions begin only when an
execution repository applies a completed plan. Execution retains stable `FOR UPDATE` ordering, concurrency checks,
bounded `SaveChanges` and tracking, and relational/revision atomicity. External object storage is outside the database
transaction. The Cave/File publication lifecycle uses the provider-neutral `IObjectStorage` boundary rather than leaking
Azure SDK types into that orchestration. Some older, unrelated archive/photo/trip/storage workflows still use the
Azure-specific `BlobService` or Azure SDK types; they are legacy consumers, not precedent for new Cave/File persistence
work. Writes performed before relational commit require safe compensation when their lifecycle is not retained; destructive
object cleanup is deferred until after commit. Lifecycle changes that only associate an already-staged object with
relational state do not copy/re-upload that object merely to cross a staging/publication boundary.

A fully populated 10,000-Cave import, and roughly 15,000 Entrances for 10,000 Caves, are normal supported workloads.
Large operations must remain set-oriented or intentionally chunked. Performance-sensitive changes require structural
measurement (commands, saves, batches, tracking) before changing thresholds or batch sizes.
