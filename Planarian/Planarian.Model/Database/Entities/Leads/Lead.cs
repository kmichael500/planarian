using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.Leads;

public class Lead : EntityBase
{
    public Lead()
    {
    }

    public Lead(CreateLeadVm lead, string tripId, string userId) : this(lead.Description,
        lead.Classification, lead.ClosestStation, userId, tripId)
    {
    }

    public Lead(string description,
        string classification, string closestStation,
        string userId, string tripId)
    {
        UserId = userId;
        TripId = tripId;
        Description = description.Trim();
        ClosestStation = closestStation.Trim();
        Classification = classification.Trim();
    }

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string TripId { get; set; } = null!;


    [MaxLength(PropertyLength.MediumText)] public string Description { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.StationName)]
    public string ClosestStation { get; set; } = null!;

    public string Classification { get; set; }
    public bool IsAlive { get; set; } = true;

    public virtual Trip Trip { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual ICollection<LeadTag> LeadTags { get; set; } = new HashSet<LeadTag>();
}

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.HasOne(e => e.Trip)
            .WithMany(e => e.Leads)
            .HasForeignKey(e => e.TripId);

        builder.HasOne(e => e.User)
            .WithMany(e => e.Leads)
            .HasForeignKey(e => e.UserId);
    }
}