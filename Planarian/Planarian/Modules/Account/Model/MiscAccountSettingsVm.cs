using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class MiscAccountSettingsVm
{
    [Required] [MaxLength(PropertyLength.Name)] public string AccountName { get; set; } = null!;
    [MaxLength(PropertyLength.Delimiter)] public string? CountyIdDelimiter { get; set; } = null!;
    [Required] public IEnumerable<string> StateIds { get; set; } = new List<string>();
}