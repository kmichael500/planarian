using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class File : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string FileTypeTagId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? AccountId { get; set; } // can be associated with an account but not a a cave

    [MaxLength(PropertyLength.Key)] public string? BlobKey { get; set; }
    [MaxLength(PropertyLength.Key)] public string? BlobContainer { get; set; }
    [MaxLength(PropertyLength.FileName)] public string FileName { get; set; } = null!;
    [MaxLength(PropertyLength.Name)] public string? DisplayName { get; set; }
    public DateTime? ExpiresOn { get; set; } = null!;
    public TagType FileTypeTag { get; set; } = null!;

    public virtual Cave? Cave { get; set; } = null!;
    public virtual Account? Account { get; set; } = null!;
}

public class FileConfiguration : BaseEntityTypeConfiguration<File>
{
    public override void Configure(EntityTypeBuilder<File> builder)
    {
        builder
            .HasOne(e => e.Cave)
            .WithMany(e => e.Files)
            .HasForeignKey(bc => bc.CaveId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.FileTypeTag)
            .WithMany(e => e.FileTypeTags)
            .HasForeignKey(e => e.FileTypeTagId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}