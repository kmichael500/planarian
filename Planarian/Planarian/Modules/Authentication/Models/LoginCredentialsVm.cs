using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Authentication.Models;

public class LoginCredentialsVm
{
    [Required]
    [MaxLength(PropertyLength.EmailAddress)]
    public string EmailAddress { get; set; } = null!;

    [Required]
    public string Password { get; set; } = null!;
}
