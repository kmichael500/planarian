# Cave Revisions — Completion Design and Implementation Plan

Recommended repository path:

`docs/cave-revisions-completion.md`

This document defines the remaining work required to finish the Cave Revision and Change Request feature.

It supplements `docs/cave-revisions.md`. It does **not** replace or duplicate the domain architecture already established there.

The goal is to finish the feature in a small number of substantial milestones while reusing the proven user interface and interaction design from the previous `feature/review-changes` implementation wherever it remains appropriate.

---

# 1. Branch and integration strategy

## Create a dedicated completion branch

Before making implementation changes:

1. Start from the **latest current head** of:

   `feature/cave-revisions`

2. Create a new branch from that exact head named:

   `feature/cave-revisions-completion`

3. Perform **all remaining Cave Revision API and UI implementation on `feature/cave-revisions-completion`**.

Do not implement this remaining work directly on `feature/cave-revisions`.

The intended integration sequence is:

`feature/cave-revisions-completion`
→ `feature/cave-revisions`
→ `main`

`feature/cave-revisions` remains the integration branch for the entire Cave Revision project.

Do **not** merge `feature/cave-revisions-completion` into `feature/cave-revisions`, and do **not** merge `feature/cave-revisions` into `main`, as part of these milestones unless the user explicitly asks for those Git operations.

The completion branch should reach a fully validated state first.

---

# 2. Sources of truth

There are two important branches, and they serve different purposes.

## Architecture source of truth

Use the current:

`feature/cave-revisions`

branch for all:

* domain modeling
* persistence
* revision semantics
* proposal/version semantics
* concurrency behavior
* tenant isolation
* authorization
* imports
* published mutation behavior
* approval/rejection behavior
* file staging/publication
* tests
* backend architecture

In particular, preserve the architecture established by `docs/cave-revisions.md`.

### Published revisions

A `CaveRevision` is an immutable snapshot of actual published Cave state.

Normal authorized edits, imports, archive changes, and published-file changes continue through the normal published mutation boundary.

Do not turn ordinary authorized edits into change requests merely because the proposal system exists.

### Proposals

A change request is **not** published state.

Creating or editing a proposal must not create a `CaveRevision`.

Proposal versions are immutable and append-only.

A proposal against an existing Cave is based on a specific published revision.

### Approval

Approval must use the existing published mutation workflow.

It must not:

* independently update Cave tables;
* directly copy proposal JSON into published tables;
* bypass revision creation;
* or maintain a second publication mechanism specifically for change requests.

The successful transaction should produce the normalized published state and its corresponding revision exactly as the current architecture intends.

### Stale proposals

Never silently rebase or merge a proposal onto a newer Cave revision.

A proposal whose base revision is no longer current must enter an explicit conflict/re-review workflow.

### Rejection

Rejection preserves the request, immutable proposal versions, reviewer information, and decision history.

It does not modify published Cave state and does not create a published Cave revision.

---

# 3. Previous implementation as the UX reference

Use:

`feature/review-changes`

as the primary product/UI reference for the remaining work.

Before implementing a corresponding frontend surface, inspect its old implementation.

Important old files include:

* `Planarian.Web/src/Modules/Caves/Components/CaveHistoryModal.tsx`
* `Planarian.Web/src/Modules/Caves/Components/CaveReviewComponent.tsx`
* `Planarian.Web/src/Modules/Caves/Components/CaveReviewsComponent.tsx`
* `Planarian.Web/src/Modules/Caves/Pages/CaveReviewPage.tsx`
* `Planarian.Web/src/Modules/Caves/Pages/CaveReviewsPage.tsx`
* `Planarian.Web/src/Modules/Caves/Pages/CaveEditReviewPage.tsx`
* the old Cave service/model files used by those surfaces
* the old routing/navigation integration

Also inspect the current Cave detail and Cave edit implementations before adapting the old UI.

## Reuse philosophy

The previous UI should generally be **ported and adapted**, not recreated from memory.

Reuse where practical:

* page composition
* responsive layouts
* history timeline concepts
* changed-field presentation
* original-versus-proposed presentation
* entrance presentation
* tag presentation
* narrative display
* file display
* user avatars
* timestamps
* status presentation
* loading and empty states
* use of the normal Cave editor for reviewer/proposal editing
* existing shared Planarian components and formatting helpers

