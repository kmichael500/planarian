namespace Planarian.Modules.Account.Model;

public class PermissionSelectListData
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Key { get; set; } = null!;
    public int SortOrder { get; set; }
}