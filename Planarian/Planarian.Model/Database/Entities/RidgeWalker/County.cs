using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class County : EntityBaseNameId
{
    public string DispayId { get; set; }
    
    public ICollection<Cave> Caves { get; set; } = null!;
}