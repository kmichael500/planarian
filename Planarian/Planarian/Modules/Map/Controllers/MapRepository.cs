using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Map.Models;
using Planarian.Modules.Query.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Map.Controllers;

public class MapRepository : RepositoryBase
{
    private readonly CaveRepository _caveRepository;

    public MapRepository(
        PlanarianDbContext dbContext,
        RequestUser requestUser,
        CaveRepository caveRepository) : base(dbContext, requestUser)
    {
        _caveRepository = caveRepository;
    }

    public async Task<IEnumerable<object>> GetMapData(
        double north,
        double south,
        double east,
        double west,
        int zoom,
        CancellationToken cancellationToken)
    {
        return await DbContext.Entrances
            .AsNoTracking()
            .Where(entrance =>
                entrance.Cave!.AccountId == RequestUser.AccountId &&
                DbContext.UserCavePermissionView.Any(permission =>
                    permission.AccountId == RequestUser.AccountId &&
                    permission.UserId == RequestUser.Id &&
                    permission.CaveId == entrance.CaveId))
            .Select(entrance => new PointDto
            {
                Latitude = entrance.Location.Y,
                Longitude = entrance.Location.X,
                Name = entrance.Cave!.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CoordinateDto> GetMapCenter()
    {
        const double defaultLatitude = 39.8333;
        const double defaultLongitude = -98.5855;

        var visibleEntrances = DbContext.Entrances
            .AsNoTracking()
            .Where(entrance =>
                entrance.Cave != null &&
                entrance.Cave.AccountId == RequestUser.AccountId &&
                DbContext.UserCavePermissionView.Any(permission =>
                    permission.AccountId == RequestUser.AccountId &&
                    permission.UserId == RequestUser.Id &&
                    permission.CaveId == entrance.CaveId));

        var averageLatitude = await visibleEntrances
            .Select(entrance => (double?)entrance.Location.Y)
            .AverageAsync();
        var averageLongitude = await visibleEntrances
            .Select(entrance => (double?)entrance.Location.X)
            .AverageAsync();

        return new CoordinateDto
        {
            Latitude = averageLatitude ?? defaultLatitude,
            Longitude = averageLongitude ?? defaultLongitude
        };
    }

    public async Task<byte[]?> GetEntrancesMVTAsync(int z, int x, int y, FilterQuery filterQuery, CancellationToken cancellationToken)
    {
        filterQuery ??= new FilterQuery();

        string additionalFilterClause = string.Empty;
        List<string>? filteredCaveIds = null;

        if (filterQuery.Conditions?.Any() == true)
        {
            filteredCaveIds = await _caveRepository
                .GetCaveIds(filterQuery)
                .ToListAsync(cancellationToken);

            if (filteredCaveIds.Count == 0)
            {
                return null;
            }

            additionalFilterClause = """
                            AND "Entrances"."CaveId" = ANY(@caveIds)
                """;
        }

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
                                    "Favorites"."UserId" = @userId
                                    AND "Favorites"."AccountId" = @accountId
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
                            AND "Caves"."AccountId" = @accountId
                            AND ucp."UserId" = @userId
                            {3}
                    ) AS tile_geom
                    """;

        query = string.Format(query, z, x, y, additionalFilterClause);

        await using var command = DbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = query;
        var accountIdParameter = new NpgsqlParameter("@accountId", NpgsqlDbType.Text)
        {
            Value = RequestUser.AccountId
        };
        command.Parameters.Add(accountIdParameter);

        var userIdParameter = new NpgsqlParameter("@userId", NpgsqlDbType.Text)
        {
            Value = RequestUser.Id
        };
        command.Parameters.Add(userIdParameter);

        if (filteredCaveIds != null)
        {
            var caveIdsParameter = new NpgsqlParameter("@caveIds", NpgsqlDbType.Array | NpgsqlDbType.Text)
            {
                Value = filteredCaveIds.ToArray()
            };
            command.Parameters.Add(caveIdsParameter);
        }

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
