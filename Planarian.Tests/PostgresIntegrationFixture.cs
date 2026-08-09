using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Testcontainers.PostgreSql;
using Xunit;

namespace Planarian.Tests;

/// <summary>
/// Real PostgreSQL/PostGIS fixture. All fixture instances in the test process
/// share one Testcontainers server; individual tests create isolated databases.
/// Docker is required and tests never silently opt out.
/// </summary>
public sealed class PostgresIntegrationFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim SharedGate = new(1, 1);
    private static PostgreSqlContainer? _sharedContainer;
    private static bool _sharedDatabaseMigrated;

    public string ConnectionString => _sharedContainer?.GetConnectionString()
        ?? throw new InvalidOperationException("PostgreSQL integration fixture has not been initialized.");

    public async Task InitializeAsync() => await EnsureSharedContainerAsync();

    // The Testcontainers resource reaper owns process-level cleanup. Disposing a
    // class fixture must not stop the server because other xUnit class fixtures
    // in the same process intentionally share it.
    public Task DisposeAsync() => Task.CompletedTask;

    public PlanarianDbContext CreateDbContext(string userId, string? accountId) =>
        CreateDbContext(ConnectionString, userId, accountId);

    public Task<PostgresTestDatabase> CreateDatabaseAsync(string testName) =>
        CreateDatabaseCoreAsync(testName, migrateToLatest: true);

    public Task<PostgresTestDatabase> CreateUnmigratedDatabaseAsync(string testName) =>
        CreateDatabaseCoreAsync(testName, migrateToLatest: false);

    private async Task<PostgresTestDatabase> CreateDatabaseCoreAsync(string testName, bool migrateToLatest)
    {
        await EnsureSharedContainerAsync();

        var prefix = SanitizeDatabaseName(testName);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var maxPrefixLength = Math.Max(1, 63 - "planarian__".Length - suffix.Length);
        if (prefix.Length > maxPrefixLength) prefix = prefix[..maxPrefixLength];
        var databaseName = $"planarian_{prefix}_{suffix}";

        await using (var connection = new NpgsqlConnection(ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName)}";
            await command.ExecuteNonQueryAsync();
        }

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName };
        var database = new PostgresTestDatabase(builder.ConnectionString, databaseName, ConnectionString);
        try
        {
            if (migrateToLatest) await database.MigrateAsync(null);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    private static async Task EnsureSharedContainerAsync()
    {
        await SharedGate.WaitAsync();
        try
        {
            if (_sharedContainer is null)
            {
                ConfigureColimaForTestcontainers();
                var container = new PostgreSqlBuilder()
                    .WithImage("postgis/postgis:16-3.4")
                    .WithDatabase("planarian_tests")
                    .WithUsername("postgres")
                    .WithPassword("postgres")
                    .Build();
                await container.StartAsync();
                _sharedContainer = container;
            }

            if (!_sharedDatabaseMigrated)
            {
                await using var db = CreateDbContext(_sharedContainer.GetConnectionString(), "fixture-user", null);
                await db.Database.MigrateAsync();
                _sharedDatabaseMigrated = true;
            }
        }
        finally
        {
            SharedGate.Release();
        }
    }

    /// <summary>
    /// Docker contexts are not consumed by Testcontainers. Colima exposes its
    /// socket below the macOS user profile, and its VM cannot bind-mount that
    /// host socket into Ryuk. Configure both only when the caller did not
    /// provide an explicit Docker endpoint.
    /// </summary>
    private static void ConfigureColimaForTestcontainers()
    {
        if (!OperatingSystem.IsMacOS() || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOCKER_HOST")))
            return;

        var socket = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".colima", "default", "docker.sock");
        if (!Directory.Exists(Path.GetDirectoryName(socket))) return;

        Environment.SetEnvironmentVariable("DOCKER_HOST", $"unix://{socket}");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED")))
            Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
    }

    internal static PlanarianDbContext CreateDbContext(string connectionString, string userId, string? accountId,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(connectionString, options =>
            {
                options.MigrationsAssembly("Planarian.Migrations");
                options.UseNetTopologySuite();
                options.MaxBatchSize(1000);
            })
            .AddInterceptors(interceptors)
            .Options;
        var db = new PlanarianDbContext(options);
        db.RequestUser = new RequestUser(db)
        {
            Id = userId,
            AccountId = accountId,
            FirstName = "Test",
            LastName = "User"
        };
        return db;
    }

    private static string SanitizeDatabaseName(string value)
    {
        var sanitized = new string(value.ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')
            .ToArray()).Trim('_');
        return string.IsNullOrWhiteSpace(sanitized) ? "test" : sanitized;
    }

    private static string QuoteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public sealed class PostgresTestDatabase : IAsyncDisposable
{
    private readonly string _databaseName;
    private readonly string _adminConnectionString;
    private int _disposed;

    internal PostgresTestDatabase(string connectionString, string databaseName, string adminConnectionString)
    {
        ConnectionString = connectionString;
        _databaseName = databaseName;
        _adminConnectionString = adminConnectionString;
    }

    public string ConnectionString { get; }

    public PlanarianDbContext CreateDbContext(string userId, string? accountId)
    {
        var persistedUserId = accountId is null ? userId : EnsurePersistedTestUser(userId);
        return PostgresIntegrationFixture.CreateDbContext(ConnectionString, persistedUserId, accountId);
    }

    public PlanarianDbContext CreateDbContext(string userId, string? accountId, params IInterceptor[] interceptors)
    {
        var persistedUserId = accountId is null ? userId : EnsurePersistedTestUser(userId);
        return PostgresIntegrationFixture.CreateDbContext(ConnectionString, persistedUserId, accountId, interceptors);
    }

    public IReadOnlyList<string> GetMigrationNames()
    {
        using var db = CreateDbContext("migration-list-user", null);
        return db.Database.GetMigrations().ToList();
    }

    public async Task MigrateAsync(string? targetMigration)
    {
        await using var db = CreateDbContext("migration-user", null);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(targetMigration);
    }

    private string EnsurePersistedTestUser(string requestedUserId)
    {
        var userId = NormalizeUserId(requestedUserId);
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "Users" ("Id", "FirstName", "LastName", "EmailAddress", "IsTemporary", "CreatedOn")
            values (@id, 'Integration', 'User', @email, false, now())
            on conflict ("Id") do nothing
            """;
        command.Parameters.AddWithValue("id", userId);
        command.Parameters.AddWithValue("email", $"{userId}@integration.test");
        command.ExecuteNonQuery();
        return userId;
    }

    private static string NormalizeUserId(string requestedUserId)
    {
        if (!string.IsNullOrWhiteSpace(requestedUserId) && requestedUserId.Length <= 10)
            return requestedUserId;

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(requestedUserId ?? string.Empty));
        return $"t{Convert.ToHexString(bytes).ToLowerInvariant()[..9]}";
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        // Only invalidate connections targeting this test's temporary database.
        // Clearing all Npgsql pools here can interfere with other test classes
        // that deliberately share the same process-level PostGIS container.
        using (var databaseConnection = new NpgsqlConnection(ConnectionString))
            NpgsqlConnection.ClearPool(databaseConnection);

        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName.Replace("\"", "\"\"")}\" WITH (FORCE)";
        await command.ExecuteNonQueryAsync();
    }
}
