using Planarian.Model.Shared;

namespace Planarian.Modules.App.Models;

public class AppInitializeVm
{
    public AppInitializeVm(string apiBaseUrl, string signalrBaseUrl, string supportName, string supportEmail)
    {
        ApiBaseUrl = apiBaseUrl;
        SignalrBaseUrl = signalrBaseUrl;
        SupportName = supportName;
        SupportEmail = supportEmail;
    }

    public string SignalrBaseUrl { get; set; } 
    public IEnumerable<SelectListItem<string>> AccountIds { get; set; } = new HashSet<SelectListItem<string>>();
    public string ApiBaseUrl { get; set; }
    public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
    public AppInitializeCurrentUserVm? CurrentUser { get; set; }
    public string? AntiforgeryRequestToken { get; set; }
    public string SupportName { get; set; }
    public string SupportEmail { get; set; }
}
