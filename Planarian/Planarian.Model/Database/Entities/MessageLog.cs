using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class MessageLog : EntityBase
{
    public MessageLog(string messageKey, string messageType, string subject, string toEmailAddress, string toName,
        string fromName, string fromEmailAddress, string substitutions)
    {
        MessageKey = messageKey;
        MessageType = messageType;
        Subject = subject;
        ToEmailAddress = toEmailAddress;
        ToName = toName;
        FromName = fromName;
        FromEmailAddress = fromEmailAddress;
        Substitutions = substitutions;
    }

    public MessageLog()
    {
    }

    public string MessageKey { get; set; } = null!;
    public string MessageType { get; set; } = null!;
    public string Subject { get; set; } = null!;
    public string ToEmailAddress { get; set; } = null!;
    public string ToName { get; set; } = null!;
    public string FromName { get; set; } = null!;
    public string FromEmailAddress { get; set; } = null!;
    public string Substitutions { get; set; } = null!;
}

public class MessageLogConfiguration : IEntityTypeConfiguration<MessageLog>
{
    public void Configure(EntityTypeBuilder<MessageLog> builder)
    {
    }
}