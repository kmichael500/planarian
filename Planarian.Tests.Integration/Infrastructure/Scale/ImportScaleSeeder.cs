using Npgsql;

namespace Planarian.Tests;

/// <summary>Dedicated set-up path for the supported workload; not importer logic or measured execution.</summary>
internal static class ImportScaleSeeder
{
    public static async Task SeedAccountAsync(PostgresTestDatabase database, string accountId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn")
            values('benchstate','Tennessee','TN',now());
            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
            values(@account,'10k Benchmark','-',false,true,now());
            """;
        command.Parameters.AddWithValue("account", accountId);
        await command.ExecuteNonQueryAsync();
    }
}
