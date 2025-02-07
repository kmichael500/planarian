using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class User : EntityBase
{
    public User(string firstName, string lastName, string email)
    {
        if (!email.IsValidEmail()) throw new ArgumentException("Invalid email address", nameof(email));
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        EmailAddress = email.Trim();
    }

    public User(string firstName, string lastName, string emailAddress, string phoneNumber) : this(firstName, lastName,
        emailAddress)
    {
        phoneNumber = phoneNumber.ExtractPhoneNumber();
        if (!phoneNumber.IsValidPhoneNumber()) throw new ArgumentException("Invalid phone number", nameof(phoneNumber));
        PhoneNumber = phoneNumber;
    }

    public User()
    {
    }

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string FirstName { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string LastName { get; set; } = null!;

    [NotMapped] public string FullName => $"{FirstName} {LastName}";

    [MaxLength(PropertyLength.EmailAddress)]
    public string EmailAddress { get; set; } = null!;

    [MaxLength(PropertyLength.PhoneNumber)]
    public string? PhoneNumber { get; set; }

    [MaxLength(PropertyLength.PasswordHash)]
    public string? HashedPassword { get; set; }

    [MaxLength(PropertyLength.PasswordResetCode)]
    public string? PasswordResetCode { get; set; }

    [MaxLength(PropertyLength.EmailConfirmationCode)]
    public string? EmailConfirmationCode { get; set; }

    public DateTime? EmailConfirmedOn { get; set; }

    public bool IsTemporary { get; set; } = false; // Used to invite users to Planarian. The entire user record will be deleted once the user accepts the invitation.

    public DateTime? PasswordResetCodeExpiration { get; set; }
    [MaxLength(PropertyLength.BlobKey)] public string? ProfilePhotoBlobKey { get; set; }

    public virtual ICollection<Member> Members { get; set; } = new HashSet<Member>();
    public ICollection<Cave> CavesReported { get; set; } = new HashSet<Cave>();
    public ICollection<Entrance> EntrancesReported { get; set; } = new HashSet<Entrance>();
    public ICollection<AccountUser> AccountUsers { get; set; } = new HashSet<AccountUser>();

    #region Helper Functions

    [NotMapped] public bool IsProjectMember => Members.Any(e => !string.IsNullOrWhiteSpace(e.ProjectId));
    [NotMapped] public bool IsTripMember => Members.Any(e => !string.IsNullOrWhiteSpace(e.TripId));

    #endregion
}

public class UserConfiguration : BaseEntityTypeConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasIndex(e => e.EmailAddress)
            .IsUnique()
            .HasFilter("\"IsTemporary\" = false");
    }
}