using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class GeoJsonUploadVm
{
    [MaxLength(PropertyLength.Id)] public string? Id { get; set; }
        
    [Required]
    public string GeoJson { get; set; } = null!;
    [Required]
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
}