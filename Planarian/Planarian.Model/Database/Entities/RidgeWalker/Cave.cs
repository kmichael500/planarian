using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Cave : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string PrimaryEntranceId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyId { get; set; } = null!;
    
    public int CaveNumber { get; set; } // max of highest cave number in county + 1
    
    [MaxLength(PropertyLength.Name)] public string Name { get; set; } = null!;
    
    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; } = 0;

    public string? Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }
    public bool IsArchived { get; set; } = false;

    public virtual Account Account { get; set; } = null!;
    public virtual User? ReportedByUser { get; set; } = null!;
    public virtual County County { get; set; } = null!;
    public virtual Entrance PrimaryEntrance { get; set; } = null!;

    public virtual ICollection<Map> Maps { get; set; } = new HashSet<Map>();
    public virtual ICollection<Entrance> Entrances { get; set; } = new HashSet<Entrance>();
    public virtual ICollection<GeologyTag> GeologyTags { get; set; } = new HashSet<GeologyTag>();
}

public class CaveConfiguration : BaseEntityTypeConfiguration<Cave>
{
    public override void Configure(EntityTypeBuilder<Cave> builder)
    {
        builder.HasOne(e => e.County)
            .WithMany(e => e.Caves)
            .HasForeignKey(e => e.CountyId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(e => e.ReportedByUser)
            .WithMany(e => e.CavesReported)
            .HasForeignKey(e => e.ReportedByUserId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(e => e.ReportedByUser)
            .WithMany(e => e.CavesReported)
            .HasForeignKey(e => e.ReportedByUserId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(e => e.PrimaryEntrance)
            .WithOne()
            .HasForeignKey<Cave>(e => e.PrimaryEntranceId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(e => e.Account)
            .WithMany(e => e.Caves)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.ClientNoAction);

    }
}