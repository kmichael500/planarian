using System.Diagnostics;
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
public sealed class PostgresTestServer : IAsyncLifetime
{
    private static readonly string LatestSchemaTemplateDatabaseName = string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PLANARIAN_TEST_POSTGRES_CONNECTION_STRING"))
        ? "planarian_test_template"
        : $"planarian_test_template_{Guid.NewGuid():N}"[..40];
    private static readonly SemaphoreSlim SharedInitializationGate = new(1, 1);
    private static readonly SemaphoreSlim DatabaseProvisioningGate = new(1, 1);
    private static PostgreSqlContainer? _sharedContainer;
    private static readonly string? ExternalConnectionString = Environment.GetEnvironmentVariable("PLANARIAN_TEST_POSTGRES_CONNECTION_STRING");
    private static bool _latestSchemaTemplateCreated;
    private static TimeSpan _templateCreationDuration;

    public string ConnectionString => !string.IsNullOrWhiteSpace(ExternalConnectionString)
        ? ExternalConnectionString
        : _sharedContainer?.GetConnectionString()
          ?? throw new InvalidOperationException("PostgreSQL integration fixture has not been initialized.");

    public TimeSpan TemplateCreationDuration => _latestSchemaTemplateCreated
        ? _templateCreationDuration
        : throw new InvalidOperationException("Latest-schema template has not been created.");

    public async Task InitializeAsync() => await EnsureSharedContainerAsync();

    // The Testcontainers resource reaper owns process-level cleanup. Disposing a
    // class fixture must not stop the server because other xUnit class fixtures
    // in the same process intentionally share it.
    public Task DisposeAsync() => Task.CompletedTask;

    public PlanarianDbContext CreateDbContext(string userId, string? accountId) =>
        CreateDbContext(ConnectionString, userId, accountId);

    public Task<PostgresTestDatabase> CreateDatabaseAsync(string testName) =>
        CreateDatabaseCoreAsync(testName, LatestSchemaTemplateDatabaseName);

    public Task<PostgresTestDatabase> CreateUnmigratedDatabaseAsync(string testName) =>
        CreateDatabaseCoreAsync(testName, "template0");

    private async Task<PostgresTestDatabase> CreateDatabaseCoreAsync(string testName, string templateDatabaseName)
    {
        await EnsureSharedContainerAsync();

        var prefix = SanitizeDatabaseName(testName);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        var maxPrefixLength = Math.Max(1, 63 - "planarian__".Length - suffix.Length);
        if (prefix.Length > maxPrefixLength) prefix = prefix[..maxPrefixLength];
        var databaseName = $"planarian_{prefix}_{suffix}";

        var provisioning = Stopwatch.StartNew();
        await DatabaseProvisioningGate.WaitAsync();
        try
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"CREATE DATABASE {QuoteIdentifier(databaseName)} TEMPLATE {QuoteIdentifier(templateDatabaseName)}";
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            DatabaseProvisioningGate.Release();
        }
        provisioning.Stop();

