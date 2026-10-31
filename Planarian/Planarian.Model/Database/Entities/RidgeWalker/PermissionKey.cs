namespace Planarian.Model.Database.Entities.RidgeWalker;

public static class PermissionKey
{
    public const string View = "View";
    public const string Manager = "Manager";
    // Platform-scoped administration. It does not imply Account Admin, Manager, or View access.
    public const string PlanarianAdmin = "PlanarianAdmin";
    public const string Admin = "Admin";
}

public static class PermissionPolicyKey
{
    public const string View = PermissionKey.View;
    public const string Manager = PermissionKey.Manager;
    public const string PlanarianAdmin = PermissionKey.PlanarianAdmin;
    public const string Admin = PermissionKey.Admin;
    public const string AdminManager = "AdminManager"; // Synthetic policy key; not persisted.
    public const string Export = "Export"; // Synthetic policy key; not persisted.
}
