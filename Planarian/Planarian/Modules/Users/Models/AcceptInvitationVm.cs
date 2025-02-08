namespace Planarian.Modules.Users.Models;

public class AcceptInvitationVm
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public IEnumerable<string> Regions { get; set; }
    public string AccountName { get; set; }
}