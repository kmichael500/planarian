namespace Planarian.Modules.Map.Models;

public sealed record StreamGageRequest(
    IReadOnlyList<StreamGageSearchOrigin> Origins,
    double DistanceMiles);
