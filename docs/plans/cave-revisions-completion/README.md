# Cave revisions completion implementation plan

Status: **active pre-merge implementation plan** for `feature/cave-revisions-completion`.

Baseline inspected while writing this plan: `565ba7383b954ce2c7797346000ed10f9e374e6a`.
Re-check the current branch and diff before implementing; do not assume this SHA remains HEAD.

## Purpose

This directory is the execution package for finishing Cave revision, direct-edit, proposal, file, and line-plot behavior.
It is intentionally more prescriptive than the durable architecture docs so an implementation agent does not need to
rediscover design decisions or invent another workflow.

The target is one coherent interactive Cave aggregate mutation model covering Cave fields, tags, Entrances, Files, and
line plots, while preserving Planarian's tenant, concurrency, revision, proposal, and import invariants.

## Authority and precedence

Read these sources before changing code:

1. `AGENTS.md`.
2. `Planarian/AGENTS.md` for backend/persistence work.
3. `docs/data-access-architecture.md`.
4. `docs/testing.md`.
5. `docs/cave-revisions.md`.
6. **This plan, starting here.**
Where this active plan intentionally changes branch-only mechanics described in `docs/cave-revisions.md`, the target-state
decision in this plan governs the implementation. The implementation must then update the durable document in the same
change so the final repository has one truthful architecture description. Never leave contradictory docs behind.

Persisted V1 snapshot/proposal schemas are still pre-release on this branch. They may be corrected by this work before
the feature lands. Once the feature lands, the existing V1 immutability rule applies and later semantic changes require
schema-versioned models.

## How to execute this plan

Work through `implementation.md` in order. Do not start with isolated UI cleanup or a storage rewrite.

For every phase:

- inspect the current implementation before editing because the branch may have moved;
- write or adjust the narrow behavioral tests first when they can prove the bug or invariant;
- preserve the dependency direction in `docs/data-access-architecture.md`;
- complete the phase's focused validation before moving to the next dependent phase;
- do not broaden the task to nearby refactors merely because they look desirable;
- record any unavoidable deviation from this plan in the final implementation summary and update these docs if the
  target architecture itself had to change.

Do not run the whole repository test matrix during development. Follow `validation.md` and use the narrowest affected
builds/tests. A wider suite is justified only when a directly changed contract makes it necessary.

## Target outcome
When complete, all of the following are true:

- an existing-Cave editor submission carries the revision the user actually loaded;
- a stale manager edit returns an explicit conflict instead of silently overwriting a newer revision;
- one interactive Cave submission represents one desired aggregate state and creates at most one `CaveRevision`;
- semantic no-ops create no published revision and no proposal version;
- Files can be added, edited, and removed from the Cave editor without publishing during authoring;
- line plots can be added, renamed, replaced, and removed from that same editor;
- the Cave detail page is read-only for Cave-owned mutable state;
- Files and line plots participate in revision snapshots, diffs, proposal semantics, approval, and no-op detection;
- proposal versions remain immutable descriptions of exactly what was proposed, including assets;
- new Cave/staged File content is uploaded once and publication does not require copying bytes to a new key;
- ordinary removal of a previously published File moves only its private object locator into a tenant-owned retention record and does not destroy its immutable bytes;
- Cave/file application logic uses a provider-neutral object-storage boundary rather than Azure SDK types;
- the current `CaveGeoJson` `jsonb` storage and current line-plot rendering path remain in place for this task;
- imports retain their specialized set-oriented mechanics while sharing the published snapshot/revision invariants;
- tenant isolation, permission rules, locking, and enum/string invariants remain intact.

## Explicit non-goals

Do **not** implement any of the following as part of this plan:

- PostGIS-backed line-plot feature storage or MVT line-plot rendering;
- an immutable `LinePlotVersion` persistence model;
- a GeoJSON recovery table, recovery blob, expiration service, or rollback UI/API;
- a general historical Cave rollback engine;
- a repository-wide rewrite of every Azure Blob Storage caller;
- a generic CRUD repository, generic unit-of-work layer, or new service-owned `DbContext` usage;
- a broad redesign of import/archive behavior unrelated to the changed snapshot fields;
- whole-solution test runs merely for reassurance during implementation.

## Confirmed branch gaps this plan closes

The current branch was inspected before this plan was written. These are concrete gaps, not hypothetical cleanup:

- `CaveVm` exposes `CurrentRevisionId`, while both backend and frontend `AddCaveVm` currently omit an expected-revision
  field; direct edit therefore loses the browser's base revision before publication.
- `CaveService` currently falls back to the freshly loaded entity's `CurrentRevisionId` when no expected revision is
  supplied, which cannot detect a stale browser form.
- `CaveComponent.tsx` currently owns direct published File upload through `UploadComponent`/`AddCaveFile`.
- `CaveComponent.tsx` currently owns direct GeoJSON persistence through `GeoJsonSaveModal`.
- `AddCaveComponent.tsx` can edit/remove existing File metadata but does not provide the complete add-file authoring flow.
- `CavePublishedSnapshotV1` contains Cave fields, tags, Entrances, and File metadata but no line-plot state.
- `CaveService.UploadCaveGeoJson` currently replaces persisted line plots outside Cave revision publication.
- current GeoJSON replacement removes all existing rows and recreates them instead of preserving unchanged logical IDs.
- proposal File intent is in `CaveProposalSnapshotV1`, but staged File ownership is currently request-level through
  `CaveChangeRequestStagedFile`, which prevents clean pre-request editor staging and requires careful version semantics.
- current staged File publication copies blobs and changes `BlobKey`/`BlobContainer`; the target design uploads new
  Cave/staged File content once and changes relational lifecycle instead.

## Plan files

- `architecture.md` — target invariants and data/lifecycle decisions. Treat this as the design contract.
- `implementation.md` — ordered code changes, concrete files, and phase exit conditions.
- `validation.md` — required focused tests, validation commands, and final acceptance gate.

If an implementation detail is unclear, resolve it from `architecture.md` first rather than inventing a competing model.
## Required execution order

1. Fix direct-editor expected-revision propagation and stale conflict behavior.
2. Introduce the provider-neutral object-storage boundary for the affected File workflows.
3. Convert new Cave/staged Files to upload-once lifecycle semantics and add the narrow retained-object persistence used on ordinary removal.
4. Complete File authoring in the aggregate Cave editor and remove detail-page File mutation.
5. Add line plots to the aggregate editor while retaining the current `CaveGeoJson` storage model.
6. Add line-plot metadata/fingerprints to published revision semantics and diffs while keeping File storage locators private.
7. Make proposal versions immutably preserve exact File and line-plot intent/content.
8. Route approval through the same complete aggregate publication semantics.
9. Verify import/archive/hard-delete compatibility only where these changed contracts reach them.
10. Update durable docs, run the focused completion validation, and perform a final bypass/no-op/tenant review.

Do not reorder phases 1–3 behind the UI work. The editor must be built on the correct concurrency and asset lifecycle
contracts, not patched around the old ones.

## Handoff/completion protocol

An agent stopping or handing off before completion must state:

- the last fully completed phase from `implementation.md`;
- files changed in the incomplete phase;
- tests actually run and their results;
- tests not yet run;
- any discovered contradiction with this plan;
- whether the working tree contains intentionally unfinished code.

A completing agent must finish the acceptance checklist in `validation.md`, update the durable docs to the implemented
truth, and remove any obsolete direct mutation path rather than leaving two supported ways to publish the same state.
