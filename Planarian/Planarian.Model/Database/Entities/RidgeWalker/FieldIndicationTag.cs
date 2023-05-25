using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class FieldIndicationTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string EntranceId { get; set; }

    public TagType TagType { get; set; }
    public Entrance Entrance { get; set; }
}