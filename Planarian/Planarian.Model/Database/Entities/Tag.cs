using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class Tag : EntityBaseNameId
{
    public string Key { get; set; } = null!;
    public string? ProjectId { get; set; } = null!;
    public virtual ICollection<TripObjective> TripObjectives { get; set; } = new HashSet<TripObjective>();
    public virtual ICollection<TripObjectiveTag> TripObjectiveTags { get; set; } = new HashSet<TripObjectiveTag>();
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