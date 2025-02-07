using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class AccountUser : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string UserId { get; set; } = null!;

    public DateTime? InvitationAcceptedOn { get; set; }
    public DateTime? InvitationSentOn { get; set; }

    [MaxLength(PropertyLength.EmailConfirmationCode)]
    public string? InvitationCode { get; set; }

    public Account? Account { get; set; }
    public User? User { get; set; }
}

public class AccountUserConfiguration : IEntityTypeConfiguration<AccountUser>
{
    public void Configure(EntityTypeBuilder<AccountUser> builder)
    {
        builder.HasKey(u => new { u.AccountId, u.UserId });

        builder
            .HasOne(u => u.Account)
            .WithMany(a => a.AccountUsers)
            .HasForeignKey(au => au.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder
            .HasOne(au => au.User)
            .WithMany(u => u.AccountUsers)
            .HasForeignKey(au => au.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}