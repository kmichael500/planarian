namespace Planarian.Modules.Map.Models;

public sealed record HydrologyFeature(
    string Id,
    string? Name,
    string FeatureType,
    double Latitude,
    double Longitude,
    string Source);
