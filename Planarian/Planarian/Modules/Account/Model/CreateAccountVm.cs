using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class CreateAccountVm
{
    [MaxLength(PropertyLength.Name)] public string Name { get; set; }
}