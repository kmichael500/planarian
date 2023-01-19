using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class MessageType : EntityBase
{
    public string Key { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Description { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    public string? Subject { get; set; } = null!;
    public string FromName { get; set; } = null!;
    public string FromEmail { get; set; } = null!;
    public string? Mjml { get; set; } = null!;
    public string? Html { get; set; } = null!;
}

public class MessageTypeConfiguration : IEntityTypeConfiguration<MessageType>
{
    public void Configure(EntityTypeBuilder<MessageType> builder)
    {
    }
}