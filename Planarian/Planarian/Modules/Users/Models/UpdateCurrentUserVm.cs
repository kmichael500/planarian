using System.ComponentModel.DataAnnotations;

namespace Planarian.Modules.Users.Models;

public class UpdateCurrentUserVm : UserVm
{
    public string? CurrentPassword { get; set; }
}

public class UpdateCurrentUserPasswordVm
{
    [Required] public string CurrentPassword { get; set; } = null!;
    [Required] public string Password { get; set; } = null!;
}
