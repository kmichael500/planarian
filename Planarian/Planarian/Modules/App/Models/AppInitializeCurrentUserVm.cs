namespace Planarian.Modules.App.Models;

public class AppInitializeCurrentUserVm
{
    public AppInitializeCurrentUserVm(
        string id,
        string fullName,
        string? currentAccountId)
    {
        Id = id;
        FullName = fullName;
        CurrentAccountId = currentAccountId;
    }

    public string Id { get; set; }
    public string FullName { get; set; }
    public string? CurrentAccountId { get; set; }
}
