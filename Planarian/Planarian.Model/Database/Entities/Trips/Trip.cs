using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.Trips;

public class Trip : EntityBase, ITrip
{
    [MaxLength(PropertyLength.Id)] [Required] public string ProjectId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DateTime TripDate { get; set; }
    
    // public int TripNumber => Project.Trips.OrderByDescending(e=>e.TripDate).
    
    
    public virtual Project Project { get; set; } = null!;
    public virtual ICollection<TripObjective> TripObjectives { get; set; } = new HashSet<TripObjective>();
}

public class TripConfiguration : IEntityTypeConfiguration<Trip>{
    public void Configure(EntityTypeBuilder<Trip> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.Trips)
            .HasForeignKey(e => e.ProjectId);
    }
}