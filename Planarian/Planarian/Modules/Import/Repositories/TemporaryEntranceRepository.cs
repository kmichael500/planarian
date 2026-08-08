using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Shared;
using Planarian.Modules.Import.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Import.Repositories;

/// <summary>
/// Owns the short-lived staging table used by the entrance importer.
/// The table name is generated per import, so this is intentionally a small
/// parameterized provider-specific adapter rather than an EF entity set.
/// </summary>
public class TemporaryEntranceRepository : RepositoryBase<PlanarianDbContextBase>
{
    private readonly string _temporaryEntranceTableName = "TemporaryEntrance" + Guid.NewGuid().ToString("N");
    private NpgsqlConnection? _connection;

    public TemporaryEntranceRepository(PlanarianDbContextBase dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
    }

    private string Table => _temporaryEntranceTableName.Quote();

    private async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null)
        {
            _connection = (NpgsqlConnection)DbContext.Database.GetDbConnection();
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    public async Task CreateTable()
    {
        var connection = await GetConnectionAsync();
        await using var command = new NpgsqlCommand($@"
            CREATE TEMP TABLE {Table} (
                ""Id"" varchar(50) NOT NULL,
                ""CaveId"" varchar(50),
                ""CountyDisplayId"" varchar(50) NOT NULL,
                ""CountyCaveNumber"" integer NOT NULL,
                ""ReportedByUserId"" varchar(50),
                ""LocationQualityTagId"" varchar(50) NOT NULL,
                ""Name"" varchar(255),
                ""IsPrimary"" boolean NOT NULL,
                ""Description"" text,
                ""Latitude"" double precision NOT NULL,
                ""Longitude"" double precision NOT NULL,
                ""Elevation"" double precision NOT NULL,
                ""ReportedOn"" timestamp with time zone,
                ""PitFeet"" double precision,
                ""CreatedByUserId"" varchar(50),
                ""ModifiedByUserId"" varchar(50),
                ""CreatedOn"" timestamp with time zone NOT NULL,
                ""ModifiedOn"" timestamp with time zone
            ) ON COMMIT PRESERVE ROWS", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> InsertEntrances(IEnumerable<TemporaryEntrance> entrances,
        Action<int, int> onBatchProcessed)
    {
        var rows = entrances.ToList();
        var connection = await GetConnectionAsync();
        var processed = 0;

        foreach (var batch in rows.Chunk(500))
        {
            await using var command = new NpgsqlCommand { Connection = connection };
            var values = new List<string>();
            for (var i = 0; i < batch.Length; i++)
            {
                var row = batch[i];
                var p = $"p{i}_";
                values.Add($"(@{p}id, @{p}cave, @{p}county, @{p}number, @{p}reportedBy, @{p}quality, @{p}name, @{p}primary, @{p}description, @{p}lat, @{p}lng, @{p}elevation, @{p}reportedOn, @{p}pit, @{p}createdBy, @{p}modifiedBy, @{p}createdOn, @{p}modifiedOn)");
                Add(command, $"{p}id", row.Id);
                Add(command, $"{p}cave", row.CaveId);
                Add(command, $"{p}county", row.CountyDisplayId);
                Add(command, $"{p}number", row.CountyCaveNumber);
                Add(command, $"{p}reportedBy", row.ReportedByUserId);
                Add(command, $"{p}quality", row.LocationQualityTagId);
                Add(command, $"{p}name", row.Name);
                Add(command, $"{p}primary", row.IsPrimary);
                Add(command, $"{p}description", row.Description);
                Add(command, $"{p}lat", row.Latitude);
                Add(command, $"{p}lng", row.Longitude);
                Add(command, $"{p}elevation", row.Elevation);
                Add(command, $"{p}reportedOn", row.ReportedOn);
                Add(command, $"{p}pit", row.PitFeet);
                Add(command, $"{p}createdBy", row.CreatedByUserId);
                Add(command, $"{p}modifiedBy", row.ModifiedByUserId);
                Add(command, $"{p}createdOn", row.CreatedOn);
                Add(command, $"{p}modifiedOn", row.ModifiedOn);
            }

            command.CommandText = $"INSERT INTO {Table} (\"Id\", \"CaveId\", \"CountyDisplayId\", \"CountyCaveNumber\", \"ReportedByUserId\", \"LocationQualityTagId\", \"Name\", \"IsPrimary\", \"Description\", \"Latitude\", \"Longitude\", \"Elevation\", \"ReportedOn\", \"PitFeet\", \"CreatedByUserId\", \"ModifiedByUserId\", \"CreatedOn\", \"ModifiedOn\") VALUES {string.Join(",", values)}";
            await command.ExecuteNonQueryAsync();
            processed += batch.Length;
            onBatchProcessed(processed, rows.Count);
        }

        return processed;
    }

    private static void Add(NpgsqlCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    public async Task<(List<string> unassociatedEntrances, List<TemporaryEntranceResult> associatedEntrances)> UpdateTemporaryEntranceWithCaveId()
    {
        var connection = await GetConnectionAsync();
        await using (var update = new NpgsqlCommand($@"
            UPDATE {Table} t SET ""CaveId"" = c.""Id""
            FROM ""Caves"" c INNER JOIN ""Counties"" co ON co.""Id"" = c.""CountyId""
            WHERE t.""CountyCaveNumber"" = c.""CountyNumber""
              AND t.""CountyDisplayId"" = co.""DisplayId""
              AND c.""AccountId"" = @account", connection))
        {
            update.Parameters.AddWithValue("account", RequestUser.AccountId!);
            await update.ExecuteNonQueryAsync();
        }

        var unassociated = await ReadIdsAsync($"SELECT \"Id\" FROM {Table} WHERE \"CaveId\" IS NULL", connection);
        var associated = new List<TemporaryEntranceResult>();
        await using (var command = new NpgsqlCommand($@"
            SELECT t.""Id"", t.""CaveId"", c.""Name"", co.""DisplayId"" || '-' || c.""CountyNumber""
            FROM {Table} t INNER JOIN ""Caves"" c ON c.""Id"" = t.""CaveId""
            INNER JOIN ""Counties"" co ON co.""Id"" = c.""CountyId""
            WHERE t.""CaveId"" IS NOT NULL AND c.""AccountId"" = @account", connection))
        {
            command.Parameters.AddWithValue("account", RequestUser.AccountId!);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                associated.Add(new TemporaryEntranceResult
                {
                    Id = reader.GetString(0), CaveId = reader.GetString(1), CaveName = reader.GetString(2),
                    DisplayId = reader.GetString(3)
                });
        }

        await using (var delete = new NpgsqlCommand($"DELETE FROM {Table} WHERE \"CaveId\" IS NULL", connection))
            await delete.ExecuteNonQueryAsync();
        return (unassociated, associated);
    }

    private static async Task<List<string>> ReadIdsAsync(string sql, NpgsqlConnection connection)
    {
        var result = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<List<string>> GetInvalidIsPrimaryRecords()
    {
        var connection = await GetConnectionAsync();
        var result = new List<string>();
        await using var command = new NpgsqlCommand($@"
            SELECT t.""Id""
            FROM {Table} t
            WHERE t.""CaveId"" IS NOT NULL
            GROUP BY t.""Id"", t.""CaveId""
            HAVING (SELECT count(*) FROM {Table} x WHERE x.""CaveId"" = t.""CaveId"" AND x.""IsPrimary"") +
                   (SELECT count(*) FROM ""Entrances"" e WHERE e.""CaveId"" = t.""CaveId"" AND e.""IsPrimary"") <> 1", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<Dictionary<string, int>> GetExistingEntranceCounts(IEnumerable<string> caveIds, CancellationToken cancellationToken)
    {
        var ids = caveIds.Distinct().ToList();
        return await DbContext.Entrances.IgnoreQueryFilters().Where(e => ids.Contains(e.CaveId))
            .GroupBy(e => e.CaveId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(e => e.Key, e => e.Count, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetExistingPrimaryEntranceCounts(IEnumerable<string> caveIds, CancellationToken cancellationToken)
    {
        var ids = caveIds.Distinct().ToList();
        return await DbContext.Entrances.IgnoreQueryFilters().Where(e => ids.Contains(e.CaveId) && e.IsPrimary)
            .GroupBy(e => e.CaveId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(e => e.Key, e => e.Count, cancellationToken);
    }

    public async Task MigrateTemporaryEntrancesAsync()
    {
        var connection = await GetConnectionAsync();
        await using var command = new NpgsqlCommand($@"
            INSERT INTO ""Entrances"" (""Id"", ""CaveId"", ""LocationQualityTagId"", ""Name"", ""IsPrimary"", ""Description"", ""Location"", ""ReportedOn"", ""ReportedByUserId"", ""PitDepthFeet"", ""CreatedByUserId"", ""ModifiedByUserId"", ""CreatedOn"", ""ModifiedOn"")
            SELECT ""Id"", ""CaveId"", ""LocationQualityTagId"", ""Name"", ""IsPrimary"", ""Description"", ST_SetSRID(ST_MakePoint(""Longitude"", ""Latitude"", ""Elevation""), 4326), ""ReportedOn"", ""ReportedByUserId"", ""PitFeet"", ""CreatedByUserId"", ""ModifiedByUserId"", ""CreatedOn"", ""ModifiedOn"" FROM {Table}", connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<TemporaryEntrance>> GetEntrancesById(string id) => await ReadEntrancesAsync("WHERE \"Id\" = @id", new("id", id));

    public async Task<List<TemporaryEntrance>> GetAllEntrances() => await ReadEntrancesAsync();

    private async Task<List<TemporaryEntrance>> ReadEntrancesAsync(string where = "", NpgsqlParameter? parameter = null)
    {
        var connection = await GetConnectionAsync();
        await using var command = new NpgsqlCommand($"SELECT * FROM {Table} {where}", connection);
        if (parameter is not null) command.Parameters.Add(parameter);
        var result = new List<TemporaryEntrance>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(new TemporaryEntrance
            {
                Id = reader.GetString(reader.GetOrdinal("Id")), CaveId = GetString(reader, "CaveId"),
                CountyDisplayId = reader.GetString(reader.GetOrdinal("CountyDisplayId")), CountyCaveNumber = reader.GetInt32(reader.GetOrdinal("CountyCaveNumber")),
                ReportedByUserId = GetString(reader, "ReportedByUserId"), LocationQualityTagId = reader.GetString(reader.GetOrdinal("LocationQualityTagId")),
                Name = GetString(reader, "Name"), IsPrimary = reader.GetBoolean(reader.GetOrdinal("IsPrimary")), Description = GetString(reader, "Description"),
                Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")), Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")), Elevation = reader.GetDouble(reader.GetOrdinal("Elevation")),
                ReportedOn = GetDate(reader, "ReportedOn"), PitFeet = GetDouble(reader, "PitFeet"), CreatedByUserId = GetString(reader, "CreatedByUserId"), ModifiedByUserId = GetString(reader, "ModifiedByUserId"),
                CreatedOn = reader.GetDateTime(reader.GetOrdinal("CreatedOn")), ModifiedOn = GetDate(reader, "ModifiedOn")
            });
        return result;
    }

    private static string? GetString(NpgsqlDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetString(reader.GetOrdinal(column));
    private static double? GetDouble(NpgsqlDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetDouble(reader.GetOrdinal(column));
    private static DateTime? GetDate(NpgsqlDataReader reader, string column) => reader.IsDBNull(reader.GetOrdinal(column)) ? null : reader.GetDateTime(reader.GetOrdinal(column));

    public async Task DeleteExistingEntrancesForImportedCaves(CancellationToken cancellationToken)
    {
        var caveIds = (await ReadEntrancesAsync("WHERE \"CaveId\" IS NOT NULL")).Select(e => e.CaveId!).Distinct().ToList();
        if (caveIds.Count == 0) return;
        var entranceIds = await DbContext.Entrances.IgnoreQueryFilters().Where(e => caveIds.Contains(e.CaveId)).Select(e => e.Id).ToListAsync(cancellationToken);
        if (entranceIds.Count == 0) return;
        await DbContext.EntranceStatusTags.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceHydrologyTags.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.FieldIndicationTags.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceReportedByNameTags.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceOtherTag.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.Entrances.IgnoreQueryFilters().Where(e => entranceIds.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DropTable()
    {
        if (_connection is null) return;
        await using var command = new NpgsqlCommand($"DROP TABLE IF EXISTS {Table}", _connection);
        await command.ExecuteNonQueryAsync();
    }
}