        var builder = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = databaseName };
        return new PostgresTestDatabase(builder.ConnectionString, databaseName, ConnectionString,
            provisioning.Elapsed);
    }

    private static async Task EnsureSharedContainerAsync()
    {
        await SharedInitializationGate.WaitAsync();
        try
        {
            if (_sharedContainer is null && string.IsNullOrWhiteSpace(ExternalConnectionString))
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

            if (!_latestSchemaTemplateCreated)
            {
                var stopwatch = Stopwatch.StartNew();
                var adminConnectionString = ExternalConnectionString ?? _sharedContainer!.GetConnectionString();
                await CreateLatestSchemaTemplateAsync(adminConnectionString);
                stopwatch.Stop();
                _templateCreationDuration = stopwatch.Elapsed;
                _latestSchemaTemplateCreated = true;
            }
        }
        finally
        {
            SharedInitializationGate.Release();
        }
    }

    private static async Task CreateLatestSchemaTemplateAsync(string adminConnectionString)
    {
        await CreateTemplateDatabaseAsync(adminConnectionString, LatestSchemaTemplateDatabaseName,
            async templateConnectionString =>
            {
                await using (var db = CreateDbContext(templateConnectionString, "fixture-user", null))
                {
                    await db.Database.MigrateAsync();
                    var pending = await db.Database.GetPendingMigrationsAsync();
                    if (pending.Any())
                        throw new InvalidOperationException(
                            $"Latest-schema test template still has pending migrations: {string.Join(", ", pending)}");
                }

                using (var templateConnection = new NpgsqlConnection(templateConnectionString))
                    NpgsqlConnection.ClearPool(templateConnection);

                await using var sealConnection = new NpgsqlConnection(adminConnectionString);
                await sealConnection.OpenAsync();
                await TerminateDatabaseConnectionsAsync(sealConnection, LatestSchemaTemplateDatabaseName);

                await using var seal = sealConnection.CreateCommand();
                seal.CommandText =
                    $"ALTER DATABASE {QuoteIdentifier(LatestSchemaTemplateDatabaseName)} WITH IS_TEMPLATE true ALLOW_CONNECTIONS false";
                await seal.ExecuteNonQueryAsync();
            });
    }

    internal static async Task CreateTemplateDatabaseAsync(string adminConnectionString, string databaseName,
        Func<string, Task> initializeAsync)
    {
        var databaseCreated = false;
        try
        {
            await using (var admin = new NpgsqlConnection(adminConnectionString))
            {
                await admin.OpenAsync();
                await using var create = admin.CreateCommand();
                create.CommandText = $"CREATE DATABASE {QuoteIdentifier(databaseName)} TEMPLATE template0";
                await create.ExecuteNonQueryAsync();
                databaseCreated = true;
            }

            var templateConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = databaseName
            }.ConnectionString;
            await initializeAsync(templateConnectionString);
        }
        catch
        {
            if (databaseCreated)
                await DropTemplateDatabaseBestEffortAsync(adminConnectionString, databaseName);
            throw;
        }
    }

    private static async Task DropTemplateDatabaseBestEffortAsync(string adminConnectionString, string databaseName)
    {
        try
        {
            using (var templateConnection = new NpgsqlConnection(
                       new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }
                           .ConnectionString))
                NpgsqlConnection.ClearPool(templateConnection);
        }
        catch
        {
            // Continue with server-side cleanup even if local pool cleanup fails.
        }

        try
        {
            await using var admin = new NpgsqlConnection(adminConnectionString);
            await admin.OpenAsync();
            try
            {
                await TerminateDatabaseConnectionsAsync(admin, databaseName);
            }
            catch
            {
                // DROP may still succeed when termination itself fails or no sessions remain.
            }

            await using var drop = admin.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS {QuoteIdentifier(databaseName)}";
            await drop.ExecuteNonQueryAsync();
        }
        catch
        {
            // Cleanup must never replace the template initialization exception.
        }
    }

    private static async Task TerminateDatabaseConnectionsAsync(NpgsqlConnection admin, string databaseName)
    {
        await using var terminate = admin.CreateCommand();
        terminate.CommandText = """
            select pg_terminate_backend(pid)
            from pg_stat_activity
            where datname = @database and pid <> pg_backend_pid()
            """;
        terminate.Parameters.AddWithValue("database", databaseName);
        await terminate.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Docker contexts are not consumed by Testcontainers. Colima exposes its
    /// host socket below the macOS user profile, while containers inside its VM
    /// reach Docker through /var/run/docker.sock. Configure each supported
    /// endpoint only when the caller did not provide one. Resource-reaper policy
    /// remains owned by Testcontainers or an explicit developer environment setting.
    /// </summary>
    private static void ConfigureColimaForTestcontainers()
    {
        if (!OperatingSystem.IsMacOS()) return;

        var socket = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".colima", "default", "docker.sock");
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (string.IsNullOrWhiteSpace(dockerHost) && File.Exists(socket))
        {
            dockerHost = $"unix://{socket}";
            Environment.SetEnvironmentVariable("DOCKER_HOST", dockerHost);
        }

        if (string.Equals(dockerHost, $"unix://{socket}", StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE")))
            Environment.SetEnvironmentVariable("TESTCONTAINERS_DOCKER_SOCKET_OVERRIDE", "/var/run/docker.sock");
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

    internal PostgresTestDatabase(string connectionString, string databaseName, string adminConnectionString,
        TimeSpan provisioningDuration)
    {
        ConnectionString = connectionString;
        _databaseName = databaseName;
        _adminConnectionString = adminConnectionString;
        ProvisioningDuration = provisioningDuration;
    }

    public string ConnectionString { get; }
    public TimeSpan ProvisioningDuration { get; }

    public PlanarianDbContext CreateDbContext(string userId, string? accountId)
    {
        var persistedUserId = accountId is null ? userId : EnsurePersistedTestUser(userId);
        return PostgresTestServer.CreateDbContext(ConnectionString, persistedUserId, accountId);
    }

    public PlanarianDbContext CreateDbContext(string userId, string? accountId, params IInterceptor[] interceptors)
    {
        var persistedUserId = accountId is null ? userId : EnsurePersistedTestUser(userId);
        return PostgresTestServer.CreateDbContext(ConnectionString, persistedUserId, accountId, interceptors);
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
