# Import compatibility contract from `main`

This contract was freshly read from `11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae`,
not inferred from the feature branch. The goal is to preserve observable import
semantics while replacing the internal staging/rollback architecture.

The checked-in `Planarian.Tests/Fixtures/import-main-11cdd9e.json` golden matrix
anchors representative CSV input, sync mode, validation, baseline preview, and
baseline committed state to that exact commit. A target override is permitted
only when it names an entry in `approvedSemanticDifferences`; the anchor test
rejects unregistered overrides and requires every registered difference to be
used exactly once. `MainImportBehaviorGoldenFixtureTests` ties every row to an
executable compatibility or preview/commit test. The committed state compares
durable TagType creation/removal deltas independently of Cave snapshots, as well
as importer-owned scalars, role-specific tag values, geometry, revision results,
and relationship preservation.

| Area | Main behavior | Required assertion |
| --- | --- | --- |
| Account context | Imports require `RequestUser.AccountId`; account-owned lookups use the active account | Two-account isolation test |
| Cave identity | Sync identity is State + County + CountyNumber | Duplicate-key characterization |
| State matching | State resolution uses the imported State value against the global State name/abbreviation behavior characterized from `main`; do not silently broaden matching rules | Case-sensitive compatibility test |
| References | States use global definitions; AccountStates, Counties, and custom Tags are active-account scoped | Same-code/name foreign-account tests |
| Custom tag resolution | Exact `main` resolved eligible tags case-insensitively but checked creation case-sensitively; the approved correction below makes both operations case-insensitive | Existing-case, creation-case, duplicate, and tenant tests |
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

Preserve externally meaningful `main@11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae`
semantics unless an intentional product correction is explicitly documented and
regression-tested. Architecture changes still require semantic equivalence. A
product correction requires a narrow registered target override while retaining
the exact baseline expectation in the fixture.

## Approved product-semantic differences

Exactly one import-semantic difference is approved: `case-insensitive-tag-creation-deduplication`.
The baseline creation/existence comparison was case-sensitive even though its
association resolution was already case-insensitive. This could plan and persist
an unused lowercase tag while associating the Cave or Entrance with an existing
canonically cased tag. Cave and Entrance imports now trim names and use
case-insensitive equality consistently for requested-name deduplication,
existence checks, creation decisions, and resolution.

An eligible existing tag retains its stored ID, name casing, ownership, and
default status. Tag key/type remains part of identity, and eligibility remains
limited to the active account's custom tags plus default/global tags. A custom
tag owned only by another account is never reused. Existing case-only duplicates
are not modified or removed; selection is deterministic: exact spelling, then
active-account ownership, then ID.

The separate tenant/data-integrity corrections remain required: File ownership,
tenant-qualified File/staged-file relationships, and fail-closed migration of
unassignable legacy Files.

## Fresh final source comparison

The final implementation was compared again with the actual Cave and Entrance
import services at the baseline commit. Parsing/defaults, State/County lookup,
case-insensitive eligible-reference resolution, role mapping, optional-date behavior,
geometry axis/SRID behavior, primary validation, sync targeting, preview values,
and committed aggregate results are behaviorally equivalent. Typed immutable
plans, read-only preview, bounded EF batches, concurrency locks, import batches,
and snapshot publication are architecture-only differences. Account-qualified
reads/deletes and File ownership constraints are intentional tenant/data-integrity
corrections. The registered tag-creation correction above is the sole approved
import product-semantic divergence; no unexplained observable difference remains.
