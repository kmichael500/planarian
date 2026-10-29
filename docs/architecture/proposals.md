# Proposal and review architecture

This page explains how suggested Cave changes remain separate from published state until approval.

## Core idea

A proposal is not a mutable alternate Cave. “Immutable” here is an application/persistence invariant rather than a database trigger that would reject arbitrary SQL updates.

Instead:

- CaveChangeRequest is the workflow envelope;
- CaveProposalVersion is an immutable version of proposed desired state;
- application/persistence code enforces that immutability by creating new version rows rather than updating prior ProposalJson;
- an existing-Cave proposal version points to the exact CaveRevision it was authored against;
- a new-Cave proposal version has a null published base and reserves the future Cave ID instead of creating a placeholder Cave;
- CurrentProposalVersionId selects the active version;
- approval publishes the selected immutable version through the normal Cave mutation boundary.

~~~mermaid
flowchart LR
    R10["Published Cave R10"]
    Request["CaveChangeRequest"]
    V1["Proposal V1<br/>base R10"]
    Changes["Request status<br/>ChangesRequested"]
    Current["Current published revision<br/>R10 or later"]
    V2["Proposal V2<br/>base = current revision when authored"]
    Published["New published revision"]

    R10 -->|"author V1 against"| V1
    Request -->|"current proposal pointer"| V1
    V1 -->|"reviewer requests changes"| Changes
    Changes -->|"author creates revised version"| V2
    Current -->|"version records exact base"| V2
    Request -->|"moves current pointer"| V2
    V2 -->|"resubmit, then approval"| Published
~~~

## Workflow state machine

~~~mermaid
stateDiagram-v2
    [*] --> Pending: create request
    Pending --> ChangesRequested: reviewer requests changes
    ChangesRequested --> Pending: author resubmits
    Pending --> Approved: reviewer approves
    Pending --> Rejected: reviewer rejects
    Approved --> [*]: terminal
    Rejected --> [*]: terminal
~~~

The state policy is centralized in CaveChangeRequestStatePolicy.

Important consequences:

- Pending is the reviewer-facing publishable state.
- ChangesRequested is the author-facing revision state.
- both Pending and ChangesRequested are open and block hard Cave deletion;
- Approved and Rejected are terminal.

## Proposal authoring

CaveChangeRequestService provides:

- authoring context;
- preview;
- initial request/version creation;
- additional immutable version creation;
- version/detail presentation;
- review decisions.

The authoring path builds CaveProposalSnapshotV1, which captures exact intent rather than only a sparse patch.

### Why complete intent matters

Files are a good example.

A proposal version explicitly records each relevant File as:

- RetainPublished;
- RemovePublished;
- PublishStaged.

Absence from some side table is not interpreted as an implicit remove operation.

Line plots similarly store exact desired GeoJSON in the proposal version, so later changes to current CaveGeoJson rows cannot silently redefine an older proposal.

## Proposal version contents

CaveProposalSnapshotV1 contains:

- Cave scalar desired state;
- State/County IDs and proposal-time labels;
- County-number intent;
- Cave and Entrance tag references;
- new People-tag creation intents where allowed;
- Entrances with stable/new IDs;
- File intents;
- full normalized proposed line-plot GeoJSON.

This differs intentionally from CavePublishedSnapshotV1, whose line-plot history stores only ID/name/content hash.

## Approval sequence

~~~mermaid
sequenceDiagram
    actor Reviewer
    participant API as CaveChangeRequestController
    participant Workflow as CaveChangeRequestService
    participant Requests as CaveChangeRequestRepository
    participant Files as FileService
    participant Mutation as CaveMutationCoordinator
    participant Cave as CaveService
    participant DB as PostgreSQL

    Reviewer->>API: Approve request + expected proposal version
    API->>Workflow: ApproveAsync
    Workflow->>Requests: Load current request/version/base/current revision
    Workflow->>Workflow: Verify reviewer permission and request state
    Workflow->>Workflow: Verify expected proposal version
    Workflow->>Workflow: Verify published base is still current, or reserved Cave still does not exist
    Workflow->>Requests: Plan staged File publications
    Workflow->>Files: Verify external objects exist
    Workflow->>Mutation: ExecuteOwnedTransaction
    Mutation->>Cave: Publish exact proposal desired state
    Cave->>DB: Apply aggregate + CaveRevision
    Workflow->>Requests: Mark request approved + review event
    Requests->>DB: Persist workflow state
    DB-->>Mutation: Commit one relational transaction
    Workflow->>Files: Best-effort cleanup discarded staged objects
    API-->>Reviewer: Approved revision ID
~~~

The Cave publication and request approval are relationally atomic because both execute inside the same owned transaction.

External object deletion is outside that transaction and occurs only after commit.

## Concurrency boundaries

Approval checks both:

1. **Expected proposal version** — prevents a reviewer from approving an older proposal version after someone created a newer one.
2. **Published base / reserved identity** — existing-Cave proposals require the authored base revision to still be current;
   new-Cave proposals require the reserved Cave ID to remain unused.

Cave xmin concurrency remains an additional protection if another write races after those checks.

There is no silent auto-rebase.

If a request becomes stale, a new proposal version can be authored against current published state and reviewed again.

## Rejection and request-changes

Rejection:

- records workflow/audit state;
- does not mutate the Cave;
- creates no CaveRevision.

Request changes:

- records the reviewer event/message;
- moves the workflow to ChangesRequested;
- does not mutate published Cave state.

Resubmit:

- moves the request back to Pending;
- preserves proposal/review history.

## Permissions

Existing-Cave request/version/staged-file/history access follows current Cave visibility.

For a new-Cave request before publication, the submitter may access and revise it only while retaining effective Cave
`View` capability somewhere in the current Account. Current Managers of the proposal's destination may review it even
though no Cave exists there yet.

Current View permission is sufficient to propose. Existing-Cave review/approval requires Manager authority for the Cave;
new-Cave review/approval requires Manager authority for the current proposed destination.

Request ownership does not become a permanent bypass if the submitter later loses View capability. On create approval,
the submitter receives explicit Cave View only if they are still an Account member.

See [Authorization](../authorization.md) for the durable permission model.

## Read the code in this order

1. Planarian.Web/src/Modules/Caves/Pages/SuggestCaveChangesPage.tsx
2. Planarian.Web/src/Modules/Caves/Pages/ReviseCaveChangeRequestPage.tsx
3. Planarian.Web/src/Modules/Caves/Pages/CaveChangeRequestPage.tsx
4. Planarian/Planarian/Modules/Caves/Controllers/CaveChangeRequestController.cs
5. Planarian/Planarian/Modules/Caves/Services/CaveChangeRequestService.cs
6. Planarian/Planarian/Modules/Caves/Revisions/CaveChangeRequestRepository.cs
7. Planarian/Planarian.Model/Database/Entities/RidgeWalker/CaveChangeRequest.cs
8. Planarian/Planarian.Model/Database/Revisions/CaveProposalSnapshotV1.cs
9. Planarian/Planarian/Modules/Caves/Services/CaveService.cs for final publication
