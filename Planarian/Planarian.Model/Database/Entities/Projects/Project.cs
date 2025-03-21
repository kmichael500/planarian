using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.Projects;

public class Project : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    public virtual ICollection<Member> Members { get; set; } = new HashSet<Member>();
    public virtual ICollection<Trip> Trips { get; set; } = new HashSet<Trip>();
    public virtual ICollection<TagType> CustomTagTypes { get; set; } = new HashSet<TagType>();
}

public class ProjectConfiguration : BaseEntityTypeConfiguration<Project>;