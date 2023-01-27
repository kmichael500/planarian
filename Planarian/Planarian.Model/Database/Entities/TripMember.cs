using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class TripMember : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string TripId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    public virtual Trip Trip { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class TripMemberConfiguration : IEntityTypeConfiguration<TripMember>
{
    public void Configure(EntityTypeBuilder<TripMember> builder)
    {
        builder.HasOne(e => e.Trip)
            .WithMany(e => e.TripMembers)
            .HasForeignKey(e => e.TripId);

        builder.HasOne(e => e.User)
            .WithMany(e => e.TripMembers)
            .HasForeignKey(e => e.UserId);
    }
}