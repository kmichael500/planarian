using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Database.Entities.RidgeWalker;

namespace Planarian.Model.Database.Entities;

public class MessageLog : EntityBase
{
    public MessageLog(string templateKey, string messageType, MessagePurpose purpose, MessageProvider provider,
        string providerDomain, string providerCorrelationId, string subject, string toEmailAddress, string toName,
        string fromName, string fromEmailAddress, string substitutions, AccountUser? accountInvitation = null)
    {
        TemplateKey = templateKey;
        MessageType = messageType;
        Purpose = purpose;
        Provider = provider;
        ProviderDomain = providerDomain;
        ProviderCorrelationId = providerCorrelationId;
        Subject = subject;
        ToEmailAddress = toEmailAddress;
        ToName = toName;
        FromName = fromName;
        FromEmailAddress = fromEmailAddress;
        Substitutions = substitutions;
        AccountInvitationAccountId = accountInvitation?.AccountId;
        AccountInvitationUserId = accountInvitation?.UserId;
        DeliveryStatus = MessageDeliveryStatus.Submitting;
        DeliveryStatusOn = DateTime.UtcNow;
    }

    public MessageLog()
    {
    }

    public string TemplateKey { get; set; } = null!;
    public string MessageType { get; set; } = null!;
    public MessagePurpose? Purpose { get; set; }
    public MessageProvider? Provider { get; set; }

    [MaxLength(PropertyLength.MediumText)]
    public string? ProviderDomain { get; set; }

    [MaxLength(PropertyLength.Id)]
    public string? ProviderCorrelationId { get; set; }

    public string? ProviderMessageId { get; set; }

    public MessageDeliveryStatus? DeliveryStatus { get; set; }
    public DateTime? DeliveryStatusOn { get; set; }
    public string? DeliveryStatusMessage { get; set; }

    public string Subject { get; set; } = null!;
    public string ToEmailAddress { get; set; } = null!;
    public string ToName { get; set; } = null!;
    public string FromName { get; set; } = null!;
    public string FromEmailAddress { get; set; } = null!;
    public string Substitutions { get; set; } = null!;

    [MaxLength(PropertyLength.Id)]
    public string? AccountInvitationAccountId { get; set; }

    [MaxLength(PropertyLength.Id)]
    public string? AccountInvitationUserId { get; set; }

    public virtual AccountUser? AccountInvitation { get; set; }
    public virtual ICollection<MessageLogEvent> Events { get; set; } = new HashSet<MessageLogEvent>();
}

public class MessageLogConfiguration : BaseEntityTypeConfiguration<MessageLog>
{
    public override void Configure(EntityTypeBuilder<MessageLog> builder)
    {
        base.Configure(builder);
        ConfigureEnum(builder, e => e.Purpose);
        ConfigureEnum(builder, e => e.Provider);
        ConfigureEnum(builder, e => e.DeliveryStatus);

        builder.HasIndex(e => e.ProviderCorrelationId)
            .IsUnique()
            .HasFilter("\"ProviderCorrelationId\" IS NOT NULL");

        builder.HasOne(e => e.AccountInvitation)
            .WithMany(e => e.InvitationMessageLogs)
            .HasForeignKey(e => new { e.AccountInvitationAccountId, e.AccountInvitationUserId })
            .HasPrincipalKey(e => new { e.AccountId, e.UserId })
            .OnDelete(DeleteBehavior.SetNull);
    }
}
