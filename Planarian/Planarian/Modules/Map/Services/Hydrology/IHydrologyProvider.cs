using Planarian.Modules.Map.Models;

namespace Planarian.Modules.Map.Services.Hydrology;

public interface IHydrologyProvider
{
    Task<IReadOnlyList<HydrologyFeature>> GetFeaturesInBoundsAsync(
        double north,
        double south,
        double east,
        double west,
        CancellationToken cancellationToken);
}
