using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries.Utilities;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Map.Controllers;

public class MapRepository : RepositoryBase
{
    private readonly MemoryCache _cache;
    private string _allPointsCacheKey;

    public MapRepository(PlanarianDbContext dbContext, RequestUser requestUser, MemoryCache cache) : base(dbContext,
        requestUser)
    {
        _cache = cache;
    }

    public async Task<IEnumerable<object>> GetMapData(double north, double south, double east, double west, int zoom,
        CancellationToken cancellationToken)
    {

        _allPointsCacheKey = $"all-points-{RequestUser.AccountId}";

        var allPoints = await _cache.GetOrCreateAsync(_allPointsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

            return await DbContext.Entrances
                .Where(e => e.Cave.AccountId == RequestUser.AccountId)
                .Select(e => new { e.Location, e.Cave.Name, e.Cave.Id })
                .ToListAsync(cancellationToken: cancellationToken);
        });

        return allPoints.Select(p => new PointDto
        {
            Latitude = p.Location.Y,
            Longitude = p.Location.X,
            Name = p.Name
        });



        const double marginRatio = 0.3; // Adjust the margin ratio as per your needs.

        var latMargin = (north - south) * marginRatio;
        var lngMargin = (east - west) * marginRatio;

        var expandedNorth = north + latMargin;
        var expandedSouth = south - latMargin;
        var expandedEast = east + lngMargin;
        var expandedWest = west - lngMargin;

        var points = allPoints
            .Where(e => e.Location.Y <= expandedNorth && e.Location.Y >= expandedSouth &&
                        e.Location.X <= expandedEast && e.Location.X >= expandedWest)
            .Select(e => new { e.Location, e.Name, e.Id }).ToList();

        const int zoomThreshold = 14;
        var minClusterSize = GetMinClusterSize(zoom);
        if (minClusterSize < 3) minClusterSize = 3;
        minClusterSize = 10;
        if (zoom < zoomThreshold)
        {
            var gridSize = GetGridSize(zoom);
            return points.GroupBy(p => new
                {
                    LatGroup = Math.Round(p.Location.Y / gridSize),
                    LngGroup = Math.Round(p.Location.X / gridSize)
                })
                .SelectMany(g => g.Count() >= minClusterSize
                    ? new object[]
                    {
                        new ClusterDto
                        {
                            Latitude = g.Average(p => p.Location.Y),
                            Longitude = g.Average(p => p.Location.X),
                            Count = g.Count(),
                            HullCoordinates = CalculateConvexHull(g.Select(p => new CoordinateDto
                                { Latitude = p.Location.Y, Longitude = p.Location.X }).ToList())
                        }
                    }
                    : g.Select(p => new PointDto
                    {
                        Latitude = p.Location.Y,
                        Longitude = p.Location.X,
                        Name = p.Name
                    }).ToArray());

        }
        else
        {
            return points.Select(p => new PointDto
            {
                Latitude = p.Location.Y,
                Longitude = p.Location.X,
                Name = p.Name
            });
        }
    }

    private double GetGridSize(int zoom)
    {
        const double m = -0.2;  
        const double b = 2.2;  

        var gridSize = m * zoom + b;

        gridSize = Math.Max(0.045, Math.Min(0.8, gridSize));  // Adjust min and max values as needed
    
        return gridSize;
    }
    
    private int GetMinClusterSize(int zoom)
    {
        return Math.Max(2, 20 / (zoom + 1));
    }







    public static List<CoordinateDto> CalculateConvexHull(List<CoordinateDto> points)
    {
        if (points == null) throw new ArgumentNullException("points");
        if (points.Count < 3) throw new ArgumentException("At least 3 points required", "points");

        var hull = new List<CoordinateDto>();
        var pivot = points[0];

        // Find the leftmost (smallest longitude) point as the pivot
        foreach (var point in points)
            if (point.Longitude < pivot.Longitude)
                pivot = point;

        CoordinateDto endPoint;
        do
        {
            hull.Add(pivot);
            endPoint = points[0];

            for (var i = 1; i < points.Count; i++)
            {
                if (pivot.Equals(endPoint) || IsLeftTurn(pivot, points[i], endPoint))
                    endPoint = points[i];
            }

            pivot = endPoint;

        } while (!endPoint.Equals(hull[0])); // Until the hull is closed

        return hull;
    }

    private static bool IsLeftTurn(CoordinateDto a, CoordinateDto b, CoordinateDto c)
    {
        return (b.Longitude - a.Longitude) * (c.Latitude - a.Latitude) -
            (b.Latitude - a.Latitude) * (c.Longitude - a.Longitude) > 0;
    }

    public async Task<CoordinateDto> GetMapCenter()
    {
        var averageLatitude = await DbContext.Entrances
            .Where(e => e.Cave.AccountId == RequestUser.AccountId)
            .AverageAsync(e => e.Location.Y);

        var averageLongitude = await DbContext.Entrances
            .Where(e => e.Cave.AccountId == RequestUser.AccountId)
            .AverageAsync(e => e.Location.X);
        
        return new CoordinateDto{Latitude = averageLatitude, Longitude = averageLongitude};
    }
}