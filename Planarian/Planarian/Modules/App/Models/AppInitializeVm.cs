using Planarian.Model.Shared;

namespace Planarian.Modules.App.Models;

public class AppInitializeVm
{
    public AppInitializeVm(string serverBaseUrl, string signalrBaseUrl, string supportName, string supportEmail)
    {
        ServerBaseUrl = serverBaseUrl;
        SignalrBaseUrl = signalrBaseUrl;
        SupportName = supportName;
        SupportEmail = supportEmail;
    }

    public string SignalrBaseUrl { get; set; } 
    public IEnumerable<SelectListItem<string>> AccountIds { get; set; } = new HashSet<SelectListItem<string>>();
    public string ServerBaseUrl { get; set; }
    public string SupportName { get; set; }
    public string SupportEmail { get; set; }
    public IEnumerable<string> Permissions { get; set; } = new HashSet<string>();
}
