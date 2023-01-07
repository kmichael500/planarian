using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class ProjectInvitation : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;
}