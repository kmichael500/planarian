using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Projects;

public class CreateOrEditProject : IProject
{
    [MaxLength(PropertyLength.Id)] public string? Id { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;
}