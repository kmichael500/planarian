using System.Data.Common;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Data;
using Planarian.Modules.Import.Planning;
using Planarian.Modules.Tags.Repositories;
using Planarian.Tests;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure.Postgres;

public sealed class LockOrderingProviderContractIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task PostgreSqlCOrderingMatchesApplicationOrdinalOrderingForStableIds()
    {
        var ids = new[] { "zaaaa00000", "Aaaaa00000", "0aaaa00000", "aaaaa00000", "Zaaaa00000" };
        var expected = new[] { "0aaaa00000", "Aaaaa00000", "Zaaaa00000", "aaaaa00000", "zaaaa00000" };
        var applicationOrder = ids.Order(StringComparer.Ordinal).ToList();

        Assert.Equal(expected, applicationOrder);

        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PostgreSqlCOrderingMatchesApplicationOrdinalOrderingForStableIds));
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select value
            from unnest(@ids::text[]) as stable_ids(value)
            order by value collate "C"
            """;
        command.Parameters.Add(new NpgsqlParameter<string[]>("ids", ids));

        var postgresOrder = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) postgresOrder.Add(reader.GetString(0));

        Assert.Equal(applicationOrder, postgresOrder);
    }

    [Fact]
    public async Task ProductionMultiRowLocksSpecifyIdOrderingCCollationAndLockMode()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProductionMultiRowLocksSpecifyIdOrderingCCollationAndLockMode));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var firstCave = await CaveTestDataFactory.AddCaveAsync(database, tenant,
            caveId: "0cave00000", countyNumber: 1, name: "First Cave");
        var secondCave = await CaveTestDataFactory.AddCaveAsync(database, tenant,
            caveId: "Acave00000", countyNumber: 2, name: "Second Cave");
        var firstTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Biology, "First lock tag", "0tag000000");
        var secondTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Biology, "Second lock tag", "Atag000000");
        var firstDeleteTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Geology, "First delete tag", "Ztag000000");
        var secondDeleteTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Geology, "Second delete tag", "atag000000");

        await using (var seed = database.CreateDbContext("county-lock-seed", tenant.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = "Acounty000",
                AccountId = tenant.AccountId,
                StateId = tenant.StateId,
                DisplayId = "AA02",
                Name = "Second County"
            });
            await seed.SaveChangesAsync();
        }

        var interceptor = new MultiRowLockOrderingCommandInterceptor();
        await using var db = database.CreateDbContext("lock-contract", tenant.AccountId, interceptor);
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            var tagLocks = new TagReferenceLockRepository(db, db.RequestUser);
            await AssertNextLockAsync(interceptor, "TagTypes", "for key share", async () =>
                await tagLocks.LockForReferenceAsync([secondTag.Id, firstTag.Id]));
            await AssertNextLockAsync(interceptor, "TagTypes", "for update", async () =>
                await tagLocks.LockForDestructiveMutationAsync([secondTag.Id, firstTag.Id]));

            var countyLocks = new CountyReferenceLockRepository(db, db.RequestUser);
            var countyIds = new[] { "Acounty000", tenant.CountyId };
            await AssertNextLockAsync(interceptor, "Counties", "for key share", async () =>
                await countyLocks.LockForReferenceAsync(countyIds));
            await AssertNextLockAsync(interceptor, "Counties", "for update", async () =>
                await countyLocks.LockForMutationAsync(countyIds));
            await transaction.RollbackAsync();
        }

        await AssertNextLockAsync(interceptor, "TagTypes", "for update", async () =>
        {
            Assert.Equal(2, await new TagTypeDeleteExecutionRepository(db, db.RequestUser)
                .ExecuteAsync([secondDeleteTag.Id, firstDeleteTag.Id]));
        });

        var currentCaves = await db.Caves.IgnoreQueryFilters().AsNoTracking().Where(cave =>
                cave.AccountId == tenant.AccountId &&
                (cave.Id == firstCave.CaveId || cave.Id == secondCave.CaveId))
            .ToDictionaryAsync(cave => cave.Id, StringComparer.Ordinal);
        Assert.Equal(2, currentCaves.Count);
        var caveIds = currentCaves.Keys.Order(StringComparer.Ordinal).ToList();
        var rawCommandObserver = new RawNpgsqlLockCommandObserver(database.ConnectionString);
        await using var rawDb = database.CreateDbContext("raw-lock-contract", tenant.AccountId, rawCommandObserver);
        var snapshots = new CavePublishedSnapshotRepository(rawDb, rawDb.RequestUser);
        var revisions = new CaveBulkRevisionRepository(rawDb, rawDb.RequestUser);
        var tagReferenceLocks = new TagReferenceLockRepository(rawDb, rawDb.RequestUser);

        await using var rawCommandTransaction = await rawDb.Database.BeginTransactionAsync();
        var mergeRepository = new TagTypeMergeExecutionRepository(rawDb, rawDb.RequestUser, tagReferenceLocks,
            snapshots, revisions);
        await InvokePrivateLockMethodAsync(mergeRepository, "LockCavesAsync", caveIds, CancellationToken.None);
        var mergeLockCommand = await rawCommandObserver.ReadLastCommandAsync(rawDb);
        MultiRowLockOrderingCommandInterceptor.AssertContract(mergeLockCommand, "for update");

        var caveTargets = currentCaves.Values.ToDictionary(cave => cave.Id, cave => new CaveImportExistingTarget(
            cave.Id, cave.Version, cave.CurrentRevisionId, cave.StateId, cave.CountyId, tenant.CountyDisplayId,
            cave.CountyNumber, cave.Name), StringComparer.Ordinal);
        var cavePlan = new CaveImportPlan(tenant.AccountId, false, [], [], [], [], [],
            new Dictionary<string, string>(StringComparer.Ordinal), caveTargets);
        var caveImportRepository = new CaveImportExecutionRepository(rawDb, rawDb.RequestUser, snapshots, revisions,
            tagReferenceLocks, new CountyReferenceLockRepository(rawDb, rawDb.RequestUser));
        await InvokePrivateLockMethodAsync(caveImportRepository, "LockAndVerifyExistingCavesAsync", cavePlan,
            CancellationToken.None);

        var entranceTargets = currentCaves.Values.ToDictionary(cave => cave.Id,
            cave => new EntranceImportCaveTarget(cave.Id, cave.Name, tenant.CountyDisplayId, cave.CountyNumber,
                cave.Version, cave.CurrentRevisionId, 0, 0), StringComparer.Ordinal);
        var entrancePlan = new EntranceImportPlan(tenant.AccountId, false, [], [],
            new Dictionary<string, string>(StringComparer.Ordinal), entranceTargets);
        var entranceImportRepository = new EntranceImportExecutionRepository(rawDb, rawDb.RequestUser, snapshots,
            revisions, tagReferenceLocks);
        await InvokePrivateLockMethodAsync(entranceImportRepository, "LockAndVerifyTargetsAsync", entrancePlan,
            CancellationToken.None);
        await rawCommandTransaction.RollbackAsync();

        Assert.Equal(2, rawCommandObserver.Commands.Count);
        foreach (var commandText in rawCommandObserver.Commands)
            MultiRowLockOrderingCommandInterceptor.AssertContract(commandText, "for update");
    }

    private static async Task AssertNextLockAsync(MultiRowLockOrderingCommandInterceptor interceptor,
        string tableName, string lockMode, Func<Task> action)
    {
        var commandCount = interceptor.Commands.Count;
        await action();
        var command = Assert.Single(interceptor.Commands.Skip(commandCount));
        Assert.Contains($"from \"{tableName}\"", command, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(lockMode, command, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task InvokePrivateLockMethodAsync(object repository, string methodName,
        object argument, CancellationToken cancellationToken)
    {
        var method = repository.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance);
        var invocation = method?.Invoke(repository, [argument, cancellationToken]);
        await Assert.IsAssignableFrom<Task>(invocation);
    }
}

internal sealed class MultiRowLockOrderingCommandInterceptor : DbCommandInterceptor
{
    private readonly List<string> _commands = [];
    public IReadOnlyList<string> Commands => _commands;

    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Inspect(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Inspect(command.CommandText);
        return ValueTask.FromResult(result);
    }

    private void Inspect(string commandText)
    {
        var lockMode = commandText.Contains("for key share", StringComparison.OrdinalIgnoreCase)
            ? "for key share"
            : commandText.Contains("for update", StringComparison.OrdinalIgnoreCase)
                ? "for update"
                : null;
        if (lockMode is null || !commandText.Contains("\"Id\" = any(", StringComparison.OrdinalIgnoreCase)) return;

        AssertContract(commandText, lockMode);
        _commands.Add(commandText);
    }

    internal static void AssertContract(string commandText, string lockMode)
    {
        Assert.Contains("\"Id\" = any(", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("order by \"Id\"", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("collate \"C\"", commandText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(lockMode, commandText, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class RawNpgsqlLockCommandObserver(string connectionString) : DbCommandInterceptor
{
    private readonly List<string> _commands = [];
    public IReadOnlyList<string> Commands => _commands;

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (command.CommandText.Contains("from \"Caves\"", StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains("\"CurrentRevisionId\"", StringComparison.OrdinalIgnoreCase) &&
            !command.CommandText.Contains("for update", StringComparison.OrdinalIgnoreCase))
            _commands.Add(await ReadLastCommandAsync((NpgsqlConnection)command.Connection!, cancellationToken));

        return result;
    }

    public Task<string> ReadLastCommandAsync(DbContext db, CancellationToken cancellationToken = default) =>
        ReadLastCommandAsync((NpgsqlConnection)db.Database.GetDbConnection(), cancellationToken);

    private async Task<string> ReadLastCommandAsync(NpgsqlConnection targetConnection,
        CancellationToken cancellationToken)
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync(cancellationToken);
        await using var command = observer.CreateCommand();
        command.CommandText = "select query from pg_stat_activity where pid = @pid";
        command.Parameters.Add(new NpgsqlParameter<int>("pid", targetConnection.ProcessID));
        return Assert.IsType<string>(await command.ExecuteScalarAsync(cancellationToken));
    }
}
