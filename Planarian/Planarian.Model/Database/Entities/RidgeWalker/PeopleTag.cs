using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class PeopleTag : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string TagTypeId { get; set; } = null!;

    public TagType TagType { get; set; }

    public ICollection<CartographerNameTag> CartographerNameTags { get; set; } = new HashSet<CartographerNameTag>();
    public ICollection<CaveReportedByNameTag> ReportedByNameTags { get; set; } = new HashSet<CaveReportedByNameTag>();

    public ICollection<EntranceReportedByNameTag> EntranceReportedByNameTags { get; set; } =
        new HashSet<EntranceReportedByNameTag>();
}

public class PeopleTagConfiguration : BaseEntityTypeConfiguration<PeopleTag>
{
    public override void Configure(EntityTypeBuilder<PeopleTag> builder)
    {
        base.Configure(builder);
        builder.HasKey(e => new { e.TagTypeId });

        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.PeopleTags)
            .HasForeignKey(bc => bc.TagTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}