The old `CaveReviewComponent` in particular is a useful starting point for the new shared snapshot/diff presentation.

The old `CaveHistoryModal` is likewise a strong starting point for revision history.

## Do not port the old architecture

Do **not** wholesale cherry-pick `feature/review-changes`.

Do not resurrect its old request/logging model simply because its components expect those DTOs.

In particular:

* do not recreate old `ProposedChangeRequestVm` contracts merely to satisfy old UI;
* do not recreate a parallel Cave change-log persistence system;
* do not make mutable proposal state where the new architecture requires immutable versions;
* do not bypass the current revision repositories/workflow;
* do not weaken concurrency or tenant guarantees;
* do not silently apply stale submissions.

Adapt the components to the **new API contracts** instead.

The objective is:

**old UX + new architecture**

---

# 4. Overall implementation strategy

Finish the remaining feature in **three milestones**.

Do not subdivide these into a large sequence of artificial milestones.

Each milestone should result in an internally complete capability.

## Milestone 1

**Revision History + Diff**

Deliver the read/query API and visible revision history UI.

## Milestone 2

**Complete Change Request Workflow**

Deliver proposal creation, submission, contributor workflow, reviewer workflow, approval/rejection, stale handling, and the corresponding UI as one end-to-end product slice.

## Milestone 3

**Integration Hardening + Completion**

Complete navigation, permissions, edge cases, testing, cleanup, and full validation so the branch is ready to merge back into `feature/cave-revisions`.

---

# Milestone 1 — Revision History + Diff

## Goal

Expose the revision history that the backend already records and port the useful history/diff presentation from `feature/review-changes`.

This milestone establishes the frontend/backend read model that the reviewer workflow will also build upon.

It should be a complete read-only vertical slice.

---

## 1.1 Verify the existing revision architecture first

Before writing endpoints, inspect the current implementations of:

* `CaveRevision`
* revision snapshot representation
* current-revision pointer
* revision repositories
* `CaveRevisionDiffService`
* `CaveMutationWorkflow`
* Cave authorization/tenant handling
* existing Cave controller/API conventions
* existing DTO conventions

Do not invent endpoint/service layering without first following the existing repository conventions.

Do not duplicate diff calculations in TypeScript if the current backend diff service already owns the authoritative comparison semantics.

---

## 1.2 Add the minimal revision query/application surface

Expose the functionality required by the UI.

At minimum, support:

### List a Cave's published revisions

Return enough information for a history list, including where available:

* revision ID
* Cave ID
* sequence/order
* creation/publication time
* actor
* publication source/provenance
* associated request ID when the publication came from an approved request
* enough information to identify the current published revision

Do not send complete snapshots for every history row unless that is genuinely required.

### Retrieve the information needed for a revision comparison

Support comparing a revision against the appropriate predecessor using the existing revision diff infrastructure.

The API should be designed around what the UI actually needs rather than exposing persistence entities directly.

The UI should not need to reverse-engineer snapshots to decide what changed.

### Current revision

The client must be able to unambiguously identify which history entry represents the currently published Cave state.

### Access control

Revision history is Cave data.

Apply the same applicable:

* account/tenant isolation
* Cave visibility
* Cave-read authorization

as the rest of the Cave.

Knowing a Cave ID or revision ID must not provide a route around normal authorization.

---

## 1.3 Port the Cave History UI

Use the old:

`CaveHistoryModal.tsx`

as the starting point.

Do not rebuild a generic history framework.

Adapt it to consume the new revision/diff query model.

Preserve useful behavior from the previous UI, including:

* timeline/history presentation
* human-readable field names
* previous and new values
* formatted dates
* formatted coordinates
* formatted distances
* booleans
* tags
* entrances
* lists
* expandable narratives
* actor information
* timestamps

Where the new revision system has better provenance than the old implementation, display the new information.

Examples might include:

* Edited
* Imported
* Approved change request
* Created

Use the actual provenance available from the current backend rather than guessing based on frontend state.

---

## 1.4 Integrate history into the Cave detail experience

Add a clear History/Revisions affordance to the existing Cave detail page.

Reuse the existing Planarian visual language.

The user should not have to navigate into an administrative review area simply to see Cave history.

Expected behavior:

