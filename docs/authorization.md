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

`AdminManager` is a synthetic account capability used by account user-management UI/workflows. `Export` is also synthetic: it follows the Account export setting, with Account `Admin` as the administrative override.

Neither synthetic capability is satisfied by `PlanarianAdmin`.

## ASP.NET Core policies

Named ASP.NET Core policies express one semantic permission each. For example, the `Admin` policy requires `Admin`, and the `Manager` policy requires `Manager`. `RequestUser` owns resolution of the Account/Cave hierarchy. Do not duplicate role inheritance in startup policy registration.

This keeps endpoint policy declarations stable while preventing platform permissions from becoming accidental tenant-wide superuser permissions.

## Client projection

The application session may include both account-scoped permissions and the global `PlanarianAdmin` permission. Client permission helpers must preserve the same domain separation as the server: `PlanarianAdmin` only satisfies `PlanarianAdmin`.

A platform administrator does not need a selected Account to receive `PlanarianAdmin` in app initialization or to use Planarian Settings.
