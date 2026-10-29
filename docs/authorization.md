# Authorization boundaries

Planarian has separate platform and account authorization domains. Permission names are not a single global privilege ladder.

## Platform administration

`PlanarianAdmin` is a global, platform-scoped permission. It is intended for explicit platform operations such as Planarian Settings and account provisioning.

`PlanarianAdmin` does **not** imply Account `Admin`, Cave `Manager`, Cave `View`, `AdminManager`, or an account-admin export override. A platform administrator receives account authority only when an account-scoped permission is explicitly assigned. Account creation does this intentionally by adding the creator to the new Account and granting that user Account `Admin`.

Global platform permissions must not be folded into account-scoped permission queries. Direct platform checks must require a global `UserPermission` (`AccountId == null`) with the `PlanarianAdmin` key. An account-scoped `UserPermission` carrying the `PlanarianAdmin` key is malformed persisted data and must be ignored by both server authorization and client permission projection.

## Account and Cave hierarchy

Within an Account, the supported hierarchy is:

```text
Admin > Manager > View
```

`Planarian.Model.Shared.PermissionPolicy` is the canonical executable source for this role inheritance, the account-scoped persisted role vocabulary used by account management and session projection, and the classification of platform/synthetic capabilities that must not be treated as resource-scoped grants. Feature repositories still own tenant filters and State/County/Cave scope resolution; they consume the shared policy rather than restating the hierarchy. The PostgreSQL `UserCavePermissions` view necessarily repeats the persisted role names in SQL; a unit contract test compares that view with `PermissionPolicy` so the two cannot silently drift.

`AdminManager` is a synthetic account capability used by account user-management UI/workflows. It is satisfied only by Account `Admin` or an all-locations Cave `Manager` grant; a State-, County-, or Cave-scoped Manager does not receive it. `Export` is also synthetic: it follows the Account export setting, with Account `Admin` as the administrative override.

Neither synthetic capability is satisfied by `PlanarianAdmin`.

## Capability checks versus resource checks

`RequestUser.HasCavePermission(permissionKey)` is a coarse capability check used by named authorization policies and account-level UI routing. For Cave permissions, it answers whether the user has that effective capability anywhere in the current Account; it does not authorize a particular Cave, County, or State.

Operations against a specific resource use the scoped overload and pass the resource identity. Resource authorization must not be inferred from the coarse capability check alone.

## Scope resolution

Scope-sensitive Cave checks use the most specific resource identifier supplied by the caller: Cave, then County, then State, then Account-wide scope. Broader permissions may satisfy a narrower check, but narrower permissions must never be promoted by matching descriptive County or State metadata on another resource. In particular, a Cave-only permission does not become County or State authority merely because that Cave belongs to the requested County or State.

`UserCavePermissions` expands broader CavePermission rows to existing Caves. Cave checks may therefore match the expanded Cave ID. The view enforces the permission domain at its source: Cave grants must reference `PermissionType.Cave` **and** one of the supported Cave role keys (`View` or `Manager`), while account-wide Cave expansion is produced only by a `PermissionType.User` `Admin` and emits only those same Cave roles. Malformed rows that cross grant families or use reserved/unsupported Cave role keys must not acquire Cave visibility merely because a permission key happens to match another authorization domain. County, State, and account-scope checks use the owning `CavePermission` grant rows instead of relying on expansion through existing Caves; this is required so authority over an empty County still exists. County-to-State inheritance is derived from the authoritative County row rather than from client-supplied descriptive metadata. Creation-request visibility/review queries and regional permission selectors preserve the same User-vs-Cave permission-domain boundary when they query raw grants directly.

## Permission delegation

A Cave `Manager` may grant or remove Cave `View`/`Manager` access only at the same scope they control or a narrower scope. State Managers may delegate State, County, or Cave access; County Managers may delegate County or Cave access; Cave Managers may delegate only Cave access. Account-wide Managers and Account `Admin` may delegate across the Account. Existing broader grants that the acting Manager cannot administer remain visible where needed for safe editing, but must be preserved and cannot be removed or widened by that Manager.