1. Open Cave.
2. Select History.
3. See published revision timeline.
4. Select/expand a revision.
5. See what changed.
6. See who caused the publication and when.
7. Clearly recognize the current published revision.

The initial published revision should be represented naturally as the Cave's creation/initial publication rather than pretending it has a nonexistent previous version.

---

## 1.5 Establish a reusable comparison presentation

Avoid implementing history diffs and proposal diffs as two unrelated frontend systems.

The old `CaveReviewComponent.tsx` already demonstrates presentation for:

* Cave-level fields
* original/current values
* modified values
* entrances
* added/deleted items
* tags
* narratives
* files

Refactor or adapt only as necessary so that the eventual change-request review UI can use the same fundamental presentation concepts.

Do **not** create a speculative generic diff framework.

The abstraction should emerge only to the degree actually needed by:

1. revision history; and
2. proposal review.

---

## 1.6 Milestone 1 tests

Add focused tests for behavior that could realistically regress.

Backend coverage should include:

* revision list authorization
* tenant isolation
* ordering/current revision behavior
* appropriate revision diff behavior
* inaccessible Cave/revision behavior

Reuse existing lower-level revision/diff tests instead of duplicating them at every layer.

Frontend validation should cover the repository's normal:

* TypeScript checks
* build
* relevant existing tests

Add component tests only where they protect meaningful behavior.

Do not add large collections of defensive tests whose only purpose is to prove that removed legacy APIs remain removed.

---

## Milestone 1 acceptance criteria

The milestone is complete when:

* a normal authorized Cave edit creates the existing published revision as before;
* the new UI can display that revision in Cave history;
* field changes display correctly;
* imported changes appear in history with appropriate provenance where available;
* the current revision is obvious;
* an unauthorized user cannot inspect revision history by guessing IDs;
* the backend and frontend validation suites relevant to the change pass.

Do not begin redesigning the proposal workflow before this slice works end-to-end.

---

# Milestone 2 — Complete Change Request Workflow

## Goal

Implement the **entire remaining contributor and reviewer workflow** against the new Cave Revision architecture.

This intentionally remains one milestone.

Do not split:

* proposal API
* proposal UI
* reviewer API
* reviewer UI
* approval

into five separate milestones.

They are one product workflow and should be completed together.

The previous `feature/review-changes` branch is the primary UI reference.

---

# 2.1 Define the application/API surface around the existing domain

First inspect the current change-request entities, proposal-version entities, repositories, services, mutation coordinator, authorization conventions, and current tests.

Then expose the minimum product-facing operations needed by the UI.

The resulting API should support the following behaviors.

---

## 2.2 Create a proposed Cave change

For a proposal against an existing Cave:

* identify the Cave;
* record the exact published revision being used as the base;
* capture the proposed Cave state through the current proposal model;
* create an immutable proposal version;
* associate it with the change request.

Creating the proposal must **not** publish a Cave revision.

Do not infer the base later from "whatever revision is current."

The base revision is part of the proposal's meaning.

---

## 2.3 Save/revise proposals

Where the product permits a contributor or reviewer to revise an existing request, never mutate an existing proposal version in place.

Create another immutable proposal version and move `CurrentProposalVersionId` according to the current architecture.

Retain historical versions for auditability.

If the old review UI used mutable request editing, adapt that interaction to the new versioning behavior rather than copying the implementation literally.

---

## 2.4 Submit and inspect requests

Support the lifecycle required by the current domain.

The product needs to retrieve enough information to render:

### Contributor views

* the user's requests
* status
* Cave
* submitted/updated time
* current proposal version
* reviewer outcome when applicable
* rejection/review information where appropriate
* stale/conflict state when applicable

### Reviewer queue

At minimum:

* Cave
* submitter
* submitted time
* current request status
* concise change summary where practical
* stale/conflict indication

### Request detail

Provide all information required to understand the request without the frontend reconstructing domain semantics:

* request metadata
* base published revision
* current proposal version
* current published revision
* proposed snapshot/state
* authoritative diff
* reviewer information
* decision/reason where applicable
* file/attachment information where applicable
* stale/conflict status

Avoid many frontend round trips when one purpose-built read model can safely provide the review screen.

---

# 2.5 Contributor UX

## Direct edit versus suggest changes

Preserve the distinction established by the current architecture:

