# ADR 0003 — Store change proposals as immutable versions

**Status:** Accepted for the pre-release Cave-revisions target
**Scope:** Cave suggestion and review workflow

## Context

A reviewer needs to know exactly what was proposed and exactly which published Cave state the proposal was based on.

If one mutable proposal row were edited in place:

- review history could be rewritten;
- an approval could publish different content than the reviewer saw;
- a current Cave change could silently change the meaning of a pending proposal;
- request-changes/resubmit cycles would lose provenance.

Sparse patch semantics also become difficult when Files, Entrances, People-tag creation, and line plots are involved.

## Decision

CaveChangeRequest is a mutable workflow envelope.

Each CaveProposalVersion is treated as immutable and append-only by application persistence: an edit creates another row rather than updating prior ProposalJson. This is not enforced by a database trigger against arbitrary SQL updates.

A version stores:

- its own BaseRevisionId;
- PreviousProposalVersionId;
- a versioned CaveProposalSnapshotV1 containing exact proposed desired state.

CaveChangeRequest.CurrentProposalVersionId points to the active version.

Review transitions update the request/status and append review events; they do not rewrite prior proposal versions.

Approval requires:

- the expected active proposal version;
- the version's published base to still be current;
- current reviewer authorization.

Approval publishes the exact selected proposal version through normal Cave publication.

There is no silent automatic rebase. A stale request is revised into a new immutable version against current state and reviewed again.

## Consequences

### Positive

- Reviewers and authors can inspect what each iteration actually contained.
- Approval cannot drift to mutable current form state.
- Proposal history remains meaningful after the Cave changes.
- Exact staged File and line-plot intent survives independently of current Cave state.
- Concurrency failures are explicit.

### Costs

- Proposal storage is append-only and may contain repeated desired-state data.
- Presentation must compare current, base, previous proposed, and selected version carefully.
- Staged File lifecycle has to preserve objects needed by unresolved versions.

## Alternatives considered

### Mutate one proposal row in place

Rejected because it destroys review/audit provenance.

### Store only a field patch

Rejected because aggregate intent across removals, Files, new Entrances, and line plots becomes ambiguous.

### Automatically rebase a stale proposal

Rejected because it could publish a combination nobody explicitly authored or reviewed.

## Related documentation

- [Proposal architecture](../proposals.md)
- [Data model](../data-model.md)
- [Cave revision contract](../../cave-revisions.md)
