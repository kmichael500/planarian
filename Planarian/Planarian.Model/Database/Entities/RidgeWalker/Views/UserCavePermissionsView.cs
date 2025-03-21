using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker.Views;

public class UserCavePermissionsView : ViewBase
{
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string UserId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string PermissionId { get; set; } = null!;
    
    
    [MaxLength(PropertyLength.Key)] public string PermissionKey { get; set; } = null!;
    
    public virtual Cave Cave { get; set; } = null!;
    public virtual Account Account { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
    public virtual County County { get; set; } = null!;
}

public class UserCavePermissionsViewConfiguration : IEntityTypeConfiguration<UserCavePermissionsView>
{
    public void Configure(EntityTypeBuilder<UserCavePermissionsView> builder)
    {
        builder.HasNoKey();
        builder.ToView("UserCavePermissions");
        builder.HasOne(e => e.Cave)
            .WithMany()
            .HasForeignKey(e => e.CaveId);
        
        builder.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountId);
        
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId);

        builder.HasOne(e => e.Permission)
            .WithMany()
            .HasForeignKey(e => e.PermissionId);
        
        builder.HasOne(e => e.County)
            .WithMany()
            .HasForeignKey(e=>e.CountyId);
    }
}