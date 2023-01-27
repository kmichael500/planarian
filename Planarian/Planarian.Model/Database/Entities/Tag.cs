using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class Tag : EntityBaseNameId
{
    public Tag(string name, string key)
    {
        Name = name;
        Key = key;
    }

    public Tag()
    {
    }

    public string Key { get; set; } = null!;
    public string? ProjectId { get; set; } = null!;
    public virtual ICollection<Trip> Trips { get; set; } = new HashSet<Trip>();
    public virtual ICollection<TripTag> TripTags { get; set; } = new HashSet<TripTag>();
    public virtual ICollection<LeadTag> LeadTags { get; set; } = new HashSet<LeadTag>();
    public virtual Project? Project { get; set; }
}

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.CustomTags)
            .HasForeignKey(e => e.ProjectId);
    }
}