using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NetTopologySuite.Geometries.Utilities;
using Npgsql;
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
                .Where(e => e.Cave!.AccountId == RequestUser.AccountId)
                .Select(e => new { e.Location, e.Cave!.Name, e.Cave.Id })
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

        gridSize = Math.Max(0.045, Math.Min(0.8, gridSize));

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
        double averageLatitude = 39.8333;
        double averageLongitude = -98.5855;
        try
        {
            averageLatitude = await DbContext.Entrances
                .Where(e => e.Cave.AccountId == RequestUser.AccountId)
                .AverageAsync(e => e.Location.Y);

            averageLongitude = await DbContext.Entrances
                .Where(e => e.Cave.AccountId == RequestUser.AccountId)
                .AverageAsync(e => e.Location.X);
        }
        catch (Exception)
        {
            // ignored
            // will fail if no entrances added
        }

        return new CoordinateDto { Latitude = averageLatitude, Longitude = averageLongitude };
    }

    public async Task<byte[]?> GetEntrancesMVTAsync(int z, int x, int y, CancellationToken cancellationToken)
    {
        var query = """

                    WITH tile AS (
                        SELECT 
                             ST_TileEnvelope({0}, {1}, {2}) AS bbox_3857,
                             ST_Transform(ST_TileEnvelope({0}, {1}, {2}), 4326) AS bbox_native
                    )
                    SELECT ST_AsMVT(tile_geom.*, 'entrances', 4096, 'geom') AS mvt
                    FROM (
                        SELECT 
                            "Entrances"."ReportedByUserId",
                            "Entrances"."CaveId",
                            "Caves"."Name" as CaveName,
                            "Entrances"."LocationQualityTagId",
                            "Entrances"."Name",
                            "Entrances"."IsPrimary",
                            "Entrances"."Description",
                            (SELECT EXISTS(
                                SELECT 1 
                                FROM "Favorites"
                                WHERE 
                                    "Favorites"."UserId" = '{4}'
                                    AND "Favorites"."AccountId" = '{3}'
                                    AND "Favorites"."CaveId" = "Entrances"."CaveId"
                            )) AS "IsFavorite",
                            ST_AsMVTGeom(
                                ST_Transform("Entrances"."Location", 3857),
                                tile.bbox_3857,
                                4096,
                                0,
                                true
                            ) AS geom
                        FROM 
                            "Entrances"
                        JOIN 
                            "Caves" ON "Entrances"."CaveId" = "Caves"."Id"
                        JOIN 
                            "UserCavePermissions" ucp ON "Caves"."Id" = ucp."CaveId"
                                                          AND "Caves"."AccountId" = ucp."AccountId"
                        , tile
                        WHERE 
                            ST_Intersects("Entrances"."Location", tile.bbox_native)
                            AND "Caves"."AccountId" = '{3}'
                            AND ucp."UserId" = '{4}'
                    ) AS tile_geom
                    """;

        query = string.Format(query, z, x, y, RequestUser.AccountId, RequestUser.Id);

        await using var command = DbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = query;
        await DbContext.Database.OpenConnectionAsync(cancellationToken: cancellationToken);

        await using var result = await command.ExecuteReaderAsync(cancellationToken);
        if (await result.ReadAsync(cancellationToken))
        {
            return result["mvt"] as byte[];
        }

        return null;
    }

    public async Task<List<string>> GetLinePlotIds(
        double north,
        double south,
        double east,
        double west,
        double zoom,
        CancellationToken cancellationToken)
    {
        if (zoom < 11)
            return new List<string>();

        const string sql = """
                           WITH view_box AS (
                             -- MakeEnvelope(minLon, minLat, maxLon, maxLat, SRID)
                             SELECT ST_MakeEnvelope(@west, @south, @east, @north, 4326) AS bbox
                           )
                           SELECT DISTINCT cg."Id"
                           FROM "CaveGeoJsons" cg
                           JOIN "Entrances" e  ON e."CaveId" = cg."CaveId"
                           JOIN "Caves" c     ON c."Id"     = cg."CaveId"
                           JOIN "UserCavePermissions" ucp
                             ON ucp."CaveId"    = c."Id"
                            AND ucp."AccountId" = c."AccountId"
                           WHERE
                             -- fast index filter
                             e."Location" && (SELECT bbox FROM view_box)
                             -- then exact containment
                             AND ST_Within(e."Location", (SELECT bbox FROM view_box))
                             AND c."AccountId" = @accountId
                             AND ucp."UserId"  = @userId;
                           """;

        var conn = (Npgsql.NpgsqlConnection)DbContext.Database.GetDbConnection();
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new Npgsql.NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("west",       west);
        cmd.Parameters.AddWithValue("south",      south);
        cmd.Parameters.AddWithValue("east",       east);
        cmd.Parameters.AddWithValue("north",      north);
        cmd.Parameters.AddWithValue("accountId",  RequestUser.AccountId);
        cmd.Parameters.AddWithValue("userId",     RequestUser.Id);

        var ids = new List<string>();
        await using var rdr = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await rdr.ReadAsync(cancellationToken))
            ids.Add(rdr.GetString(0));

        return ids;
    }


    public async Task<System.Text.Json.JsonElement?> GetLinePlotGeoJson(
        string plotId, CancellationToken cancellationToken)
    {
        var record = await DbContext.CaveGeoJsons
            .AsNoTracking()
            .Where(e =>
                e.Cave.AccountId == RequestUser.AccountId
                && DbContext.UserCavePermissionView.Any(ucp =>
                    ucp.AccountId == RequestUser.AccountId &&
                    ucp.UserId == RequestUser.Id &&
                    ucp.CaveId == e.Cave.Id)
            )
            .FirstOrDefaultAsync(
                cg => cg.Id == plotId
                      && cg.Cave.AccountId == RequestUser.AccountId,
                cancellationToken);

        if (record == null)
            return null;

        return System.Text.Json.JsonDocument
            .Parse(record.GeoJson)
            .RootElement;
    }
    
}