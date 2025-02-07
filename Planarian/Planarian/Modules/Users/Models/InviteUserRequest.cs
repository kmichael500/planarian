using System.ComponentModel.DataAnnotations;

namespace Planarian.Modules.Users.Models;

public class InviteUserRequest
{
    [Required] public string EmailAddress { get; set; } = null!;
    [Required] public string FirstName { get; set; } = null!;
    [Required] public string LastName { get; set; } = null!;
}