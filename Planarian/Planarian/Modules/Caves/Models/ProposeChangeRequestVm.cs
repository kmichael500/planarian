using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;

namespace Planarian.Modules.Caves.Models;

public class ProposeChangeRequestVm
{
    public string? Id { get; set; } 
    public AddCaveVm Cave { get; set; } = null!;
}