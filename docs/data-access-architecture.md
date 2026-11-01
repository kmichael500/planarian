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
repositories own EF Core, Npgsql, and PostgreSQL access; there is no generic CRUD repository. Query composition may stay
inside the data-access layer when another data-access capability needs it, but services, workflows, and controllers receive
materialized results rather than `IQueryable`.

At the project boundary, `Planarian.Model` is dependency-free from other Planarian projects and
`Planarian.Library` may depend on Model for shared persistence/application capabilities. The legacy
`ApiException`/`ApiExceptionType` contract is therefore compiled by Model while retaining its historical
`Planarian.Library.Exceptions` namespace for source compatibility; the higher-level
`ApiExceptionDictionary` factory remains in Library. Do not reintroduce a Model -> Library project reference.

When EF Core inspection or reflection targets one known CLR member, use typed access or `nameof` on that exact
entity/contract rather than duplicating the spelling in a string. Generic cross-entity code must not borrow `nameof`
from a representative concrete entity merely because several entities share the same member spelling; use a
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
leak. Planarian's ownership interceptor rejects synchronous `SaveChanges` because its authorization checks
perform asynchronous database access. Prefer cohesive resource- or capability-oriented repositories over one repository per command. Extract persistence
behind a stronger repository capability when the operation requires provider-specific SQL, row-lock ordering, set-based
rewrites, provider exception translation, or another database algorithm that application orchestration should not know.

Normal request-scoped EF repositories in the web application derive from `RepositoryBase`; repositories that are only
valid to construct for an authenticated account and whose entire contract is account-bound derive from
`AccountScopedRepositoryBase`. These bases centralize request
context, unit-of-work mechanics, transaction helpers, and safe bounded insert mechanics; they do not define a generic CRUD
contract. `RequestUser` binds itself once to its request `PlanarianDbContext`; that binding is not publicly replaceable,
and `RepositoryBase` rejects a request user bound to a different context, so query-filter, permission, and audit context
cannot depend on repository construction order or cross-context boundaries. Persistence components with intentionally different lifetime, context,
or trust semantics remain explicit exceptions, including factory-created contexts, singleton maintenance locks, and
trusted non-request Library data access.

`AddRangeBatchedAndClearAsync` is for isolated insert phases that begin with an empty EF change tracker. It performs bounded
`AddRange`/`SaveChangesAsync` cycles and clears tracking after each batch; it does not create a transaction or replace a
provider-native bulk loader. Callers that require atomicity own the surrounding transaction. A future PostgreSQL `COPY` or
other direct bulk-write path must preserve Planarian's tenant, audit, ownership, and revision invariants and be justified by
structural import measurements rather than introduced as a generic repository shortcut.

Request-oriented services may derive from `ServiceBase`; services that are only valid to construct after authenticated
account selection and whose entire contract is account-bound may derive from `AccountServiceBase`. `ServiceBase<TRepository>` remains a legacy convenience for services with one natural
primary repository, not a required shape for new workflow services that coordinate multiple capabilities.

Repository implementations may use EF projections, tracking where mutation needs it, set-based operations, explicit
PostgreSQL SQL, row locks, and bounded batches. Provider-specific behavior is appropriate when it protects correctness
or throughput. `IgnoreQueryFilters` requires an explicit account predicate and behavioral tenant-isolation coverage except
for narrow trusted safety checks that intentionally search all accounts before destructive external cleanup. Those checks
may only answer whether an external storage address is live; they must not expose another account's domain data, and they
require cross-account regression coverage.

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
Large operations must remain set-oriented or chunked. Performance-sensitive changes require structural
measurement (commands, saves, batches, tracking) before changing thresholds or batch sizes.
