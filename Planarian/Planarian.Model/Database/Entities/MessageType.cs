using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.Entities;

public class MessageType : EntityBase
{
    [MaxLength(PropertyLength.Key)] public string Key { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string Type { get; set; } = null!;
    [MaxLength(PropertyLength.MediumText)] public string Description { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;
    [MaxLength(PropertyLength.SmallText)] public string? Subject { get; set; } = null!;
    [MaxLength(PropertyLength.Name)] public string FromName { get; set; } = null!;

    [MaxLength(PropertyLength.EmailAddress)]
    public string FromEmail { get; set; } = null!;

    public string? Mjml { get; set; } = null!;
    public string? Html { get; set; } = null!;
}

public class MessageTypeConfiguration : BaseEntityTypeConfiguration<MessageType>
{
}