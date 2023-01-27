using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.Trips;

public class Trip : EntityBase, ITrip
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [MaxLength(PropertyLength.MediumText)]
    public string? Description { get; set; } = null!;

    public string? TripReport { get; set; }
    
    public virtual Project Project { get; set; } = null!;
    public virtual ICollection<TripTag> TripTags { get; set; } = new HashSet<TripTag>();

    public virtual ICollection<TripMember> TripMembers { get; set; } =
        new HashSet<TripMember>();

    public virtual ICollection<Photo> Photos { get; set; } = new HashSet<Photo>();
    public virtual ICollection<Lead> Leads { get; set; } = new HashSet<Lead>();
}

public class TripConfiguration : IEntityTypeConfiguration<Trip>
{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.Trips)
            .HasForeignKey(e => e.ProjectId);
    }
}