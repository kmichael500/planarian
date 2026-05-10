using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Authentication.Models;

public class BrowserLoginVm : LoginCredentialsVm
{
    public bool? Remember { get; set; }

    [MaxLength(PropertyLength.InvitationCode)]
    public string? InvitationCode { get; set; }
}
