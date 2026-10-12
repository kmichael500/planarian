using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Planarian.Model.Shared;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class CaveSearchDocument
{
    [Key]
    [MaxLength(PropertyLength.Id)]
    public string CaveId { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string DisplayId { get; set; } = null!;
    public string NameSearchText { get; set; } = null!;
    public string SnippetText { get; set; } = null!;
    public NpgsqlTsVector PhraseVector { get; set; } = null!;
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}

public class CaveSearchDocumentConfiguration : IEntityTypeConfiguration<CaveSearchDocument>
{
    public void Configure(EntityTypeBuilder<CaveSearchDocument> builder)
    {
        builder.ToTable("CaveSearchDocuments");
        builder.HasKey(e => e.CaveId);

        builder.HasIndex(e => e.SearchVector)
            .HasMethod("GIN");

        builder.HasIndex(e => e.DisplayId)
            .HasMethod("GIN")
            .HasOperators("gin_trgm_ops");

        builder.HasIndex(e => e.NameSearchText)
            .HasMethod("GIN")
            .HasOperators("gin_trgm_ops");
    }
}
