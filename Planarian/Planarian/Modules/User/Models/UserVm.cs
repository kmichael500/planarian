using System.ComponentModel.DataAnnotations;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities;

namespace Planarian.Modules.Users.Models;

public class UserVm
{
    private string? _phoneNumber;

    public UserVm(string firstName, string lastName, string emailAddress, string? phoneNumber = null) : this()
    {
        if (!emailAddress.IsValidEmail())
            throw new ArgumentOutOfRangeException(nameof(emailAddress), "Email address is not valid");

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            phoneNumber = phoneNumber.ExtractPhoneNumber();
            if (!phoneNumber.IsValidPhoneNumber())
                throw new ArgumentOutOfRangeException(nameof(phoneNumber), "Phone number is not valid");
            PhoneNumber = phoneNumber.Trim();
        }

        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        EmailAddress = emailAddress.Trim();
    }

    public UserVm(User firstName) : this(firstName.FirstName, firstName.LastName, firstName.EmailAddress,
        firstName.PhoneNumber)
    {
    }

    public UserVm()
    {
    }

    [Required] public string EmailAddress { get; set; }
    [Required] public string FirstName { get; set; }
    [Required] public string LastName { get; set; }

    public string? PhoneNumber
    {
        get => _phoneNumber?.ExtractPhoneNumber();
        set => _phoneNumber = value;
    }
}