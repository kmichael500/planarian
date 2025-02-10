using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class CavePermissionPermission : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string CavePermissionId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string PermissionId { get; set; } = null!;

    public virtual CavePermission CavePermission { get; set; } = null!;
    public virtual Permission Permission { get; set; } = null!;
}
public class CavePermissionPermissionConfiguration : IEntityTypeConfiguration<CavePermissionPermission>
{
    public void Configure(EntityTypeBuilder<CavePermissionPermission> builder)
    {
        builder.HasOne(e => e.CavePermission)
            .WithMany(e => e.CavePermissionPermissions)
            .HasForeignKey(e => e.CavePermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Permission)
            .WithMany(e => e.CavePermissionPermissions)
            .HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}