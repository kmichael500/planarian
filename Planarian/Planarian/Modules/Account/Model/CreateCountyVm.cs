using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class CreateCountyVm
{
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(PropertyLength.SmallText)]
    public string CountyDisplayId { get; set; } = string.Empty;
}