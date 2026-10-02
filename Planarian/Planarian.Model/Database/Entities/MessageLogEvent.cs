using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class MessageLogEvent : EntityBase
{
    [MaxLength(PropertyLength.Id)]
    public string MessageLogId { get; set; } = null!;

    public MessageProvider Provider { get; set; }

    [MaxLength(PropertyLength.MediumText)]
    public string ProviderDomain { get; set; } = null!;

    public MessageDeliveryEventType EventType { get; set; }
    public DateTime OccurredOn { get; set; }

    public string ProviderEventType { get; set; } = null!;

    public string ProviderEventId { get; set; } = null!;

    public DateOnly ProviderEventDay { get; set; }

    [MaxLength(PropertyLength.MailgunWebhookToken)]
    public string? WebhookToken { get; set; }

    public string? Severity { get; set; }

    public string? Reason { get; set; }

    public string? DeliveryCode { get; set; }

    public string? EnhancedDeliveryCode { get; set; }

    public string? DeliveryMessage { get; set; }
    public int? AttemptNumber { get; set; }
    public bool? IsDelayedBounce { get; set; }
    public string? Bot { get; set; }

    public virtual MessageLog MessageLog { get; set; } = null!;
}

public class MessageLogEventConfiguration : BaseEntityTypeConfiguration<MessageLogEvent>
{
    public override void Configure(EntityTypeBuilder<MessageLogEvent> builder)
    {
        base.Configure(builder);
        ConfigureEnum(builder, e => e.Provider);
        ConfigureEnum(builder, e => e.EventType);

        builder.HasOne(e => e.MessageLog)
            .WithMany(e => e.Events)
            .HasForeignKey(e => e.MessageLogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.Provider, e.ProviderDomain, e.ProviderEventDay, e.ProviderEventId })
            .IsUnique();

        builder.HasIndex(e => e.WebhookToken)
            .IsUnique()
            .HasFilter("\"WebhookToken\" IS NOT NULL");
    }
}