Regional permission selectors may expose a State as a container when the acting Manager controls one or more Counties in it. That does not itself authorize a State-wide grant: the server's scoped permission check remains authoritative. Cave-only Managers use the individual-Cave permission path and do not receive regional choices.

Directly publishing a new Cave requires Manager authority at the destination County (or a broader State/account scope). A Cave-only Manager may manage the Cave they control but cannot directly publish a sibling Cave merely because it is in the same County or State.

## Permission persistence and membership lifecycle

Persisted permission definitions are identified by the pair `(PermissionType, Key)`, not by `Key` alone. That pair is unique in the database. Write paths must resolve definitions using both values and enforce the supported grant vocabulary: account user management may persist only User `Admin`, while Cave delegation may persist only Cave `View` or `Manager`. `PlanarianAdmin`, `AdminManager`, and `Export` are not assignable account-management roles, and malformed cross-domain rows must never be selected merely because their textual key matches. Authorization also fails closed for unknown semantic permission keys, including the public scoped-capability probe; an unsupported same-named persisted `UserPermission` is not authority.

Creating either a `CavePermission` or an account-scoped `UserPermission` is serialized against account-access revocation through the target `AccountUser` row. Grant workflows acquire `FOR KEY SHARE` inside the grant transaction and revalidate that membership still exists; revocation acquires `FOR UPDATE`, then removes Cave grants, User grants, and membership atomically. Therefore a grant that wins first is subsequently removed by revocation, while a revocation that wins first causes the grant to fail closed instead of leaving an orphaned permission row.

A user with effective Cave `View` capability in the current Account may instead submit a new-Cave proposal for any State/County configured for that Account, even when the user cannot view any existing Cave in that destination. The proposal reserves the eventual Cave ID but does not create a Cave row or fake revision. Before publication, the submitter can read/revise that creation request only while they retain effective Cave `View` capability in the Account; current Managers of the proposed destination can review it independently of the submitter's later access. A reviewer who revises the proposal may not move it outside their current Manager scope; the submitter may change the proposed destination and thereby route the request to the new destination's Managers.

New-Cave proposals use authoritative account+County Cave data for automatic and first-available County Cave Number calculation, including Caves hidden from the submitter and archived Caves whose identifiers remain reserved. Manual County Cave Number selection and first-available allocation remain limited to a Manager for the selected County; View-only proposals always use automatic-next allocation. Approval transactionally creates the Cave, publishes its first `UserSubmission`/`Create` revision, and marks the request approved. The submitter receives an explicit Cave-scoped `View` permission only when they are still a member of the Account at approval time; approval remains valid if the original submitter has left the Account. After the Cave exists, request/history access follows normal current Cave visibility rather than historical destination authority.

## ASP.NET Core policies

Named ASP.NET Core policies express one semantic permission each. For example, the `Admin` policy requires `Admin`, and the `Manager` policy requires `Manager`. `RequestUser` owns resolution of the Account/Cave hierarchy. Do not duplicate role inheritance in startup policy registration.

This keeps endpoint policy declarations stable while preventing platform permissions from becoming accidental tenant-wide superuser permissions.

## Client projection

The application session may include both account-scoped permissions and the global `PlanarianAdmin` permission. Projection version 2 expands persisted account roles on the server into their effective coarse capabilities before returning the session (`Admin` emits `Admin`, `Manager`, and `View`; `Manager` emits `Manager` and `View`). For version-2 sessions the browser performs exact membership checks and does not implement the current role hierarchy. Because the web and API are deployed independently, the browser retains a version-1 compatibility adapter only when the projection-version field is absent or older; this preserves rolling deployments and rollback against an older API without making the legacy adapter authoritative for current behavior. Account-scoped permission projection excludes the `PlanarianAdmin` key even when malformed persisted data references it; app initialization adds `PlanarianAdmin` only after the server validates a global `UserPermission`, so `PlanarianAdmin` only satisfies itself.

A platform administrator does not need a selected Account to receive `PlanarianAdmin` in app initialization or to use Planarian Settings.
