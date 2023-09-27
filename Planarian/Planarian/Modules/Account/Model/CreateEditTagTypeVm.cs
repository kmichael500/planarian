using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class CreateEditTagTypeVm
{
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(PropertyLength.Key)]
    public string Key { get; set; } = string.Empty;
}