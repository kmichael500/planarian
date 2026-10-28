# Import compatibility

Observable import compatibility is anchored to the golden fixture derived from
`main@11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae`. Compatibility concerns preview and committed semantics, not the old
internal implementation. Do not rewrite the fixture merely to make a refactor pass. An intentional semantic change
requires an explicit, narrowly documented and regression-tested difference while retaining the baseline expectation.

## Durable contracts

- All lookups, planning, writes, sync deletion, and preservation behavior remain scoped to the active account. Global
  references remain global; unrelated records and records owned by other accounts are preserved.
- Cave sync owns the imported Cave aggregate but preserves unrelated Entrances and Files on update; those relationships
  disappear only when their Cave is deleted. Entrance sync replaces Entrances only for represented active-account
  Caves.
- Dry run produces the same observable plan semantics as commit and performs no durable writes.
- Entrance primary validation considers existing plus imported state for append, and the intended replacement state for
  sync. Zero, one, and multiple-primary cases retain their characterized behavior.
- When multiple eligible tags match, selection is deterministic: exact spelling, then active-account ownership, then
  stable ID. Tag identity includes its key/type, and foreign-account-only custom tags are never reused.
- Relational import changes and revisions are atomic. Physical blob cleanup for Cave deletion occurs only after commit.

## Approved semantic differences

`case-insensitive-tag-creation-deduplication` trims requested tag names and compares them case-insensitively for
deduplication, existence, creation, and resolution. A new tag preserves the first trimmed spelling encountered in CSV
order; an eligible existing tag retains its stored identity, casing, ownership, and default status. Existing case-only
duplicates are not rewritten or removed.

`sync-optional-cave-narrative-canonicalization` applies only to existing-Cave sync updates. Blank Narrative is
canonicalized to `null`, matching normal authoring and the planner's own blank/absent comparison semantics. New Cave
imports keep their characterized baseline behavior. This prevents a row whose preview reports only another field change
from silently converting a previously absent Narrative to an empty string during commit.

`sync-optional-entrance-text-canonicalization` applies only to Entrance sync replacement. Blank optional Entrance name
and description values are canonicalized to `null`, matching normal authoring clear semantics. Append/import creation
keeps its characterized baseline behavior. This prevents a sync preview that treats blank and absent text as equivalent
from committing an otherwise invisible `null` to empty-string mutation.

Tenant/data-integrity corrections also intentionally differ from the baseline: every File is account-owned,
File/staged-file relationships are tenant-qualified, and migration of unassignable legacy Files fails closed.
