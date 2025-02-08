namespace Planarian.Modules.Authentication.Models;

public class RegisterUserVm
{
    public RegisterUserVm(string firstName, string lastName, string emailAddress, string password)
    {
        FirstName = firstName;
        LastName = lastName;
        EmailAddress = emailAddress;
        Password = password;
    }

    public RegisterUserVm()
    {
    }

    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string EmailAddress { get; set; }
    public string Password { get; set; }
    public string PhoneNumber { get; set; }
    
    public string? InvitationCode { get; set; }
}