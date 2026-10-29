# Authorization boundaries

Planarian has separate platform and account authorization domains. Permission names are not a single global privilege ladder.

## Platform administration

`PlanarianAdmin` is a global, platform-scoped permission. It is intended for explicit platform operations such as Planarian Settings and account provisioning.

`PlanarianAdmin` does **not** imply Account `Admin`, Cave `Manager`, Cave `View`, `AdminManager`, or an account-admin export override. A platform administrator receives account authority only when an account-scoped permission is explicitly assigned. Account creation does this intentionally by adding the creator to the new Account and granting that user Account `Admin`.

Global platform permissions must not be folded into account-scoped permission queries. Direct platform checks must require a global `UserPermission` (`AccountId == null`) with the `PlanarianAdmin` key.

## Account and Cave hierarchy

Within an Account, the supported hierarchy is:

```text
Admin > Manager > View
```

`AdminManager` is a synthetic account capability used by account user-management UI/workflows. It is satisfied only by Account `Admin` or an all-locations Cave `Manager` grant; a State-, County-, or Cave-scoped Manager does not receive it. `Export` is also synthetic: it follows the Account export setting, with Account `Admin` as the administrative override.

Neither synthetic capability is satisfied by `PlanarianAdmin`.

## Capability checks versus resource checks

`RequestUser.HasCavePermission(permissionKey)` is a coarse capability check used by named authorization policies and account-level UI routing. For Cave permissions, it answers whether the user has that effective capability anywhere in the current Account; it does not authorize a particular Cave, County, or State.

Operations against a specific resource use the scoped overload and pass the resource identity. Resource authorization must not be inferred from the coarse capability check alone.

## Scope resolution

Scope-sensitive Cave checks use the most specific resource identifier supplied by the caller: Cave, then County, then State, then Account-wide scope. Broader permissions may satisfy a narrower check, but narrower permissions must never be promoted by matching descriptive County or State metadata on another resource. In particular, a Cave-only permission does not become County or State authority merely because that Cave belongs to the requested County or State.

`UserCavePermissions` expands broader CavePermission rows to the Caves and Counties they cover. Cave checks therefore match the expanded Cave ID, County checks match the expanded County ID, and State/all-location checks use the owning CavePermission scope rather than treating the view's descriptive State metadata as authority.

## Permission delegation

A Cave `Manager` may grant or remove Cave `View`/`Manager` access only at the same scope they control or a narrower scope. State Managers may delegate State, County, or Cave access; County Managers may delegate County or Cave access; Cave Managers may delegate only Cave access. Account-wide Managers and Account `Admin` may delegate across the Account. Existing broader grants that the acting Manager cannot administer remain visible where needed for safe editing, but must be preserved and cannot be removed or widened by that Manager.

Regional permission selectors may expose a State as a container when the acting Manager controls one or more Counties in it. That does not itself authorize a State-wide grant: the server's scoped permission check remains authoritative. Cave-only Managers use the individual-Cave permission path and do not receive regional choices.

Creating a new Cave requires Manager authority at the destination County (or a broader State/account scope). A Cave-only Manager may manage the Cave they control but cannot create a sibling Cave merely because it is in the same County or State. The new-Cave form filters State/County choices using the effective Manager scope; editing an existing Cave retains the existing move semantics and is authorized against the current Cave.

## ASP.NET Core policies

Named ASP.NET Core policies express one semantic permission each. For example, the `Admin` policy requires `Admin`, and the `Manager` policy requires `Manager`. `RequestUser` owns resolution of the Account/Cave hierarchy. Do not duplicate role inheritance in startup policy registration.

This keeps endpoint policy declarations stable while preventing platform permissions from becoming accidental tenant-wide superuser permissions.

## Client projection

The application session may include both account-scoped permissions and the global `PlanarianAdmin` permission. Client permission helpers must preserve the same domain separation as the server: `PlanarianAdmin` only satisfies `PlanarianAdmin`.

A platform administrator does not need a selected Account to receive `PlanarianAdmin` in app initialization or to use Planarian Settings.
