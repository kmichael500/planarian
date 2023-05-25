using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Map : EntityBase
{
    public string CaveId { get; set; }
    
    public TagType MapStatusTag { get; set; }
    
    public virtual Cave Cave { get; set; } = null!;
}