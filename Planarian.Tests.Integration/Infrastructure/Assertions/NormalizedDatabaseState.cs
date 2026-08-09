using System.Text;
using Npgsql;

namespace Planarian.Tests;

/// <summary>
/// Canonical PostgreSQL row state used when a test must prove that an operation
/// preserved data, rather than merely preserving row counts. PostgreSQL's xmin
/// is included so an otherwise invisible rewrite of a Cave (or related row) is
/// observable.
/// </summary>
internal static class NormalizedDatabaseState
{
    private static readonly string[] ImportTables =
    [
        "States", "Accounts", "AccountStates", "Counties", "TagTypes", "Caves",
        "GeologyTags", "GeologicAgeTags", "MapStatusTags", "PhysiographicProvinceTags",
        "ArcheologyTags", "BiologyTags", "CaveOtherTags", "CartographerNameTags",
        "CaveReportedByNameTags", "Entrances", "EntranceStatusTags",
        "EntranceHydrologyTags", "FieldIndicationTags", "EntranceReportedByNameTags",
        "EntranceOtherTag", "Files", "CaveRevisions", "CaveImportBatches",
        "CaveChangeRequests", "CaveProposalVersions", "CaveChangeRequestStagedFiles",
        "CavePermissions", "CaveGeoJsons"
    ];

    public static Task<string> CaptureAllAsync(PostgresTestDatabase database) =>
        CaptureAsync(database, accountId: null);

    public static Task<string> CaptureTenantAsync(PostgresTestDatabase database, string accountId) =>
        CaptureAsync(database, accountId);

    private static async Task<string> CaptureAsync(PostgresTestDatabase database, string? accountId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var result = new StringBuilder();

        foreach (var table in ImportTables)
        {
            if (!await ExistsAsync(connection, table))
                continue;

            var predicate = accountId is null ? null : await TenantPredicateAsync(connection, table);
            if (accountId is not null && predicate is null)
                continue;

            var quoted = '"' + table.Replace("\"", "\"\"") + '"';
            var sql = $"select row_to_json(x)::text from (select t.*, t.xmin::text as \"__xmin\" from {quoted} t{predicate}) x order by row_to_json(x)::text";
            await using var command = new NpgsqlCommand(sql, connection);
            if (accountId is not null)
                command.Parameters.AddWithValue("account", accountId);

            result.Append('[').Append(table).AppendLine("]");
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.AppendLine(reader.GetString(0));
        }

        return result.ToString();
    }

    private static async Task<bool> ExistsAsync(NpgsqlConnection connection, string table)
    {
        await using var command = new NpgsqlCommand("select to_regclass('public.' || @table) is not null", connection);
        command.Parameters.AddWithValue("table", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string?> TenantPredicateAsync(NpgsqlConnection connection, string table)
    {
        var columns = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = new NpgsqlCommand("select column_name from information_schema.columns where table_schema='public' and table_name=@table", connection))
        {
            command.Parameters.AddWithValue("table", table);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) columns.Add(reader.GetString(0));
        }

        if (columns.Contains("AccountId")) return " where t.\"AccountId\"=@account";
        if (table == "Accounts") return " where t.\"Id\"=@account";
        if (table == "States") return " where t.\"Id\" in (select \"StateId\" from \"Counties\" where \"AccountId\"=@account)";
        if (columns.Contains("CaveId")) return " where t.\"CaveId\" in (select \"Id\" from \"Caves\" where \"AccountId\"=@account)";
        if (columns.Contains("EntranceId")) return " where t.\"EntranceId\" in (select e.\"Id\" from \"Entrances\" e join \"Caves\" c on c.\"Id\"=e.\"CaveId\" where c.\"AccountId\"=@account)";
        return null;
    }
}
