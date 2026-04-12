using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class ThrottleEventLog : EntityBase
{
    [Required]
    [MaxLength(PropertyLength.Key)]
    public ThrottleProfile OperationName { get; set; }

    [Required]
    [MaxLength(PropertyLength.Key)]
    public RequestThrottleKeyType LimiterKeyType { get; set; }

    [Required]
    [MaxLength(PropertyLength.MediumText)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(PropertyLength.Id)]
    public string? UserId { get; set; }

    [MaxLength(PropertyLength.Id)]
    public string? AccountId { get; set; }

    [MaxLength(PropertyLength.MediumText)]
    public string? IpAddress { get; set; }

    [MaxLength(PropertyLength.MediumText)]
    public string? NormalizedIdentifier { get; set; }

    public int Limit { get; set; }
    public int WindowSeconds { get; set; }
    public int RetryAfterSeconds { get; set; }

    [Required]
    public DateTime OccurredOn { get; set; }
}

public class ThrottleEventLogConfiguration : BaseEntityTypeConfiguration<ThrottleEventLog>
{
    public override void Configure(EntityTypeBuilder<ThrottleEventLog> builder)
    {
        base.Configure(builder);
        ConfigureEnum(builder, e => e.OperationName);
        ConfigureEnum(builder, e => e.LimiterKeyType);
        builder.HasIndex(e => e.OccurredOn);
    }
}
