using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class Member : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string? ProjectId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? TripId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    public virtual Project? Project { get; set; } = null!;
    public virtual Trip? Trip { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class ProjectMemberConfiguration : BaseEntityTypeConfiguration<Member>
{
    public override void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.Members)
            .HasForeignKey(e => e.ProjectId);

        builder.HasOne(e => e.Trip)
            .WithMany(e => e.Members)
            .HasForeignKey(e => e.TripId);

        builder.HasOne(e => e.User)
            .WithMany(e => e.Members)
            .HasForeignKey(e => e.UserId);
    }
}