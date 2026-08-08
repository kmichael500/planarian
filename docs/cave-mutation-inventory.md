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

All published Cave mutations are intended to pass through one coordinator.
Pending proposals are workflow data and do not count as published mutations.

