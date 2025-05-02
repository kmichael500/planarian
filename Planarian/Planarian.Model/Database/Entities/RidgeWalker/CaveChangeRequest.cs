using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities.RidgeWalker;

public static class ChangeRequestStatus
{
    public const string Pending   = "Pending";
    public const string Approved  = "Approved";
    public const string Rejected  = "Rejected";
}

public static class ChangeRequestType
{
    public const string Submission = "Submission";
    public const string Import = "Import";
    public const string Merge = "Merge";
    public const string Initial = "Initial";
    public static string Rename = "Rename";
}

public class CaveChangeRequest : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }
    [MaxLength(PropertyLength.Id)] public string AccountId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? ReviewedByUserId { get; set; }

    [MaxLength(PropertyLength.LargeText)] public string? Notes { get; set; }
    [MaxLength(PropertyLength.Key)] public string Status { get; set; } = ChangeRequestStatus.Pending;
    [MaxLength(PropertyLength.Key)] public string Type { get; set; } = null!;
    public DateTime? ReviewedOn { get; set; }
    public Account Account { get; set; } = null!;
    public Cave Cave { get; set; } = null!;
    public User ReviewedByUser { get; set; } = null!;
    
    public IEnumerable<CaveChangeHistory> CaveChangeHistory { get; set; } = new HashSet<CaveChangeHistory>();
}

public class ChangeRequestConfiguration : BaseEntityTypeConfiguration<CaveChangeRequest>
{
    public override void Configure(EntityTypeBuilder<CaveChangeRequest> builder)
    {
        builder.HasOne(e => e.Account)
            .WithMany(e => e.CaveChangeRequests)
            .HasForeignKey(e => e.AccountId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.Cave)
            .WithMany(e => e.CaveChangeRequests)
            .HasForeignKey(e => e.CaveId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(e => e.ReviewedByUser)
            .WithMany(e => e.CaveChangeRequestsReviewed)
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}