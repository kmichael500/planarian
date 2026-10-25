# ADR 0004 — Upload new Cave Files once and represent publication relationally

**Status:** Accepted for the pre-release Cave-revisions target
**Scope:** New Cave/staged File lifecycle

## Context

Object storage and PostgreSQL cannot share one atomic transaction.

The older staged-to-published model copied or renamed blob content when a File crossed lifecycle boundaries. That adds:

- extra network I/O;
- another failure point during publication;
- temporary duplicate content;
- changing object identity for the same logical File.

Large object transfers also should not hold a PostgreSQL transaction open.

## Decision

Allocate the File ID before upload and write new authoring content once to a stable application-assigned address based on that identity. The lifecycle treats that address/content as immutable after creation. The current address is:

~~~text
objects/files/{fileId}
~~~

The staged File row is account-owned with:

- CaveId null;
- ExpiresOn set;
- stable storage address.

Publication changes relational lifecycle only:

- same File ID;
- same object address;
- CaveId becomes the target Cave;
- ExpiresOn becomes null.

Application logic for this lifecycle depends on IObjectStorage rather than Azure SDK types.

Upload occurs before the short relational insert. If the relational insert fails, the service attempts compensating object deletion.

## Consequences

### Positive

- Publication does not copy large File bytes.
- Object identity remains stable across staging and publication.
- External storage work is kept outside long database transactions.
- The application-level Cave/File flow is not coupled directly to Azure types.
- Retry/failure reasoning is simpler.

### Costs

- Staged objects can temporarily outlive abandoned authoring and require cleanup.
- External upload can succeed before relational persistence, so compensation remains necessary.
- Provider neutrality is scoped; older storage workflows still use Azure-specific APIs.
- Object-address immutability is an application invariant; the production adapter behavior should be contract-tested (including retries/accidental reuse) rather than assumed from the interface alone.

## Alternatives considered

### Copy staging object to a published key

Rejected because it creates unnecessary external I/O and another publication failure boundary.

### Store File bytes in PostgreSQL

Rejected because Planarian already uses object storage for large Files and the relational database should not become the bulk byte store.

### Expose Azure Blob clients directly to Cave workflow code

Rejected for new Cave/File publication behavior because it leaks provider-specific infrastructure into application orchestration.

## Related documentation

- [Files and storage](../files-and-storage.md)
- [Retained File object ADR](0005-retained-file-objects.md)
- [Data-access architecture](../../data-access-architecture.md)
