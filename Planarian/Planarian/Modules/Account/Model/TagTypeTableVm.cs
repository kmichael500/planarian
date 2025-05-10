namespace Planarian.Modules.Account.Model;

public class TagTypeTableVm
{
    public string TagTypeId { get; set; }
    public string Name { get; set; }
    public bool IsUserModifiable { get; set; }
    public bool CanRename { get; set; }

    public int Occurrences { get; set; }
}