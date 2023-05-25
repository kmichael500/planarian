using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public class Cave : EntityBase
{
    public string CountyId { get; set; }
    public string ReportedByUserId { get; set; }
    public string PrimaryEntranceId { get; set; }

    public string Name { get; set; }
    public int CaveNumber { get; set; } // max of highest cave number in county + 1

    public double LengthFeet { get; set; }
    public double DepthFeet { get; set; }
    public double MaxPitDepthFeet { get; set; }
    public int NumberOfPits { get; set; }

    public string Narrative { get; set; }

    public DateTime? ReportedOn { get; set; }
    public string ReportedByName { get; set; }
    public bool IsArchived { get; set; }

    public virtual User ReportedByUser { get; set; } = null!;
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
            .HasForeignKey(e => e.CountyId);
        
        builder.HasOne(e => e.ReportedByUser)
            .WithMany(e => e.CavesReported)
            .HasForeignKey(e => e.ReportedByUserId);
        
        // TODO: Is this correct way to configure a one to one?
        builder.HasOne(e => e.PrimaryEntrance)
            .WithOne()
            .HasForeignKey<Cave>(e => e.PrimaryEntranceId);
    }
}