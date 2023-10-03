using Planarian.Model.Shared;

namespace Planarian.Modules.App.Models;

public class AppInitializeVm
{
    public string SignalrBaseUrl { get; set; } = null!;
    public IEnumerable<SelectListItem<string>> AccountIds { get; set; } = new HashSet<SelectListItem<string>>();
}