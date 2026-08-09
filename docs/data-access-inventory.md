# Data-access inventory

The live branch was inventoried with `rg` before consolidation. The complete
production set was:

| Area | Operation | Replacement | Sensitivity |
| --- | --- | --- | --- |
| `RepositoryBase` | Bulk insert/save wrappers | Removed; repository/data-access implementations use EF explicitly | Import throughput |
| Cave import services | Cave/tag inserts | Bounded `AddRange` + save | Import throughput |
| Cave repository | Distinct Cave updates | Tracked bounded updates | Import throughput |
| Account repository | Batch deletes and async materialization | EF execute/query APIs | Cleanup correctness |
| Temporary entrance repository | Typed in-memory import rows and account-scoped resolution | Typed import plan/executor | Import throughput and connection lifetime |
| Map repository | Query materialization | EF Core LINQ | Normal request path |

The dynamic PostgreSQL staging table has been removed. Entrance CSV rows remain
typed in-memory import data and are materialized through EF inside the import
transaction. No linq2db namespace, startup initialization, BulkExtensions API,
or substitute bulk library remains in production code.

Import CSV parsing is now separate from account-qualified planning-state loads. `CaveImportPlanner` and
`EntranceImportPlanner` are database-free and consume immutable projections. Feature planning/execution repositories,
the Cave snapshot repository, the import revision repository, and the Cave mutation repository own EF/Npgsql work.
Application services and coordinators do not receive a DbContext. See `data-access-architecture.md`.
