using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class TripPhoto : EntityBase
{
    public TripPhoto()
    {
    }

    public TripPhoto(string userId, string tripObjectiveId, string name, string description, string fileType)
    {
        UserId = userId;
        TripObjectiveId = tripObjectiveId;
        Name = name;
        Description = description;
        FileType = fileType;
    }

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string TripObjectiveId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [MaxLength(PropertyLength.MediumText)] public string? Description { get; set; }

    [Required]
    [MaxLength(PropertyLength.FileType)]
    public string FileType { get; set; } = null!;

    [MaxLength(PropertyLength.BlobKey)] public string? BlobKey { get; set; } = null!;

    public virtual TripObjective TripObjective { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class PhotoConfiguration : IEntityTypeConfiguration<TripPhoto>
{
    public void Configure(EntityTypeBuilder<TripPhoto> builder)
    {
        builder.HasOne(e => e.TripObjective)
            .WithMany(e => e.Photos)
            .HasForeignKey(e => e.TripObjectiveId);

        builder.HasOne(e => e.User)
            .WithMany(e => e.TripPhotos)
            .HasForeignKey(e => e.UserId);
    }
}