using System.ComponentModel.DataAnnotations;

namespace Planarian.Modules.Caves.Models;

public class GeoJsonUploadVm
{
    public string? Id { get; set; }
        
    [Required]
    public string GeoJson { get; set; } = null!;
}