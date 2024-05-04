using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class TagTypeTableCountyVm : TagTypeTableVm
{
    [MaxLength(PropertyLength.SmallText)] public string CountyDisplayId { get; set; } = null!;
}