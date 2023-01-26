using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.TripObjectives;

public class TripObjective : EntityBase, ITripObjective
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.MediumText)]
    public string Description { get; set; } = null!;

    public string? TripReport { get; set; }
    
    public virtual Project Project { get; set; } = null!;
    public virtual ICollection<TripObjectiveTag> TripObjectiveTags { get; set; } = new HashSet<TripObjectiveTag>();

    public virtual ICollection<TripObjectiveMember> TripObjectiveMembers { get; set; } =
        new HashSet<TripObjectiveMember>();

    public virtual ICollection<TripPhoto> Photos { get; set; } = new HashSet<TripPhoto>();
    public virtual ICollection<Lead> Leads { get; set; } = new HashSet<Lead>();
}

public class TripObjectiveConfiguration : IEntityTypeConfiguration<TripObjective>
{
    public void Configure(EntityTypeBuilder<TripObjective> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.Trips)
            .HasForeignKey(e => e.ProjectId);
    }
}