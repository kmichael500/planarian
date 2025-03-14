namespace Planarian.Modules.Account.Model;

public class UserPermissionVm
{
    public UserPermissionVm(string id, string permissionKey, string display, string description)
    {
        Id = id;
        PermissionKey = permissionKey;
        Display = display;
        Description = description;
    }
    public string Id { get; set; } = null!;
    public string PermissionKey { get; set; } = null!;
    public string Display { get; set; } = null!;
    public string Description { get; set; } = null!;
}