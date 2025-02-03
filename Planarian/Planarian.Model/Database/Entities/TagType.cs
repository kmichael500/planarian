using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
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

    public string? ProjectId { get; set; }
    public string? AccountId { get; set; }

    [MaxLength(450)] public string Key { get; set; } = null!;
    public bool IsDefault { get; set; } = false;
    
    public virtual Project? Project { get; set; }
    public virtual Account? Account { get; set; }

    public virtual ICollection<Trip> Trips { get; set; } = new HashSet<Trip>();
    public virtual ICollection<TripTag> TripTags { get; set; } = new HashSet<TripTag>();
    public virtual ICollection<LeadTag> LeadTags { get; set; } = new HashSet<LeadTag>();
    public virtual ICollection<EntranceStatusTag> EntranceStatusTags { get; set; } = new HashSet<EntranceStatusTag>();

    public virtual ICollection<EntranceHydrologyTag> EntranceHydrologyTags { get; set; } =
        new HashSet<EntranceHydrologyTag>();


    public virtual ICollection<FieldIndicationTag> FieldIndicationTags { get; set; } =
        new HashSet<FieldIndicationTag>();

    public ICollection<Entrance> EntranceLocationQualitiesTags { get; set; } = new HashSet<Entrance>();
    public ICollection<GeologyTag> GeologyTags { get; set; } = new HashSet<GeologyTag>();
    public ICollection<File> FileTypeTags { get; set; } = new HashSet<File>();
    public ICollection<MapStatusTag> MapStatusTags { get; set; }
    public ICollection<GeologicAgeTag> GeologicAgeTags { get; set; }
    public ICollection<PhysiographicProvinceTag> PhysiographicProvinceTags { get; set; } =
        new HashSet<PhysiographicProvinceTag>();
    public ICollection<BiologyTag> BiologyTags { get; set; } = new HashSet<BiologyTag>();
    public ICollection<ArcheologyTag> ArcheologyTags { get; set; } = new HashSet<ArcheologyTag>();
    public ICollection<CaveOtherTag> CaveOtherTags { get; set; } = new HashSet<CaveOtherTag>();
    public virtual ICollection<CaveReportedByNameTag> CaveReportedByNameTags { get; set; } =
        new HashSet<CaveReportedByNameTag>();

    public virtual ICollection<EntranceReportedByNameTag> EntranceReportedByNameTags { get; set; } =
        new HashSet<EntranceReportedByNameTag>();

    public virtual ICollection<CartographerNameTag> CartographerNameTags { get; set; } =
        new HashSet<CartographerNameTag>();

    public virtual ICollection<EntranceOtherTag> EntranceOtherTags { get; set; } = new HashSet<EntranceOtherTag>();
}

public class TagTypeConfiguration : BaseEntityTypeConfiguration<TagType>
{
    public override void Configure(EntityTypeBuilder<TagType> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.CustomTagTypes)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Account>(e => e.Account)
            .WithMany(e => e.Tags)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(e => new { e.Key });
    }
}