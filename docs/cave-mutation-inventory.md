# Cave mutation inventory

## Implemented published mutation paths

| Mutation | Publication boundary | Revision source/status |
| --- | --- | --- |
| Manager create/edit | `CaveMutationCoordinator` | `ManagerEdit` |
| Archive/unarchive | `CaveMutationCoordinator` | `ManagerEdit` |
| Hard delete | `CaveMutationCoordinator` | `ManagerEdit`; final tombstone |
| Cave CSV import | `ImportRevisionPublisher` in executor transaction | `Import` |
| Entrance CSV import | `ImportRevisionPublisher` in executor transaction | `Import` |
| Published Cave file association/metadata | `CaveMutationCoordinator` | `ManagerEdit` or authorized writer |

These paths publish actual normalized state, suppress semantic no-ops, and
advance `Cave.CurrentRevisionId` atomically. TagType rename/delete creates no
synthetic Cave revision; a later Cave publication captures the changed label as
historical reference metadata. CaveGeoJson remains a separate V1 domain.

## Implemented future-work foundation

| Foundation | Current status |
| --- | --- |
| `CaveChangeRequest` | Entity, relationships, status, base/current/approved links, reviewer metadata |
| `CaveProposalVersion` | Immutable/versioned proposal JSON V1 persistence model |
| Staged proposal files | `CaveChangeRequestStagedFile` and tenant-qualified File ownership/FKs |
| User-submission provenance | `CaveRevision.Source.UserSubmission` and change-request linkage columns |

These rows and columns are schema/model infrastructure, not operational request
actions.

## Future workflow

| Action | Status |
| --- | --- |
| Submit a Cave correction or new-Cave proposal | Not implemented |
| Append a user/reviewer amendment | Not implemented |
| Retrieve requests and operate a review queue | Not implemented |
| Approve with conflict checks and materialization | Not implemented |
| Reject while retaining the audit record | Not implemented |
| Publish approved staged files and `UserSubmission` history | Not implemented |

Future approval must cross the same accepted-history boundary as other published
mutations. Pending proposals are workflow data and never count as published Cave
state.
