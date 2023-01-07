using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.Projects;

public interface IProject
{
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; }
}