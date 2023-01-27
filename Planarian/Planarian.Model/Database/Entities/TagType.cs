using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class TagType : EntityBaseNameId
{
    public TagType(string name, string key)
    {
        Name = name;
        Key = key;
    }

    public TagType()
    {
    }
    public string? ProjectId { get; set; } = null!;

    public string Key { get; set; } = null!;
    
    public virtual ICollection<Trip> Trips { get; set; } = new HashSet<Trip>();
    public virtual ICollection<TripTag> TripTags { get; set; } = new HashSet<TripTag>();
    public virtual ICollection<LeadTag> LeadTags { get; set; } = new HashSet<LeadTag>();
    public virtual Project? Project { get; set; }
}

public class TagTypeConfiguration : IEntityTypeConfiguration<TagType>
{
    public void Configure(EntityTypeBuilder<TagType> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.CustomTagTypes)
            .HasForeignKey(e => e.ProjectId);
    }
}