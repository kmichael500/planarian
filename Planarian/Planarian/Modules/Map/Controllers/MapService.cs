using Planarian.Model.Shared;
using Planarian.Modules.Map.Services;
using Planarian.Shared.Base;

namespace Planarian.Modules.Map.Controllers;

public class MapService : ServiceBase<MapRepository>
{
    public MapService(MapRepository repository, RequestUser requestUser) : base(repository, requestUser)
    {
    }

    public async Task<IEnumerable<object>> GetMapData(double north, double south, double east, double west, int zoom,
        CancellationToken cancellationToken)
    {
        var result = await Repository.GetMapData(north, south, east, west, zoom, cancellationToken);

        return result;
    }

    public async Task<CoordinateDto> GetMapCenter()
    {
        var result = await Repository.GetMapCenter();

        return result;
    }
}