using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Library.Extensions.String;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class User : EntityBase
{
    public User(string firstName, string lastName, string email)
    {
        if (!email.IsValidEmail())
        {
            throw new ArgumentException("Invalid email address", nameof(email));
        }
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        EmailAddress = email.Trim();
    }
    
    public User(){}

    [Required] [MaxLength(PropertyLength.Name)] public string FirstName { get; set; } = null!;
    [Required] [MaxLength(PropertyLength.Name)] public string LastName { get; set; } = null!;
    public string FullName => $"{FirstName} {LastName}";

    [MaxLength(PropertyLength.EmailAddress)] public string EmailAddress { get; set; } = null!;
    [MaxLength(PropertyLength.PhoneNumber)] public string? PhoneNumber { get; set; } = null!;

    [MaxLength(PropertyLength.BlobKey)]public string? ProfilePhotoBlobKey { get; set; }
    
    public virtual ICollection<ProjectMember> ProjectMembers { get; set; } = new HashSet<ProjectMember>();
    public virtual ICollection<TripObjectiveMember> TripObjectiveMembers { get; set; } = new HashSet<TripObjectiveMember>();
    public virtual ICollection<TripPhoto> TripPhotos { get; set; } = new HashSet<TripPhoto>();
    public virtual ICollection<Lead> Leads { get; set; } = new HashSet<Lead>();
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {

    }
}