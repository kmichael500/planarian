# Data-access inventory

The live branch was inventoried with `rg` before consolidation. The complete
production set was:

| Area | Operation | Replacement | Sensitivity |
| --- | --- | --- | --- |
| `RepositoryBase` | Bulk insert/save wrappers | Removed; callers use EF explicitly | Import throughput |
| Cave import services | Cave/tag inserts | Bounded `AddRange` + save | Import throughput |
| Cave repository | Distinct Cave updates | Tracked bounded updates | Import throughput |
| Account repository | Batch deletes and async materialization | EF execute/query APIs | Cleanup correctness |
| Temporary entrance repository | Dynamic staging DDL, insert, joins, reads | Parameterized Npgsql commands | Import throughput and connection lifetime |
| Map repository | Query materialization | EF Core LINQ | Normal request path |

The only remaining raw SQL is existing/provider-specific SQL (PostGIS and
dynamic staging) expressed through EF/Npgsql parameterization. No linq2db
namespace, startup initialization, BulkExtensions API, or substitute bulk
library remains in production code.

