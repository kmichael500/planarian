using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.Trips;

public class Trip : EntityBase, ITrip
{
    public virtual Project Project { get; set; } = null!;
    public virtual ICollection<TripTag> TripTags { get; set; } = new HashSet<TripTag>();

    public virtual ICollection<Member> Members { get; set; } =
        new HashSet<Member>();

    public virtual ICollection<Photo> Photos { get; set; } = new HashSet<Photo>();
    public virtual ICollection<Lead> Leads { get; set; } = new HashSet<Lead>();

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [MaxLength(PropertyLength.MediumText)] public string? Description { get; set; } = null!;

    public string? TripReport { get; set; }
}

public class TripConfiguration : BaseEntityTypeConfiguration<Trip>
{
    public override void Configure(EntityTypeBuilder<Trip> builder)
    {
        base.Configure(builder);
        builder.HasOne(e => e.Project)
            .WithMany(e => e.Trips)
            .HasForeignKey(e => e.ProjectId);
    }
}