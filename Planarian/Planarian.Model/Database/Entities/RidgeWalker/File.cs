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
    // Every persisted file is account-owned. Temporary import files have no Cave,
    // but are still owned by the uploading account.
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;

    [MaxLength(PropertyLength.Key)] public string? BlobKey { get; set; }
    [MaxLength(PropertyLength.Key)] public string? BlobContainer { get; set; }
    [MaxLength(PropertyLength.FileName)] public string Name { get; set; } = null!;
    [MaxLength(PropertyLength.FileName)] public string Extension { get; set; } = string.Empty;
    public DateTime? ExpiresOn { get; set; } = null!;
    public TagType FileTypeTag { get; set; } = null!;

    public virtual Cave? Cave { get; set; } = null!;
    public virtual Account? Account { get; set; } = null!;
}

public class FileConfiguration : BaseEntityTypeConfiguration<File>
{
    // PostgreSQL's default btrim/[:space:] behavior does not cover every Unicode character
    // that .NET string.IsNullOrWhiteSpace recognizes, so keep the persisted invariant explicit.
    private const string DotNetWhitespaceCharactersSql =
        "' ' || chr(9) || chr(10) || chr(11) || chr(12) || chr(13) || chr(133) || chr(160) || chr(5760) || " +
        "chr(8192) || chr(8193) || chr(8194) || chr(8195) || chr(8196) || chr(8197) || chr(8198) || " +
        "chr(8199) || chr(8200) || chr(8201) || chr(8202) || chr(8232) || chr(8233) || chr(8239) || " +
        "chr(8287) || chr(12288)";

    public override void Configure(EntityTypeBuilder<File> builder)
    {
        builder.ToTable("Files", table =>
        {
            table.HasCheckConstraint("CK_Files_ValidNameAndExtension",
                $"length(\"Name\") + length(\"Extension\") <= {PropertyLength.FileName} AND " +
                $"btrim(\"Name\", {DotNetWhitespaceCharactersSql}) <> '' AND \"Name\" NOT IN ('.', '..') AND " +
                "\"Name\" NOT LIKE '%/%' AND \"Name\" NOT LIKE '%\\\\%' AND \"Name\" !~ '[[:cntrl:]]' AND " +
                "(\"Extension\" = '' OR (length(\"Extension\") > 1 AND left(\"Extension\", 1) = '.' AND " +
                "\"Extension\" NOT LIKE '%/%' AND \"Extension\" NOT LIKE '%\\\\%' AND \"Extension\" !~ '[[:cntrl:]]'))");
        });

        // This alternate key is the tenant-qualified principal key used by staged
        // change-request files.
        builder.HasAlternateKey(e => new { e.AccountId, e.Id });

        builder.HasOne(e => e.Account)
            .WithMany()
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

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
