namespace Planarian.Modules.Users.Models;

public class UserManagerGridVm
{
    public string UserId { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public DateTime? InvitationAcceptedOn { get; set; }
    public DateTime? InvitationSentOn { get; set; }
    public DateTime? LastActiveOn { get; set; }
    
    public UserManagerGridVm(string userId, string emailAddress, string fullName,
        DateTime? invitationSentOn,
        DateTime? invitationAcceptedOn, DateTime? lastActiveOn)
    {
        UserId = userId;
        EmailAddress = emailAddress;
        FullName = fullName;
        InvitationSentOn = invitationSentOn;
        InvitationAcceptedOn = invitationAcceptedOn;
        LastActiveOn  = lastActiveOn;
    }
    public UserManagerGridVm(){}
}