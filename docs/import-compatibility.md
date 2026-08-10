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

The only registered import-semantic difference is `case-insensitive-tag-creation-deduplication`. Requested tag names
are trimmed and compared case-insensitively for deduplication, existence, creation, and resolution. A new tag preserves
the first trimmed spelling encountered in CSV order; an eligible existing tag retains its stored identity, casing,
ownership, and default status. Existing case-only duplicates are not rewritten or removed.

Tenant/data-integrity corrections also intentionally differ from the baseline: every File is account-owned,
File/staged-file relationships are tenant-qualified, and migration of unassignable legacy Files fails closed.
