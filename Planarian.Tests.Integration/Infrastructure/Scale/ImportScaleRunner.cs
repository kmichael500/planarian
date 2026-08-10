using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Data;
using Planarian.Modules.Import.Parsing;
using Planarian.Modules.Import.Planning;

namespace Planarian.Tests;

internal sealed class ImportScaleRunner(PostgresTestDatabase database, string accountId)
{
    public async Task<CaveScaleMetrics> RunCavesAsync(string phase, string csvText, bool syncExisting)
    {
        var sql = new SqlTimingInterceptor();
        var saves = new SaveMetricsInterceptor();
        await using var db = CreateContext(sql, saves);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(csvText);

        var parse = Stopwatch.StartNew();
        var records = await new CaveImportCsvParser().ParseAsync(csv);
        parse.Stop();

        var load = Stopwatch.StartNew();
        var state = await new CaveImportPlanningRepository(db, db.RequestUser).LoadAsync(records, syncExisting);
        load.Stop();

        var planning = Stopwatch.StartNew();
        var plan = new CaveImportPlanner().Plan(records, state, syncExisting);
        planning.Stop();

        sql.Reset();
        saves.Reset();
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);
        var execution = Stopwatch.StartNew();
        await new CaveImportExecutionRepository(db, db.RequestUser, snapshots,
            new CaveImportRevisionRepository(db, db.RequestUser)).ExecuteAsync(plan, phase + ".csv");
        execution.Stop();

        var writes = sql.Items.Where(item => IsWrite(item.Sql)).ToList();
        return new CaveScaleMetrics(
            phase, records.Count, parse.Elapsed.TotalMilliseconds, load.Elapsed.TotalMilliseconds,
            planning.Elapsed.TotalMilliseconds, execution.Elapsed.TotalMilliseconds,
            plan.Caves.Count(cave => cave.Action == CaveImportAction.Insert),
            plan.Caves.Count(cave => cave.Action == CaveImportAction.Update),
            plan.Caves.Count(cave => cave.Action == CaveImportAction.NoChange),
            plan.Deletions.Count, sql.Items.Count,
            sql.Items.Count(item => item.Sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase)),
            writes.Count, writes.Count(item => item.Sql.Contains("\"CaveRevisions\"", StringComparison.Ordinal)),
            writes.Sum(item => item.Duration.TotalMilliseconds),
            writes.Where(item => item.Sql.Contains("\"CaveRevisions\"", StringComparison.Ordinal))
                .Sum(item => item.Duration.TotalMilliseconds),
            saves.Saves, saves.TrackedHighWater);
    }

    public async Task<EntranceScaleMetrics> RunEntrancesAsync(string phase, string csvText, bool syncExisting)
    {
        var sql = new SqlTimingInterceptor();
        var saves = new SaveMetricsInterceptor();
        await using var db = CreateContext(sql, saves);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(csvText);

        var parse = Stopwatch.StartNew();
        var records = await new EntranceImportCsvParser().ParseAsync(csv);
        parse.Stop();

        var load = Stopwatch.StartNew();
        var state = await new EntranceImportPlanningRepository(db, db.RequestUser).LoadAsync(records);
        load.Stop();

        var planning = Stopwatch.StartNew();
        var plan = new EntranceImportPlanner().Plan(records, state, syncExisting);
        planning.Stop();

        sql.Reset();
        saves.Reset();
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);
        var execution = Stopwatch.StartNew();
        await new EntranceImportExecutionRepository(db, db.RequestUser, snapshots,
            new CaveImportRevisionRepository(db, db.RequestUser)).ExecuteAsync(plan, phase + ".csv");
        execution.Stop();

        var writes = sql.Items.Where(item => IsWrite(item.Sql)).ToList();
        return new EntranceScaleMetrics(
            phase, records.Count, plan.Targets.Count, parse.Elapsed.TotalMilliseconds, load.Elapsed.TotalMilliseconds,
            planning.Elapsed.TotalMilliseconds, execution.Elapsed.TotalMilliseconds, sql.Items.Count,
            sql.Items.Count(item => item.Sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase)),
            writes.Count, writes.Count(item => item.Sql.Contains("\"CaveRevisions\"", StringComparison.Ordinal)),
            writes.Sum(item => item.Duration.TotalMilliseconds),
            writes.Where(item => item.Sql.Contains("\"CaveRevisions\"", StringComparison.Ordinal))
                .Sum(item => item.Duration.TotalMilliseconds),
            saves.Saves, saves.TrackedHighWater);
    }

    public async Task<int> CountCaveStateLoadCommandsAsync(string csvText)
    {
        var sql = new SqlTimingInterceptor();
        var saves = new SaveMetricsInterceptor();
        await using var db = CreateContext(sql, saves);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(csvText);
        var records = await new CaveImportCsvParser().ParseAsync(csv);
        sql.Reset();
        await new CaveImportPlanningRepository(db, db.RequestUser).LoadAsync(records, syncExisting: false);
        return sql.Items.Count;
    }

    private PlanarianDbContext CreateContext(SqlTimingInterceptor sql, SaveMetricsInterceptor saves)
    {
        using (database.CreateDbContext("bench", accountId)) { }
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(database.ConnectionString, options =>
            {
                options.MigrationsAssembly("Planarian.Migrations");
                options.UseNetTopologySuite();
                options.MaxBatchSize(1000);
            })
            .AddInterceptors(sql, saves)
            .Options;
        var db = new PlanarianDbContext(options);
        db.RequestUser = new RequestUser(db)
        {
            Id = "bench",
            AccountId = accountId,
            FirstName = "Scale",
            LastName = "Benchmark"
        };
        return db;
    }

    private static bool IsWrite(string sql) =>
        sql.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
        sql.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
        sql.Contains("DELETE", StringComparison.OrdinalIgnoreCase);
}
