# Import compatibility contract from `main`

This contract was freshly read from `11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae`,
not inferred from the feature branch. The goal is to preserve observable import
semantics while replacing the internal staging/rollback architecture.

| Area | Main behavior | Required assertion |
| --- | --- | --- |
| Account context | Imports require `RequestUser.AccountId`; account-owned lookups use the active account | Two-account isolation test |
| Cave identity | Sync identity is State + County + CountyNumber | Duplicate-key characterization |
| State matching | State resolution uses the imported State value against the global State name/abbreviation behavior characterized from `main`; do not silently broaden matching rules | Case-sensitive compatibility test |
| References | States use global definitions; AccountStates, Counties, and custom Tags are active-account scoped | Same-code/name foreign-account tests |
| Custom tag resolution | Existing eligible tag resolution is case-insensitive, while creation comparison remains case-sensitive as characterized from `main` | Existing-case and creation-case tests |
| Cave required fields | `CaveName`, `State`, `CountyCode`, `CountyName`, and `CountyCaveNumber` must satisfy the same parsing/model constraints as `main` | Required-field and max-length tests |
| Cave optional fields | `AlternateNames`, `CaveLengthFt`, `CaveDepthFt`, `MaxPitDepthFt`, `NumberOfPits`, `Narrative`, `ReportedOnDate`, and `IsArchived` preserve the characterized parsing/default behavior | Complete scalar matrix |
| Cave numeric validation | Negative length/depth/pit/count values remain invalid where characterized by `main` | Negative-number tests |
| Cave tags | `Geology`, `GeologicAges`, `MapStatuses`, `PhysiographicProvinces`, `Archeology`, `Biology`, `OtherTags`, `CartographerNames`, `ReportedByNames` retain their role-specific mapping | Role-aware tag tests |
| Cave sync | Updates, inserts, no-change, and CSV-missing deletion follow `main`; unrelated Entrances/Files are preserved when a Cave is updated and removed only when the Cave itself is deleted | Sync ownership tests |
| Cave no-change | A semantically unchanged sync row remains observable as no-change in the plan and does not create a new Cave revision on commit | No-change/revision test |
| Dry run | `main` computes the preview through the mutating import path inside a transaction and rolls the transaction back; the replacement must produce the same semantic preview without durable writes | Preview/database equality test |
| Entrance association | `CountyCode`/County display ID + `CountyCaveNumber` resolve the target Cave within the active account | Foreign collision test |
| Entrance required fields | Cave key, latitude, longitude, elevation, and location quality retain the same required/validation behavior | Required-field and numeric-range tests |
| Entrance optional fields | `EntranceName`, description, pit depth, reported date, reported-by names, and primary flag retain characterized parsing/default behavior; an invalid optional reported date remains null | Complete scalar matrix |
| Entrance tags | `LocationQuality`, `EntranceStatuses`, `EntranceHydrology`, `FieldIndication`, `ReportedByNames` retain their role-specific mapping | Role-aware tag tests |
| Entrance primary rule | Append evaluates existing + imported primary counts; sync validates the intended replacement state | Zero/one/multiple-primary tests |
| Entrance geometry | Longitude=X, latitude=Y, elevation=Z, SRID 4326 | PostGIS geometry test |
| Entrance sync | Only represented active-account Caves have Entrances replaced; unrelated and foreign-account Caves are untouched | Unrelated/foreign deletion tests |
| Failure and files | Relational changes are atomic; physical blob cleanup for Cave deletions occurs only after relational commit | Failure/dry-run cleanup tests |

## Intentional architecture replacements

`main`'s rollback-based preview is a legitimate implementation of its existing
behavior: it runs the normal mutating import path inside a transaction and then
rolls the transaction back. This feature replaces that design deliberately for
**architecture, performance, and simplicity**, not because the baseline is
being declared incorrect.

The replacement parses and resolves the file into an immutable, read-only plan
before opening the write transaction. Preview is produced directly from that
plan. Commit applies the same plan after concurrency/ownership verification.
This avoids temporary writes during preview, shortens the write transaction,
and removes the need to maintain two conceptually different representations of
what preview and commit intend to do. Compatibility is defined by the resulting
preview and committed database semantics, not by reproducing the old internal
rollback mechanism.

The old temporary Entrance staging objects/store are likewise implementation
details, not part of the product contract. They are removed in favor of the
typed `EntranceImportPlan` planner/executor flow. No compatibility assertion
should depend on temporary staging tables, in-memory staging repositories, or
rollback side effects.

## Compatibility rule for intentional differences

When the new planner/executor differs internally from `main`, tests must prove
that the difference is intentional and that externally meaningful behavior is
preserved. A change should not be labeled a baseline correctness fix unless a
separate test and product decision establish that `main` itself was wrong.
