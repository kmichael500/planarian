using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Planarian.Tests;

public sealed class ImportScaleBenchmarkTests(PostgresTestServer fixture, ITestOutputHelper output)
    : IClassFixture<PostgresTestServer>
{
    private const int CaveCount = 10_000;

    [Fact]
    public async Task TenThousandCaveAndEntranceImportSyncWorkloadIsBoundedAndSemanticallyCorrect()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenThousandCaveAndEntranceImportSyncWorkloadIsBoundedAndSemanticallyCorrect));
        const string accountId = "benchacct1";
        await ImportScaleSeeder.SeedAccountAsync(database, accountId);
        var runner = new ImportScaleRunner(database, accountId);
        var total = Stopwatch.StartNew();
        var memoryBefore = GC.GetTotalMemory(forceFullCollection: true);

        var initial = await runner.RunCavesAsync(
            "initial",
            ImportScaleDataFactory.BuildCaves(Enumerable.Range(1, CaveCount), _ => false),
            syncExisting: false);
        Assert.Equal(CaveCount, initial.Inserts);
        AssertCaveStructure(initial, 600, 400, 550, 50, 40, 40_000);

        var smallLoadCommands = await runner.CountCaveStateLoadCommandsAsync(
            ImportScaleDataFactory.BuildCaves(Enumerable.Range(1, 100), _ => false));
        var fullLoadCommands = await runner.CountCaveStateLoadCommandsAsync(
            ImportScaleDataFactory.BuildCaves(Enumerable.Range(1, CaveCount), _ => false));
        Assert.InRange(fullLoadCommands, smallLoadCommands, smallLoadCommands + 1);

        var entrances = await runner.RunEntrancesAsync(
            "entrances",
            ImportScaleDataFactory.BuildEntrances(Enumerable.Range(1, CaveCount), replacement: false),
            syncExisting: false);
        Assert.Equal(13_200, entrances.Rows);
        AssertEntranceStructure(entrances, 250, 200, 200, 50, 40, 50_000);

        var before = await ReadVersionsAsync(database, accountId);
        var revisionsBefore = await CountRevisionsAsync(database, accountId);
        var mostly = await runner.RunCavesAsync(
            "mostly",
            ImportScaleDataFactory.BuildCaves(Enumerable.Range(1, CaveCount), number => number % 20 == 0),
            syncExisting: true);
        Assert.Equal(500, mostly.Updates);
        Assert.Equal(9500, mostly.NoChange);
        AssertCaveStructure(mostly, 250, 150, 180, 20, 40, 40_000);
        await AssertOnlyExpectedCavesChangedAsync(database, accountId, before, revisionsBefore);

        var churnNumbers = Enumerable.Range(101, 9900).Concat(Enumerable.Range(10001, 100));
        var churn = await runner.RunCavesAsync(
            "churn",
            ImportScaleDataFactory.BuildCaves(churnNumbers, number => number % 10 == 0, churn: true),
            syncExisting: true);
        Assert.Equal(100, churn.Deletes);
        Assert.Equal(100, churn.Inserts);
        AssertCaveStructure(churn, 300, 200, 220, 25, 50, 40_000);

        var replacementNumbers = Enumerable.Range(101, 900).Concat(Enumerable.Range(10001, 100)).ToList();
        var entranceChurn = await runner.RunEntrancesAsync(
            "entrance-churn",
            ImportScaleDataFactory.BuildEntrances(replacementNumbers, replacement: true),
            syncExisting: true);
        Assert.Equal(1000, entranceChurn.Targets);
        AssertEntranceStructure(entranceChurn, 250, 150, 180, 25, 40, 30_000);
        await AssertChurnStateAsync(database, accountId, replacementNumbers);

        total.Stop();
        var process = Process.GetCurrentProcess();
        process.Refresh();
        output.WriteLine("10K_IMPORT_BENCHMARK " + System.Text.Json.JsonSerializer.Serialize(new
        {
            initial,
            entrances,
            mostly,
            churn,
            entranceChurn,
            totalMs = total.Elapsed.TotalMilliseconds,
            managedMemoryDeltaBytes = GC.GetTotalMemory(false) - memoryBefore,
            peakWorkingSetBytes = process.PeakWorkingSet64 > 0 ? process.PeakWorkingSet64 : (long?)null
        }));
    }

    private static async Task<Dictionary<int, (uint Version, string? Revision)>> ReadVersionsAsync(
        PostgresTestDatabase database, string accountId)
    {
        await using var db = database.CreateDbContext("bench", accountId);
        Assert.Equal(CaveCount, await db.Caves.IgnoreQueryFilters().CountAsync(cave => cave.AccountId == accountId));
        return await db.Caves.IgnoreQueryFilters().Where(cave => cave.AccountId == accountId).AsNoTracking()
            .ToDictionaryAsync(cave => cave.CountyNumber,
                cave => new ValueTuple<uint, string?>(cave.Version, cave.CurrentRevisionId));
    }

    private static async Task<int> CountRevisionsAsync(PostgresTestDatabase database, string accountId)
    {
        await using var db = database.CreateDbContext("bench", accountId);
        return await db.CaveRevisions.CountAsync();
    }

    private static async Task AssertOnlyExpectedCavesChangedAsync(PostgresTestDatabase database, string accountId,
        IReadOnlyDictionary<int, (uint Version, string? Revision)> before, int revisionsBefore)
    {
        await using var db = database.CreateDbContext("bench", accountId);
        Dictionary<int, (uint Version, string? Revision)> after = await db.Caves.IgnoreQueryFilters()
            .Where(cave => cave.AccountId == accountId).AsNoTracking()
            .ToDictionaryAsync(cave => cave.CountyNumber,
                cave => new ValueTuple<uint, string?>(cave.Version, cave.CurrentRevisionId));

        foreach (var pair in before.Where(pair => pair.Key % 20 != 0))
        {
            Assert.Equal(pair.Value.Version, after[pair.Key].Version);
            Assert.Equal(pair.Value.Revision, after[pair.Key].Revision);
        }

        var changed = before.Where(pair => pair.Key % 20 == 0).ToList();
        Assert.Equal(500, changed.Count);
        foreach (var pair in changed)
        {
            Assert.NotEqual(pair.Value.Version, after[pair.Key].Version);
            Assert.NotEqual(pair.Value.Revision, after[pair.Key].Revision);
            Assert.NotNull(after[pair.Key].Revision);
        }
        Assert.Equal(500, await db.CaveRevisions.CountAsync() - revisionsBefore);
    }

    private static async Task AssertChurnStateAsync(PostgresTestDatabase database, string accountId,
        IReadOnlyCollection<int> replacementNumbers)
    {
        await using var db = database.CreateDbContext("bench", accountId);
        Assert.Equal(CaveCount, await db.Caves.IgnoreQueryFilters().CountAsync(cave => cave.AccountId == accountId));
        var caveIds = await db.Caves.IgnoreQueryFilters()
            .Where(cave => cave.AccountId == accountId && replacementNumbers.Contains(cave.CountyNumber))
            .Select(cave => cave.Id).ToListAsync();
        Assert.Equal(1000, await db.Entrances.IgnoreQueryFilters().CountAsync(entrance => caveIds.Contains(entrance.CaveId)));
        Assert.Equal(1000, await db.Entrances.IgnoreQueryFilters()
            .CountAsync(entrance => caveIds.Contains(entrance.CaveId) && entrance.IsPrimary));
    }

    private static void AssertCaveStructure(CaveScaleMetrics metrics, int maxCommands, int maxSelects,
        int maxWrites, int maxRevisionWrites, int maxSaves, int maxTracked)
    {
        Assert.InRange(metrics.Commands, 1, maxCommands);
        Assert.InRange(metrics.Selects, 1, maxSelects);
        Assert.InRange(metrics.WriteCommands, 1, maxWrites);
        Assert.InRange(metrics.RevisionWriteCommands, 1, maxRevisionWrites);
        Assert.InRange(metrics.SaveChanges, 1, maxSaves);
        Assert.InRange(metrics.TrackedHighWater, 1, maxTracked);
        Assert.True(metrics.Commands < Math.Max(10, metrics.Rows / 10),
            $"Command count {metrics.Commands} indicates per-record growth for {metrics.Rows} Caves.");
    }

    private static void AssertEntranceStructure(EntranceScaleMetrics metrics, int maxCommands, int maxSelects,
        int maxWrites, int maxRevisionWrites, int maxSaves, int maxTracked)
    {
        Assert.InRange(metrics.Commands, 1, maxCommands);
        Assert.InRange(metrics.Selects, 1, maxSelects);
        Assert.InRange(metrics.WriteCommands, 1, maxWrites);
        Assert.InRange(metrics.RevisionWriteCommands, 1, maxRevisionWrites);
        Assert.InRange(metrics.SaveChanges, 1, maxSaves);
        Assert.InRange(metrics.TrackedHighWater, 1, maxTracked);
        Assert.True(metrics.Commands < Math.Max(10, metrics.Rows / 10),
            $"Command count {metrics.Commands} indicates per-record growth for {metrics.Rows} Entrances.");
    }
}
