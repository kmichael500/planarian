# ADR 0003 — Store change proposals as immutable versions

**Status:** Accepted
**Scope:** Cave suggestion and review workflow

## Context

A reviewer needs to know exactly what was proposed and, for an existing Cave, exactly which published Cave state the
proposal was based on. A proposal that creates a Cave has no published base yet.

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

- its own BaseRevisionId (`null` only for a new-Cave creation proposal);
- PreviousProposalVersionId;
- a versioned CaveProposalSnapshotV1 containing exact proposed desired state.

CaveChangeRequest.CurrentProposalVersionId points to the active version.

Review transitions update the request/status and append review events; they do not rewrite prior proposal versions.

Approval requires:

- the expected active proposal version;
- for an existing Cave, the version's published base to still be semantically compatible with current published state;
- for a new Cave, the reserved Cave ID to remain unused;
- current reviewer authorization for the Cave or proposed destination, respectively.

An identity-preserving reference-metadata change, such as a TagType or County rename, is compatible: it does not rewrite the immutable proposal version, and publication uses the current label for the unchanged stable identity. Stable-ID remaps/merges and substantive Cave aggregate changes are not compatible.

Approval publishes the exact selected proposal intent through normal Cave publication. When only compatible reference metadata advanced, the accepted revision follows the actual current Cave revision while the proposal version retains its authored base and captured historical labels.

There is no silent automatic rebase of a stale proposal. A request made stale by a substantive or identity-changing publication is revised into a new immutable version against current state and reviewed again.

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

Rejected because it could publish a combination nobody explicitly authored or reviewed. Crossing an identity-preserving reference-metadata rename is not a stale rebase: proposal intent and stable reference identities are unchanged, and the immutable proposal version is not rewritten.

## Related documentation

- [Proposal architecture](../proposals.md)
- [Data model](../data-model.md)
- [Cave revision contract](../../cave-revisions.md)
