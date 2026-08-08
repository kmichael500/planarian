# Cave mutation inventory

| Mutation | Cave revision | Source |
| --- | --- | --- |
| Manager create | Yes | ManagerEdit |
| Manager edit | Yes | ManagerEdit |
| Archive/unarchive | Yes | ManagerEdit |
| Hard delete | Yes, final tombstone | ManagerEdit |
| Cave CSV changes | Yes | Import |
| Entrance CSV changes | Yes | Import |
| Published file association/metadata | Yes | ManagerEdit or appropriate writer |
| TagType rename/delete | No synthetic Cave revision | Taxonomy |
| CaveGeoJson | Excluded in V1 | Separate domain |

All published Cave mutations are intended to cross the same accepted-history
boundary. Interactive manager and Cave-file mutations use
`CaveMutationCoordinator`; bulk Cave/Entrance imports use
`ImportRevisionPublisher` inside their executor transaction so the same
snapshot, provenance, no-change, and revision-pointer invariants can be applied
without per-row coordinator transactions. Pending proposals are workflow data
and do not count as published mutations until an approval publishes them.
