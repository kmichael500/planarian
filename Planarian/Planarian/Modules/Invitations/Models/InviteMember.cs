namespace Planarian.Modules.Invitations.Models;

public class InviteMember
{
    public InviteMember(string firstName, string lastName, string email)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
    }

    public InviteMember()
    {
    }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}