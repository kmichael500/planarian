using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Planarian.Tests;

public sealed class PostgresTemplateDatabaseIntegrationTests(
    PostgresTestServer fixture,
    ITestOutputHelper output) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task LatestSchemaCloneHasCurrentPostgresSchemaWithoutDomainData()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LatestSchemaCloneHasCurrentPostgresSchemaWithoutDomainData));
        await using var db = database.CreateDbContext("template-guarantee", accountId: null);

        var knownMigrations = db.Database.GetMigrations().ToList();
        var appliedMigrations = (await db.Database.GetAppliedMigrationsAsync()).ToList();
        Assert.NotEmpty(knownMigrations);
        Assert.Equal(knownMigrations, appliedMigrations);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select exists(select 1 from information_schema.tables where table_name = 'Caves'),
                   exists(select 1 from information_schema.tables where table_name = 'CaveRevisions'),
                   postgis_version(),
                   (select count(*) from "Accounts"),
                   (select count(*) from "Users"),
                   (select count(*) from "States"),
                   (select count(*) from "Caves")
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.True(reader.GetBoolean(1));
        Assert.False(string.IsNullOrWhiteSpace(reader.GetString(2)));
        Assert.Equal(0, reader.GetInt64(3));
        Assert.Equal(0, reader.GetInt64(4));
        Assert.Equal(0, reader.GetInt64(5));
        Assert.Equal(0, reader.GetInt64(6));

        await using var admin = new NpgsqlConnection(fixture.ConnectionString);
        await admin.OpenAsync();
        await using var templatePolicy = new NpgsqlCommand("""
            select datistemplate, datallowconn
            from pg_database
            where datname = @template_database
            """, admin);
        templatePolicy.Parameters.AddWithValue("template_database", fixture.TemplateDatabaseName);
        await using var templateReader = await templatePolicy.ExecuteReaderAsync();
        Assert.True(await templateReader.ReadAsync());
        Assert.True(templateReader.GetBoolean(0));
        Assert.False(templateReader.GetBoolean(1));

        output.WriteLine("TEMPLATE_CREATION_MS {0:F1}", fixture.TemplateCreationDuration.TotalMilliseconds);
        output.WriteLine("DATABASE_CLONE_MS {0:F1}", database.ProvisioningDuration.TotalMilliseconds);
    }

    [Fact]
    public async Task TwoLatestSchemaClonesKeepTenantDataIsolated()
    {
        await using var databaseA = await fixture.CreateDatabaseAsync(
            nameof(TwoLatestSchemaClonesKeepTenantDataIsolated) + "_a");
        await using var databaseB = await fixture.CreateDatabaseAsync(
            nameof(TwoLatestSchemaClonesKeepTenantDataIsolated) + "_b");

        await CaveTestDataFactory.CreatePublishedCaveAsync(databaseA, 'a');

        Assert.Equal(1, await CountRowsAsync(databaseA, "Accounts"));
        Assert.Equal(1, await CountRowsAsync(databaseA, "Caves"));
        Assert.Equal(0, await CountRowsAsync(databaseB, "Accounts"));
        Assert.Equal(0, await CountRowsAsync(databaseB, "Caves"));
    }

    [Fact]
    public async Task MutatingCloneDoesNotDirtyTemplateOrSubsequentClone()
    {
        await using (var mutated = await fixture.CreateDatabaseAsync(
                         nameof(MutatingCloneDoesNotDirtyTemplateOrSubsequentClone) + "_mutated"))
        {
            await CaveTestDataFactory.CreatePublishedCaveAsync(mutated, 't');
            Assert.Equal(1, await CountRowsAsync(mutated, "Accounts"));
        }

        await using var subsequent = await fixture.CreateDatabaseAsync(
            nameof(MutatingCloneDoesNotDirtyTemplateOrSubsequentClone) + "_subsequent");
        Assert.Equal(0, await CountRowsAsync(subsequent, "Accounts"));
        Assert.Equal(0, await CountRowsAsync(subsequent, "Users"));
        Assert.Equal(0, await CountRowsAsync(subsequent, "States"));
        Assert.Equal(0, await CountRowsAsync(subsequent, "Caves"));
    }

    [Fact]
    public async Task ExternalServerTemplateIsRemovedWhenChildTestProcessExits()
    {
        var externalConnectionString = Environment.GetEnvironmentVariable("PLANARIAN_TEST_POSTGRES_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(externalConnectionString))
            return; // Testcontainers owns process-level cleanup when an external server is not configured.

        var before = await GetTemplateDatabaseNamesAsync(fixture.ConnectionString);
        var assemblyPath = typeof(PostgresTemplateDatabaseIntegrationTests).Assembly.Location;
        var testName =
            $"{typeof(PostgresTemplateDatabaseIntegrationTests).FullName}.{nameof(LatestSchemaCloneHasCurrentPostgresSchemaWithoutDomainData)}";
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("vstest");
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add($"--TestCaseFilter:FullyQualifiedName={testName}");
        startInfo.Environment["PLANARIAN_TEST_POSTGRES_CONNECTION_STRING"] = externalConnectionString;

        using var child = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the PostgreSQL cleanup probe process.");
        var stdout = child.StandardOutput.ReadToEndAsync();
        var stderr = child.StandardError.ReadToEndAsync();
        await child.WaitForExitAsync();
        var output = await stdout;
        var error = await stderr;

        Assert.True(child.ExitCode == 0,
            $"Child PostgreSQL test process failed with exit code {child.ExitCode}.\n{output}\n{error}");
        Assert.Equal(before, await GetTemplateDatabaseNamesAsync(fixture.ConnectionString));
    }

    [Fact]
    public async Task FailedTemplateInitializationPreservesOriginalFailureAndDropsPartialDatabase()
    {
        var databaseName = $"planarian_failed_template_{Guid.NewGuid():N}";
        NpgsqlConnection? partialConnection = null;
        var expected = new InvalidOperationException("deliberate template initialization failure");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            PostgresTestServer.CreateTemplateDatabaseAsync(fixture.ConnectionString, databaseName,
                async connectionString =>
                {
                    partialConnection = new NpgsqlConnection(connectionString);
                    await partialConnection.OpenAsync();
                    throw expected;
                }));

        Assert.Same(expected, actual);
        await using var admin = new NpgsqlConnection(fixture.ConnectionString);
        await admin.OpenAsync();
        await using var exists = new NpgsqlCommand(
            "select exists(select 1 from pg_database where datname = @database)", admin);
        exists.Parameters.AddWithValue("database", databaseName);
        Assert.False((bool)(await exists.ExecuteScalarAsync())!);
        await partialConnection!.DisposeAsync();
    }

    private static async Task<List<string>> GetTemplateDatabaseNamesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "select datname from pg_database where datname like 'planarian_test_template_%' order by datname",
            connection);
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result;
    }

    private static async Task<long> CountRowsAsync(PostgresTestDatabase database, string table)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"select count(*) from \"{table}\"", connection);
        return (long)(await command.ExecuteScalarAsync())!;
    }
}
