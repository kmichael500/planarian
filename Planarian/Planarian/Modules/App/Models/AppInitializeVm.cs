using Planarian.Model.Shared;

namespace Planarian.Modules.App.Models;

public class AppInitializeVm
{
    public AppInitializeVm(string serverBaseUrl, string signalrBaseUrl)
    {
        ServerBaseUrl = serverBaseUrl;
        SignalrBaseUrl = signalrBaseUrl;
    }

    public string SignalrBaseUrl { get; set; } 
    public IEnumerable<SelectListItem<string>> AccountIds { get; set; } = new HashSet<SelectListItem<string>>();
    public string ServerBaseUrl { get; set; }
}