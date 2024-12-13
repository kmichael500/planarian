using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class Photo : EntityBase
{
    public Photo()
    {
    }

    public Photo(string tripId, string name, string description, string fileType)
    {
        TripId = tripId;
        Name = name;
        Description = description;
        FileType = fileType;
    }

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string TripId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Name)]
    public string Name { get; set; } = null!;

    [MaxLength(PropertyLength.MediumText)] public string? Description { get; set; }

    [Required]
    [MaxLength(PropertyLength.FileType)]
    public string FileType { get; set; } = null!;

    [MaxLength(PropertyLength.BlobKey)] public string? BlobKey { get; set; } = null!;

    public virtual Trip? Trip { get; set; } = null!;
}

public class PhotoConfiguration : BaseEntityTypeConfiguration<Photo>
{
    public override void Configure(EntityTypeBuilder<Photo> builder)
    {
        var photo = new Photo();
        builder.HasOne(e => e.Trip)
            .WithMany(e => e.Photos)
            .HasForeignKey(e => e.TripId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}