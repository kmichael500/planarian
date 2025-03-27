using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Planarian.Model.Database.Entities.RidgeWalker.Views;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Cave : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string StateId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; }
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; } = null!;
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    [MaxLength(PropertyLength.Max)] public string AlternateNames { get; private set; } = "[]";
    public int CountyNumber { get; set; } // max of highest cave number in county + 1
    public double? LengthFeet { get; set; }
    public double? DepthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int? NumberOfPits { get; set; }

    public string? Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    public bool IsArchived { get; set; } = false;


    public virtual Account Account { get; set; } = null!;
    public virtual User? ReportedByUser { get; set; }
    public virtual County County { get; set; } = null!;
    public virtual State State { get; set; } = null!;
    public virtual ICollection<File> Files { get; set; } = new HashSet<File>();
    public virtual ICollection<Entrance> Entrances { get; set; } = new HashSet<Entrance>();
    public virtual ICollection<GeologyTag> GeologyTags { get; set; } = new HashSet<GeologyTag>();
    public virtual ICollection<MapStatusTag> MapStatusTags { get; set; } = new HashSet<MapStatusTag>();

    public virtual ICollection<GeologicAgeTag> GeologicAgeTags { get; set; } =
        new HashSet<GeologicAgeTag>();

    public virtual ICollection<PhysiographicProvinceTag> PhysiographicProvinceTags { get; set; } =
        new HashSet<PhysiographicProvinceTag>();

    public virtual ICollection<BiologyTag> BiologyTags { get; set; } =
        new HashSet<BiologyTag>();

    public virtual ICollection<ArcheologyTag> ArcheologyTags { get; set; } =
        new HashSet<ArcheologyTag>();

    public virtual ICollection<CartographerNameTag> CartographerNameTags { get; set; } =
        new HashSet<CartographerNameTag>();

    public virtual ICollection<CaveReportedByNameTag> CaveReportedByNameTags { get; set; } =
        new HashSet<CaveReportedByNameTag>();

    public virtual ICollection<CaveOtherTag> CaveOtherTags { get; set; } =
        new HashSet<CaveOtherTag>();

    [NotMapped]
    public IEnumerable<string> AlternateNamesList =>
        JsonSerializer.Deserialize<List<string>>(AlternateNames) ?? new List<string>();

    public ICollection<CavePermission> CavePermissions { get; set; } = new HashSet<CavePermission>();


    public void SetAlternateNamesList(IEnumerable<string> alternateNames) =>
        AlternateNames = JsonSerializer.Serialize(alternateNames);
}

public class CaveConfiguration : BaseEntityTypeConfiguration<Cave>
{
    public override void Configure(EntityTypeBuilder<Cave> builder)
    {
        builder.HasOne(e => e.County)
            .WithMany(e => e.Caves)
            .HasForeignKey(e => e.CountyId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.ReportedByUser)
            .WithMany(e => e.CavesReported)
            .HasForeignKey(e => e.ReportedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Account)
            .WithMany(e => e.Caves)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.State)
            .WithMany(e => e.Caves)
            .HasForeignKey(e => e.StateId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(e => new { e.CountyNumber, e.CountyId }).IsUnique();
        builder.HasIndex(e => e.LengthFeet);
        builder.HasIndex(e => e.DepthFeet);
        builder.HasIndex(e => e.CountyNumber);
        builder.HasIndex(e => e.Name);
    }
}