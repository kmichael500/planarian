using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class TagTypeEditVm
{
    public string TagTypeId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;
}