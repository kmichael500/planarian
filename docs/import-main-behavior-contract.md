# Import compatibility contract from `main`

This contract was freshly read from `11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae`,
not inferred from the feature branch.

| Area | Main behavior | Required assertion |
| --- | --- | --- |
| Account context | Imports require `RequestUser.AccountId`; account-owned lookups use the active account | Two-account isolation test |
| Cave identity | Sync identity is State + County + CountyNumber | Duplicate-key characterization |
| References | States use global definitions; AccountStates, Counties, and custom Tags are active-account scoped | Same-code/name foreign-account tests |
| Cave fields | Name, alternate names, State, County, CountyNumber, lengths/depth, pits, narrative, ReportedOn, archive state | Complete field matrix |
| Cave tags | Geology, Geologic Age, Map Status, Physiographic Province, Archeology, Biology, Other, Cartographer, Reported By | Role-aware tag tests |
| Cave sync | Updates, inserts, no-change, and CSV-missing deletion follow main; unrelated Entrances/Files are preserved unless the Cave is deleted | Sync ownership tests |
| Dry run | Main uses the same processing rules and rolls back; replacement must produce the same preview with no durable writes | Preview/database equality test |
| Entrance association | CountyDisplayId + CountyCaveNumber within the active account | Foreign collision test |
| Entrance primary rule | Append uses existing + imported primary counts; sync validates intended replacement state | Zero/one/multiple tests |
| Entrance geometry | Longitude=X, latitude=Y, elevation=Z, SRID 4326 | PostGIS geometry test |
| Entrance sync | Only represented active-account Caves have Entrances replaced | Unrelated/foreign deletion tests |
| Failure and files | Database work is atomic; physical file cleanup is deferred after commit | Failure/dry-run cleanup tests |

## Intentional divergences

The replacement will make dry-run planning pure instead of writing reference
data and rolling it back. This is a safety correction while preserving main's
semantic preview. The dynamic temporary entrance table is also an
implementation detail scheduled for removal, not a product behavior.

## Suspected main behavior issues

The durable writes during dry run are a probable correctness risk. They must
not be retained by the typed planner, and the divergence must remain explicit
in compatibility tests and release notes.
