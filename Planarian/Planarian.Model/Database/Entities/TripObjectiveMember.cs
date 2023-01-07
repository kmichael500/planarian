using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class TripObjectiveMember : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string TripObjectiveId { get; set; } = null!;
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    public virtual TripObjective TripObjective { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class TripObjectiveMemberConfiguration : IEntityTypeConfiguration<TripObjectiveMember>
{
    public void Configure(EntityTypeBuilder<TripObjectiveMember> builder)
    {
        builder.HasOne(e => e.TripObjective)
            .WithMany(e => e.TripObjectiveMembers)
            .HasForeignKey(e => e.TripObjectiveId);
        
        builder.HasOne(e => e.User)
            .WithMany(e => e.TripObjectiveMembers)
            .HasForeignKey(e => e.UserId);
    }
}