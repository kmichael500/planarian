namespace Planarian.Modules.Map.Models;

public sealed record StreamGageLocation(
    string Id,
    string SiteCode,
    string SiteName,
    double Latitude,
    double Longitude);
