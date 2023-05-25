using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class GeologyTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string CaveId { get; set; }

    public TagType TagType { get; set; }
    public Cave Cave { get; set; }
}