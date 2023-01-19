using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Authentication.Models;

public class UserLoginVm
{
    public UserLoginVm(string emailAddress, string password)
    {
        EmailAddress = emailAddress;
        Password = password;
    }

    public UserLoginVm()
    {
    }

    [Required]
    [MaxLength(PropertyLength.EmailAddress)]
    public string EmailAddress { get; set; } = null!;

    [Required] public string Password { get; set; } = null!;

    public bool? Remember { get; set; }
}