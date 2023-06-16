using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared.Base;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

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
    public string? AccountId { get; set; } = null!;

    public string Key { get; set; } = null!;

    public virtual ICollection<Trip> Trips { get; set; } = new HashSet<Trip>();
    public virtual ICollection<TripTag> TripTags { get; set; } = new HashSet<TripTag>();
    public virtual ICollection<LeadTag> LeadTags { get; set; } = new HashSet<LeadTag>();
    public virtual ICollection<EntranceStatusTag> EntranceStatusTags { get; set; } = new HashSet<EntranceStatusTag>();

    public virtual ICollection<EntranceHydrologyTag> EntranceHydrologyTags { get; set; } =
        new HashSet<EntranceHydrologyTag>();
    
    public virtual ICollection<EntranceHydrologyFrequencyTag> EntranceHydrologyFrequencyTags { get; set; } =
        new HashSet<EntranceHydrologyFrequencyTag>();
    public virtual ICollection<FieldIndicationTag> FieldIndicationTags { get; set; } = new HashSet<FieldIndicationTag>();
    public ICollection<Entrance> EntranceLocationQualitiesTags { get; set; } = new HashSet<Entrance>();
    public ICollection<GeologyTag> GeologyTags { get; set; } = new HashSet<GeologyTag>();
    public ICollection<File> FileTypeTags { get; set; } = new HashSet<File>();
    public virtual Project? Project { get; set; }
}

public class TagTypeConfiguration : BaseEntityTypeConfiguration<TagType>
{
    public override void Configure(EntityTypeBuilder<TagType> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.CustomTagTypes)
            .HasForeignKey(e => e.ProjectId);
    }
}