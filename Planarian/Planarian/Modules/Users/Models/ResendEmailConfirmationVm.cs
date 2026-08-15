using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Users.Models;

public class ResendEmailConfirmationVm
{
    [Required]
    [EmailAddress]
    [MaxLength(PropertyLength.EmailAddress)]
    public string EmailAddress { get; set; } = null!;
}
