using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.TripObjectives;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Modules.Leads.Models;
using Planarian.Modules.TripObjectives.Controllers;

namespace Planarian.Model.Database.Entities;

public class Lead : EntityBase
{
    public Lead()
    {
    }

    public Lead(CreateLeadVm lead, string tripObjectiveId, string userId) : this(lead.Description,
        lead.Classification, lead.ClosestStation, userId, tripObjectiveId)
    {

    }

    public Lead(string description,
        string classification, string closestStation,
        string userId, string tripObjectiveId)
    {
        UserId = userId;
        TripObjectiveId = tripObjectiveId;
        Description = description.Trim();
        ClosestStation = closestStation.Trim();
        Classification = classification.Trim();
    }

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.Id)]
    public string TripObjectiveId { get; set; } = null!;


    [MaxLength(PropertyLength.MediumText)] public string Description { get; set; } = null!;

    [Required]
    [MaxLength(PropertyLength.StationName)]
    public string ClosestStation { get; set; } = null!;

    public string Classification { get; set; }


    public virtual TripObjective TripObjective { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}

public class LeadConfiguration : IEntityTypeConfiguration<Lead>
{
    public void Configure(EntityTypeBuilder<Lead> builder)
    {
        builder.HasOne(e => e.TripObjective)
            .WithMany(e => e.Leads)
            .HasForeignKey(e => e.TripObjectiveId);

        builder.HasOne(e => e.User)
            .WithMany(e => e.Leads)
            .HasForeignKey(e => e.UserId);

    }
}