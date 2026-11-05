using Planarian.Model.Shared;

namespace Planarian.Modules.App.Models;

public sealed class DisabledAccountVm : SelectListItem<string>
{
    public string? DisableMessage { get; set; }
}
