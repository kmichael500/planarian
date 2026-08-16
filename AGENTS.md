# Planarian agent rules

- Behavior changes require appropriate automated tests.
- Use the narrowest relevant validation while developing and run the appropriate completion suite before finishing.
- Backend and persistence work follows `Planarian/AGENTS.md` and `docs/data-access-architecture.md`.
- Consult `docs/testing.md` when test or import work needs its additional policy or command detail.
- Import-compatibility work follows `docs/import-compatibility.md`.
- Cave revision or proposal work follows `docs/cave-revisions.md`.
- While `docs/plans/cave-revisions-completion/README.md` is marked active, Cave-revisions completion work also follows
  that target-state execution plan; its explicitly identified target changes override branch-only mechanics until the
  implementation updates the durable architecture docs.
- Do not silently weaken tenant isolation or the supported import-scale guarantees.
