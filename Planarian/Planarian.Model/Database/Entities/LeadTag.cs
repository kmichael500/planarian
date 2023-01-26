using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class LeadTag : EntityBase
{
    public string TagId { get; set; }
    public string LeadId { get; set; }
    
    public Tag Tag { get; set; }
    public Lead Lead { get; set; }

}

public class LeadTagConfiguration : IEntityTypeConfiguration<LeadTag>
{
    public void Configure(EntityTypeBuilder<LeadTag> builder)
    {
        builder.HasKey(e => new { e.TagId, TripObjectiveId = e.LeadId });
        builder
            .HasOne(e => e.Tag)
            .WithMany(e => e.LeadTags)
            .HasForeignKey(bc => bc.TagId);


        builder.HasOne(e => e.Lead)
            .WithMany(e => e.LeadTags)
            .HasForeignKey(e => e.LeadId);
    }
}