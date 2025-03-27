namespace Planarian.Model.Database.Entities.RidgeWalker;

public static class PermissionKey
{
    public const string View = "View";
    public const string Manager = "Manager";
    public const string PlanarianAdmin = "PlanarianAdmin";
    public const string Admin = "Admin";
}

public static class PermissionPolicyKey
{
    public const string View = "View";
    public const string Manager = "Manager";
    public const string PlanarianAdmin = "PlanarianAdmin";
    public const string Admin = "Admin";
    public const string AdminManager = "AdminManager"; // Not actually in the database
    public const string Export = "Export"; // Not actually in the database
}