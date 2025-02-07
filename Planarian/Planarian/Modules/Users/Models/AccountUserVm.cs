namespace Planarian.Modules.Users.Models;

public class AccountUserVm
{
    public string UserId { get; set; } = null!;
    public string EmailAddress { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public bool IsInvitationAccepted => InvitationAcceptedOn.HasValue;
    public DateTime? InvitationAcceptedOn { get; set; }

    public AccountUserVm(string userId, string emailAddress, string fullName,
        DateTime? invitationAcceptedOn)
    {
        UserId = userId;
        EmailAddress = emailAddress;
        FullName = fullName;
        InvitationAcceptedOn = invitationAcceptedOn;
    }
    public AccountUserVm(){}
}