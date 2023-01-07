using System.ComponentModel.DataAnnotations;

namespace Planarian.Model.Shared.Base;

public abstract class EntityBaseNameId : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;
}
public abstract class EntityBase
{
    protected EntityBase()
    {
        // Temporary Id for EF Core change tracking
        Id = Guid.NewGuid().ToString();
    }
    [Key][MaxLength(10)] [Required] public string Id { get; set; } = null!;
    [Required] public DateTime CreatedOn { get; set; } 
    public DateTime? ModifiedOn { get; set; } = null!;
    [Required] public string CreatedByUserId { get; set; } = null!;
    [Required] public string CreatedByName { get; set; } = null!;
}