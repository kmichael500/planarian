using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class UserPermission : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string PermissionId { get; set; } = null!;
    public string? AccountId { get; set; }
    
    public virtual Account? Account { get; set; }
    public virtual User? User { get; set; }
    public virtual Permission? Permission { get; set; }
}

public class UserPermissionConfiguration : BaseEntityTypeConfiguration<UserPermission>
{
    public override void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        base.Configure(builder);

        builder.HasOne(e => e.User)
            .WithMany(e => e.UserPermissions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Permission)
            .WithMany(e => e.UserPermissions)
            .HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Account)
            .WithMany(e => e.UserPermissions)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(e => new { e.UserId, e.PermissionId, e.AccountId })
            .IsUnique();
    }
}