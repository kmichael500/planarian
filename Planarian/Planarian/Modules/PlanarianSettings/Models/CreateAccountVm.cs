using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.PlanarianSettings.Models;

public class CreateAccountVm
{
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [MaxLength(PropertyLength.Delimiter)]
    public string? CountyIdDelimiter { get; set; }

    [Required]
    public IEnumerable<string> StateIds { get; set; } = new List<string>();

    public bool DefaultViewAccessAllCaves { get; set; }

    public bool ExportEnabled { get; set; }
}
