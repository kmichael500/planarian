using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Controller;

public class CreateAccountVm
{
    [MaxLength(PropertyLength.Name)] public string Name { get; set; }
}