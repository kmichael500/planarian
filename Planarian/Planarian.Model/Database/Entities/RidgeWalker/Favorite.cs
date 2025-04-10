using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Favorite : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string? AccountId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }

    [MaxLength(PropertyLength.Max)] public string? Notes { get; set; }

    public IEnumerable<string> Tags { get; set; } = new HashSet<string>();
    
    public virtual Account Account { get; set; } = null!;
    public virtual Cave Cave { get; set; } = null!;
}