* users authorized to edit Cave state directly keep the normal Cave edit workflow;
* users who are allowed to propose but not directly publish changes receive the proposal workflow.

Do not make managers/admins submit routine edits for approval unless the existing permission policy explicitly requires that.

Do not invent permission names or a new permission framework without checking the existing account/feature/authorization system.

---

## Suggest Changes

Add an appropriate **Suggest Changes** action where a user may propose modifications.

Reuse the normal Cave editing experience as much as practical.

The user should be editing a Cave, not filling out an entirely different administrative form simply because the result requires approval.

Where possible:

1. Load the published base.
2. Use the existing Cave editor.
3. Capture the resulting proposed Cave state.
4. Let the user review the changes.
5. Submit the request.

Do not maintain two independent Cave editors.

---

## Review before submission

Before final submission, show the contributor the changes they are proposing.

Reuse/adapt the comparison presentation derived from the previous `CaveReviewComponent`.

Changes should be understandable at the field level rather than presenting raw JSON.

---

## My Requests

Provide a reasonable way for contributors to find their requests.

Reuse the old UI patterns where useful.

The user should be able to distinguish at least the states that actually exist in the new domain, such as:

* draft, if supported;
* pending/review;
* approved;
* rejected;
* stale/conflict/re-review state.

Use the domain's real statuses rather than manufacturing frontend-only lifecycle states.

---

# 2.6 Reviewer UX

Port/adapt the previous review experience instead of replacing it.

The old flow provides a useful baseline:

**review queue → request detail → optional edit/revision → approve or reject**

---

## Review queue

Port/adapt:

* `CaveReviewsPage.tsx`
* `CaveReviewsComponent.tsx`

to the new API.

The queue should quickly answer:

* Which Cave?
* Who submitted it?
* When?
* What is its status?
* Is it stale?
* What should I review next?

Do not overload the queue with the complete proposal diff.

---

## Review detail

Port/adapt:

* `CaveReviewPage.tsx`
* `CaveReviewComponent.tsx`

Use the old component's successful design concept:

* show proposed values;
* clearly identify modified fields;
* let reviewers inspect original/base values;
* present entrances coherently;
* present additions/removals;
* present tags;
* present narrative changes;
* present files.

But feed the component from the **new immutable revision/proposal read model**.

---

## Base vs proposed vs current

There are potentially three meaningful states:

1. the published revision on which the proposal was based;
2. the proposed Cave state;
3. the currently published Cave state.

For a normal non-stale proposal, base and current are the same.

The interface should remain simple in that common case.

For a stale proposal, the UI must make the distinction obvious.

Do not silently replace "base" with "current" in the comparison.

---

# 2.7 Stale proposal behavior

This is a critical product behavior.

Before approval, determine whether the request still targets the current published base according to the current domain/concurrency rules.

If it is stale:

* display a prominent but understandable conflict/re-review state;
* show enough information for the reviewer to understand that the Cave changed after the proposal was based;
* do not pretend that the proposal has already been rebased;
* do not silently merge proposed values over current values;
* do not allow an unsafe normal approval operation to bypass the conflict workflow.

The API should communicate this as a domain result that the UI can deliberately handle, rather than relying on string matching an exception message.

Resolution must preserve the immutable proposal-version/audit model.

If resolving the conflict creates an updated proposal, that should become a new immutable proposal version based on the appropriate published revision and undergo the necessary review rather than rewriting the old version.

---

# 2.8 Reviewer editing

The old branch includes an edit-review experience.

Reuse it **only insofar as it fits the new model**.

A reviewer editing the proposal must not:

* mutate an existing immutable proposal version;
* directly alter the published Cave before approval;
* hide what the original contributor submitted.

If reviewer modification is retained:

1. reuse the normal Cave editor;
2. initialize it with the current proposal;
3. save the result as a new immutable proposal version;
4. preserve who created that version;
5. ensure the final review screen shows the version actually being approved.

Avoid building a special second editor.

---

# 2.9 Approval

Approval is where the new architecture is most important.

The endpoint/application service must delegate final publication to the current Cave published-mutation boundary.

On successful approval:

