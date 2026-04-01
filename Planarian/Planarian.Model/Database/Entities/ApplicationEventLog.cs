using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class ApplicationEventLog : EntityBase
{
    public ApplicationEventCategory Category { get; set; }
    public ApplicationEventType EventType { get; set; }

    [Required]
    public DateTime OccurredOn { get; set; }

    public int AttemptCount { get; set; } = 1;

    public DateTime? WindowStartedOn { get; set; }

    public DateTime? WindowEndsOn { get; set; }

    [MaxLength(PropertyLength.Id)]
    public string? UserId { get; set; }

    [MaxLength(PropertyLength.Id)]
    public string? AccountId { get; set; }

    [MaxLength(PropertyLength.MediumText)]
    public string? IpAddress { get; set; }

    [MaxLength(PropertyLength.MediumText)]
    public string? NormalizedIdentifier { get; set; }

    [MaxLength(PropertyLength.MediumText)]
    public string? AggregationKey { get; set; }

    [Required]
    [MaxLength(PropertyLength.MediumText)]
    public string Path { get; set; } = string.Empty;

    [MaxLength(PropertyLength.Max)]
    public string? Message { get; set; }

    [MaxLength(PropertyLength.Max)]
    public string? DataJson { get; set; }
}

public class ApplicationEventLogConfiguration : BaseEntityTypeConfiguration<ApplicationEventLog>
{
    public override void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<ApplicationEventLog> builder)
    {
        base.Configure(builder);
        ConfigureEnum(builder, e => e.Category);
        ConfigureEnum(builder, e => e.EventType);
        builder.HasIndex(e => e.AggregationKey).IsUnique();
    }
}

public enum ApplicationEventCategory
{
    Security
}

public enum ApplicationEventType
{
    LoginFailure,
    LoginThrottled,
    PasswordResetRequested,
    PasswordResetThrottled,
    EndpointRateLimitThrottled
}

public enum LoginFailureReason
{
    EmailDoesNotExist,
    InvalidPassword,
    UnconfirmedEmail
}
