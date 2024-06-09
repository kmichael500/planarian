using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class LeadTag : EntityBase
{
    public string TagTypeId { get; set; }
    public string LeadId { get; set; }

    public TagType? TagType { get; set; }
    public Lead? Lead { get; set; }
}

public class LeadTagConfiguration : BaseEntityTypeConfiguration<LeadTag>
{
    public override void Configure(EntityTypeBuilder<LeadTag> builder)
    {
        base.Configure(builder);

        builder.HasKey(e => new { e.TagTypeId, TripId = e.LeadId });
        builder
            .HasOne(e => e.TagType)
            .WithMany(e => e.LeadTags)
            .HasForeignKey(bc => bc.TagTypeId);

        builder.HasOne(e => e.Lead)
            .WithMany(e => e.LeadTags)
            .HasForeignKey(e => e.LeadId);
    }
}