using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class CavePermission : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string AccountId { get; set; } = null!;
    
    [Required] [MaxLength(PropertyLength.Id)] public string PermissionId { get; set; } = null!;

    // Either a CountyId OR a CaveId (or neither) may be provided.
    [MaxLength(PropertyLength.Id)] public string? CountyId { get; set; } // Applies to all caves in the county.

    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }

    public virtual User? User { get; set; }
    public virtual Account? Account { get; set; }
    public virtual County? County { get; set; }
    public virtual Cave? Cave { get; set; }
    public virtual Permission? Permission { get; set; }

}


public class CavePermissionConfiguration : BaseEntityTypeConfiguration<CavePermission>
{
    public override void Configure(EntityTypeBuilder<CavePermission> builder)
    {
        base.Configure(builder);
        
        builder.HasOne(e => e.User)
            .WithMany(e => e.CavePermissions)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Account)
            .WithMany(e => e.CavePermissions)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.County)
            .WithMany(e => e.CavePermissions)
            .HasForeignKey(e => e.CountyId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Cave)
            .WithMany(e => e.CavePermissions)
            .HasForeignKey(e => e.CaveId)
            .OnDelete(DeleteBehavior.NoAction);
        
        builder.HasOne(e => e.Permission)
            .WithMany(e => e.CavePermission)
            .HasForeignKey(e => e.PermissionId)
            .OnDelete(DeleteBehavior.NoAction);

        // Enforce that at most one of CountyId and CaveId is provided.
        builder.ToTable(nameof(CavePermission), tb =>
        {
            tb.HasCheckConstraint("CK_CavePermission_CountyOrCave", "\"CountyId\" IS NULL OR \"CaveId\" IS NULL");
        });

        // Unique index for records with a CountyId (and CaveId is null).
        builder.HasIndex(e => new { e.UserId, e.AccountId, e.CountyId, e.PermissionId })
            .IsUnique()
            .HasFilter("\"CountyId\" IS NOT NULL");

        // Unique index for records with a CaveId (and CountyId is null).
        builder.HasIndex(e => new { e.UserId, e.AccountId, e.CaveId, e.PermissionId })
            .IsUnique()
            .HasFilter("\"CaveId\" IS NOT NULL");

        // Unique index for records with neither location specified.
        builder.HasIndex(e => new { e.UserId, e.AccountId, e.PermissionId })
            .IsUnique()
            .HasFilter("\"CountyId\" IS NULL AND \"CaveId\" IS NULL");
    }
}