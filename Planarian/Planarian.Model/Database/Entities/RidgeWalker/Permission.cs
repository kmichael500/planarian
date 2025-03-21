using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Permission : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.MediumText)]
    public string Description { get; set; } = null!;

    [Required] public bool IsHidden { get; set; } = false;

    [Required]
    [MaxLength(PropertyLength.Key)]
    public string Key { get; set; } = null!;
    public int SortOrder { get; set; }
    [Required] [MaxLength(PropertyLength.Key)] public string PermissionType { get; set; } = null!;

    public ICollection<CavePermission> CavePermission { get; set; } = new HashSet<CavePermission>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new HashSet<UserPermission>();
}

public class PermissionConfiguration : BaseEntityTypeConfiguration<Permission>
{
    public override void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasIndex(p => new { p.PermissionType, p.Key });
        builder.HasIndex(p => p.Key);
        base.Configure(builder);
    }
}