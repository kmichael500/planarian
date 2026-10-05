using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Model;

public class RevokeAccountAccessRequest
{
    [MaxLength(PropertyLength.MediumText)]
    public string? Reason { get; set; }
}
