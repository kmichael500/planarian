using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities;

namespace Planarian.Model.Shared.Base;

public abstract class EntityBaseNameId : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;
}

public abstract class EntityBase
{
    protected EntityBase()
    {
        // Temporary Id for EF Core change tracking
        Id = Guid.NewGuid().ToString();
    }

    [Key]
    [MaxLength(PropertyLength.Id)]
    [Required]
    public string Id { get; set; } = null!;

    [MaxLength(PropertyLength.Id)] public string? CreatedByUserId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? ModifiedByUserId { get; set; } = null!;

    [Required] public DateTime CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; } = null!;

    [NotMapped] public virtual User? CreatedByUser { get; set; } = null!;
    [NotMapped] public virtual User? ModifiedByUser { get; set; } = null!;
}

public abstract class BaseEntityTypeConfiguration<T> : IEntityTypeConfiguration<T> where T : EntityBase
{
    public virtual void Configure(EntityTypeBuilder<T> builder)
    {
        builder.HasOne(e => e.CreatedByUser)
            .WithMany()
            .HasForeignKey(b => b.CreatedByUserId);

        builder.HasOne(e => e.ModifiedByUser)
            .WithMany()
            .HasForeignKey(e => e.ModifiedByUserId);
    }
}