* validate the proposal;
* validate base/current compatibility;
* validate authorization and tenant boundaries;
* materialize the normalized target Cave state;
* publish through the existing mutation workflow;
* produce the resulting `CaveRevision`;
* link publication provenance to the request;
* persist request status;
* persist reviewer identity;
* persist reviewer metadata;
* handle proposal files correctly;
* commit the relational work atomically according to the current architecture.

There should be **one authoritative publication operation**.

Do not implement an approval-only version of Cave persistence.

Do not create two revisions for a single approved proposal.

The published revision must describe the state that was actually validated and persisted, not blindly duplicate proposal JSON.

---

# 2.10 Rejection

Reviewer rejection should:

* leave published Cave state untouched;
* create no `CaveRevision`;
* preserve the request;
* preserve every proposal version;
* retain reviewer identity;
* retain reviewed time;
* retain the rejection reason/comment according to the current model.

The contributor should be able to understand that the request was rejected and why, where that information is available.

---

# 2.11 Proposal files

Follow `docs/cave-revisions.md`.

Pending proposal files remain staged/account-owned.

They must not appear as published Cave files merely because a proposal was submitted.

Successful approval promotes/associates files through the existing publication transaction semantics.

Respect the existing blob compensation and post-commit destructive-cleanup design.

Do not introduce a second file lifecycle in the React client.

Reviewers should still be able to inspect proposed attachments where authorized.

---

# 2.12 History integration

An approved request should naturally appear later in the Cave's revision history because approval produced a real published revision.

Where provenance supports it, history should indicate that the revision resulted from an approved change request and identify the relevant actors.

Rejected requests must **not** appear as published Cave revisions.

The change-request audit view and published Cave revision history are related but distinct concepts.

Do not conflate them.

---

# 2.13 Milestone 2 tests

Prioritize end-to-end domain behavior.

At minimum cover:

### Proposal lifecycle

* create proposal against an explicit base revision
* immutable proposal version creation
* additional proposal version creation
* submission
* contributor reads own request
* authorized reviewer reads queue/detail

### Authorization

* unauthorized proposal
* unauthorized review
* tenant isolation
* guessing request/revision IDs does not cross account boundaries

### Approval

* valid approval succeeds
* resulting Cave contains validated proposed state
* exactly one appropriate published revision is created
* request/reviewer provenance is retained
* normal Cave mutation rules remain in force

### Rejection

* published Cave remains unchanged
* no revision is created
* audit data remains available

### Stale requests

* Cave changes after request base revision
* ordinary approval cannot silently overwrite/merge newer published state
* explicit stale/conflict result is returned

### Files

Cover meaningful staging/publication/rollback behavior already represented by the current architecture.

Do not create redundant tests for implementation details already heavily covered at lower levels.

---

# Milestone 2 acceptance criteria

This milestone is not complete merely because endpoints exist or because a review page renders.

The following must work as one connected workflow:

### Proposal path

1. User opens a Cave.
2. User selects Suggest Changes.
3. Existing Cave editor is reused.
4. User modifies data.
5. User previews changes.
6. User submits.
7. Request appears in the appropriate request/reviewer views.

### Review and approval path

1. Reviewer opens review queue.
2. Reviewer selects request.
3. Reviewer sees correct base and proposed values.
4. Reviewer can inspect all important Cave/entrance/file changes.
5. Reviewer approves.
6. Published Cave is updated through the existing mutation workflow.
7. Request is marked approved.
8. Exactly one resulting published revision is created.
9. New revision appears correctly in Cave history.

### Rejection path

1. Reviewer rejects.
2. Published Cave is unchanged.
3. No published revision is created.
4. Request and proposal history remain available.

### Stale path

1. Proposal is submitted against revision A.
2. Another authorized operation publishes revision B.
3. Reviewer opens/attempts to approve the old proposal.
4. System recognizes that its base is stale.
5. UI clearly communicates the conflict.
6. System does not silently apply the proposal over revision B.

Do not stop Milestone 2 after only one of these paths works.

---

# Milestone 3 — Integration Hardening and Merge-Ready Completion

## Goal

Turn the completed vertical slices into one polished, internally consistent feature and validate the entire Cave Revision system before it is merged back into `feature/cave-revisions`.

This milestone is for completion, not for inventing another architecture.

---

# 3.1 Navigation and discoverability

Review the old branch's navigation integration and reuse useful patterns.

Ensure users who need them can reasonably discover:

* Cave history
* Suggest Changes
* their submitted requests
* requests requiring review

