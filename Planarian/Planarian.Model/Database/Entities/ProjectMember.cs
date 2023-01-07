using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class ProjectMember : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string ProjectId { get; set; } = null!;
    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    public virtual Project Project { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasOne(e => e.Project)
            .WithMany(e => e.ProjectMembers)
            .HasForeignKey(e => e.ProjectId);
        
        builder.HasOne(e => e.User)
            .WithMany(e => e.ProjectMembers)
            .HasForeignKey(e => e.UserId);
    }
}