using Npgsql;

namespace Planarian.Tests;

/// <summary>
/// Strict observation helpers for the current-main v32 preservation gate.
/// Unlike the import-state helper, this census treats an unclassified or misspelled table
/// as a test failure instead of silently skipping it.
/// </summary>
internal static class CurrentMainV32DatabaseCensus
{
    internal static readonly string[] BaselineTables =
    [
        "Accounts",
        "AccountStates",
        "AccountUsers",
        "ArcheologyTags",
        "BiologyTags",
        "CartographerNameTags",
        "CaveGeoJsons",
        "CaveOtherTags",
        "CavePermission",
        "CaveReportedByNameTags",
        "Caves",
        "Counties",
        "EntranceHydrologyTags",
        "EntranceOtherTag",
        "EntranceReportedByNameTags",
        "Entrances",
        "EntranceStatusTags",
        "Favorites",
        "FeatureSettings",
        "FieldIndicationTags",
        "Files",
        "GeologicAgeTags",
        "GeologyTags",
        "Leads",
        "LeadTags",
        "MapStatusTags",
        "Members",
        "MessageLogEvents",
        "MessageLogs",
        "MessageTypes",
        "Permissions",
        "Photos",
        "PhysiographicProvinceTags",
        "Projects",
        "States",
        "TagTypes",
        "ThrottleEventLogs",
        "Trips",
        "TripTags",
        "UserPermissions",
        "Users"
    ];

    internal static readonly string[] TransformedBaselineTables =
        ["Caves", "Entrances", "FeatureSettings", "Files"];

    internal static readonly string[] CompletionOwnedTables =
    [
        "StorageCleanupTargets",
        "CaveChangeRequestReviewEvents",
        "CaveChangeRequestStagedFiles",
        "CaveChangeRequests",
        "CaveImportBatches",
        "CaveProposalVersions",
        "CaveRevisions"
    ];

    internal static readonly string[] MigrationOwnedTables =
        ["__BackfillEntranceOtherFeatureSettingRows", "__CaveRevisionsCompletionLegacyFileNames"];

    internal static string[] ExactBaselineTables =>
        BaselineTables.Except(TransformedBaselineTables, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    // Deliberately strict: a future migration that adds or removes an application table must classify it here
    // rather than silently falling outside the preservation census.
    internal static string[] ExpectedLatestTables =>
        BaselineTables.Concat(CompletionOwnedTables).Concat(MigrationOwnedTables)
            .Order(StringComparer.Ordinal)
            .ToArray();

    internal static async Task<IReadOnlyList<string>> GetPublicApplicationTablesAsync(
        PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select table_name
            from information_schema.tables
            where table_schema='public'
              and table_type='BASE TABLE'
              and table_name not in ('__EFMigrationsHistory', 'spatial_ref_sys')
            order by table_name collate "C"
            """, connection);

        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            tables.Add(reader.GetString(0));
        return tables;
    }

    internal static async Task<IReadOnlyDictionary<string, long>> CaptureTableCountsAsync(
        PostgresTestDatabase database, IEnumerable<string> tables)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in tables.Order(StringComparer.Ordinal))
        {
            await using var command = new NpgsqlCommand(
                $"select count(*) from {QuoteIdentifier(table)}", connection);
            result[table] = (long)(await command.ExecuteScalarAsync())!;
        }

        return result;
    }

    internal static async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> CaptureExactTablesAsync(
        PostgresTestDatabase database, IEnumerable<string> tables)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var table in tables.Order(StringComparer.Ordinal))
            result[table] = await CaptureRowsAsync(connection, table,
                "to_jsonb(t) || jsonb_build_object('__xmin', t.xmin::text)");
        return result;
    }

    internal static Task<IReadOnlyList<string>> CaptureCaveMigrationRowsAsync(PostgresTestDatabase database) =>
        CaptureRowsAsync(database, "Caves",
            "(to_jsonb(t) - 'ReportedByUserId' - 'CurrentRevisionId') || jsonb_build_object('__xmin', t.xmin::text)");

    internal static Task<IReadOnlyList<string>> CaptureEntranceMigrationRowsAsync(PostgresTestDatabase database) =>
        CaptureRowsAsync(database, "Entrances",
            "(to_jsonb(t) - 'ReportedByUserId') || jsonb_build_object('__xmin', t.xmin::text)");

    internal static Task<IReadOnlyList<string>> CaptureFileStableRowsAsync(PostgresTestDatabase database) =>
        CaptureRowsAsync(database, "Files",
            "to_jsonb(t) - 'FileName' - 'DisplayName' - 'Name' - 'Extension' - 'AccountId'");

    internal static Task<IReadOnlyList<string>> CaptureCaveRolloutRowsAsync(PostgresTestDatabase database) =>
        CaptureRowsAsync(database, "Caves", "to_jsonb(t) - 'CurrentRevisionId'");

    internal static Task<IReadOnlyList<string>> CaptureUserCavePermissionsAsync(PostgresTestDatabase database) =>
        CaptureRowsAsync(database, "UserCavePermissions", "to_jsonb(t)");

    internal static async Task<IReadOnlyList<string>> CaptureSeededFeatureSettingsAsync(
        PostgresTestDatabase database)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select (to_jsonb(t) || jsonb_build_object('__xmin', t.xmin::text))::text as row
            from "FeatureSettings" t
            where t."Id" = any(@ids)
            order by row
            """, connection);
        command.Parameters.AddWithValue("ids", CurrentMainV32CensusSeeder.SeededFeatureSettingIds);

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0));
        return rows;
    }

    private static async Task<IReadOnlyList<string>> CaptureRowsAsync(
        PostgresTestDatabase database, string relation, string expression)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        return await CaptureRowsAsync(connection, relation, expression);
    }

    private static async Task<IReadOnlyList<string>> CaptureRowsAsync(
        NpgsqlConnection connection, string relation, string expression)
    {
        await using var command = new NpgsqlCommand(
            $"select ({expression})::text as row from {QuoteIdentifier(relation)} t order by row",
            connection);

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            rows.Add(reader.GetString(0));
        return rows;
    }

    private static string QuoteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