Do not add navigation merely because an old branch had it if it no longer fits the current application structure.

If a useful pending-review count/badge is straightforward within current application patterns, retain/adapt it.

Avoid introducing a new global state architecture solely for a badge.

---

# 3.2 Complete state UX

Audit all new screens for:

* loading
* empty
* unauthorized
* not found
* stale/conflict
* validation failure
* rejected
* approved
* submission in progress
* backend error

Do not leave raw exceptions, JSON, internal enum names, or database terminology in user-facing surfaces.

Keep the experience consistent with the rest of Planarian.

---

# 3.3 Permission matrix

Exercise the feature with the meaningful categories of users supported by the existing authorization model.

At minimum reason through:

### Direct editor

Can publish authorized Cave edits normally.

Their direct edit produces normal revision history.

### Proposal contributor

Can submit allowed proposed changes but cannot bypass the review boundary.

### Reviewer/manager

Can inspect and make review decisions according to existing permissions.

### Unauthorized user

Cannot gain revision/request data or mutation powers by accessing new routes/endpoints directly.

Do not solve this by inventing a new permission system unless the existing architecture demonstrably lacks a required capability.

---

# 3.4 Concurrency and stale-state validation

Validate actual races rather than only the happy path.

Important scenario:

1. Proposal based on revision A.
2. Cave becomes revision B.
3. Approval arrives using proposal based on A.

The final system must preserve the explicit conflict semantics established by `docs/cave-revisions.md`.

Also verify that normal direct-edit concurrency behavior was not weakened by adding UI endpoints.

---

# 3.5 Import and ordinary edit regression validation

The new UI must not destabilize the already-completed foundation.

Verify:

* normal authorized Cave edits still publish through the revision workflow;
* imports still produce their intended published revisions;
* semantic no-ops do not manufacture revisions;
* direct edits do not accidentally become proposals;
* rejected proposals create no revision;
* approved proposals create the correct revision;
* revision provenance remains understandable.

---

# 3.6 File lifecycle validation

Exercise proposal files through:

* staging
* review
* approval
* relational commit
* publication association
* failure compensation
* rejection/abandonment behavior where implemented

Make sure the frontend does not assume a staged file is a published Cave file.

---

# 3.7 Cleanup

Before considering the feature complete:

Remove:

* temporary DTO adapters that are no longer needed
* dead prototype endpoints
* mocks
* duplicated comparison logic
* unused legacy types copied from `feature/review-changes`
* obsolete TODOs
* abandoned components
* debug logging
* redundant tests created during implementation
* documentation that only narrates temporary implementation steps

Do **not** keep old architecture around "just in case."

Likewise, do not perform broad unrelated cleanup.

---

# 3.8 Documentation

Keep documentation small.

This completion document and the existing `docs/cave-revisions.md` should remain the primary documentation.

Update `docs/cave-revisions.md` only when the completed implementation establishes a durable architectural fact that is missing or different.

Do not create a separate document for every endpoint, component, milestone, or temporary decision.

Prefer code, types, tests, and focused comments for implementation details.

---

# 3.9 Final automated validation

Run the repository-standard validation for both backend and frontend.

At minimum, identify and use the actual repository-documented commands for:

* backend restore/build
* backend test suite
* frontend install/build
* TypeScript checking if separate
* frontend tests if present
* repository linting/format validation where configured

Use the actual solution path and current CI configuration.

Do not invent a second validation command when the repository already defines the canonical one.

Fix failures caused by this work before stopping.

If an unrelated pre-existing failure exists, prove that it is pre-existing and document it clearly rather than simply declaring the branch complete.

---

# 3.10 Final manual acceptance walkthrough

Before declaring completion, manually reason through or exercise this complete product flow.

## A. Normal direct edit

1. Open Cave.
2. Authorized direct editor edits it.
3. Save.
4. Cave updates.
5. One appropriate revision is created.
6. History displays the change and provenance.

## B. Proposed change

1. Proposal-only contributor opens Cave.
2. Selects Suggest Changes.
3. Familiar Cave editor opens.
4. Makes multiple types of changes.
5. Reviews diff.
6. Submits.
7. Request becomes visible to reviewer.

## C. Approval

1. Reviewer opens queue.
2. Opens request.
3. Sees submitter and
