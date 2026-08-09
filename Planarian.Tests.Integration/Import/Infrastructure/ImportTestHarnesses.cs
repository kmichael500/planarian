using System.Text;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Data;
using Planarian.Modules.Import.Parsing;
using Planarian.Modules.Import.Planning;

namespace Planarian.Tests;

/// <summary>
/// Test-only composition for the real Cave import path. Semantic inputs remain
/// visible in each test while repetitive parser/repository/executor wiring is
/// kept out of production workflow types.
/// </summary>
internal sealed class CaveImportTestHarness
{
    private readonly PlanarianDbContext _db;
    private readonly CaveImportCsvParser _parser = new();
    private readonly CaveImportPlanningRepository _planningRepository;
    private readonly CaveImportPlanner _planner = new();

    public CaveImportTestHarness(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _planningRepository = new CaveImportPlanningRepository(db, requestUser);
    }

    public async Task<CaveImportPlan> PlanAsync(Stream csv, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var records = await _parser.ParseAsync(csv, cancellationToken);
        var state = await _planningRepository.LoadAsync(records, syncExisting, cancellationToken);
        return _planner.Plan(records, state, syncExisting, cancellationToken);
    }

    public async Task<CaveImportPlan> PlanCsvAsync(string csv, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        await using var stream = CsvStream(csv);
        return await PlanAsync(stream, syncExisting, cancellationToken);
    }

    public Task<CaveImportExecutionResult> ExecuteAsync(CaveImportPlan plan, string sourceFileName,
        CancellationToken cancellationToken = default)
    {
        var snapshots = new CavePublishedSnapshotRepository(_db, _db.RequestUser);
        var revisions = new CaveImportRevisionRepository(_db, _db.RequestUser);
        var repository = new CaveImportExecutionRepository(_db, _db.RequestUser, snapshots, revisions);
        return repository.ExecuteAsync(plan, sourceFileName, cancellationToken);
    }

    private static MemoryStream CsvStream(string csv) => new(Encoding.UTF8.GetBytes(csv));
}

/// <summary>Test-only composition for the real Entrance import path.</summary>
internal sealed class EntranceImportTestHarness
{
    private readonly PlanarianDbContext _db;
    private readonly EntranceImportCsvParser _parser = new();
    private readonly EntranceImportPlanningRepository _planningRepository;
    private readonly EntranceImportPlanner _planner = new();

    public EntranceImportTestHarness(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _planningRepository = new EntranceImportPlanningRepository(db, requestUser);
    }

    public async Task<EntranceImportPlan> PlanAsync(Stream csv, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var records = await _parser.ParseAsync(csv, cancellationToken);
        var state = await _planningRepository.LoadAsync(records, cancellationToken);
        return _planner.Plan(records, state, syncExisting, cancellationToken);
    }

    public async Task<EntranceImportPlan> PlanCsvAsync(string csv, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        await using var stream = CsvStream(csv);
        return await PlanAsync(stream, syncExisting, cancellationToken);
    }

    public Task<string> ExecuteAsync(EntranceImportPlan plan, string sourceFileName,
        CancellationToken cancellationToken = default)
    {
        var snapshots = new CavePublishedSnapshotRepository(_db, _db.RequestUser);
        var revisions = new CaveImportRevisionRepository(_db, _db.RequestUser);
        var repository = new EntranceImportExecutionRepository(_db, _db.RequestUser, snapshots, revisions);
        return repository.ExecuteAsync(plan, sourceFileName, cancellationToken);
    }

    private static MemoryStream CsvStream(string csv) => new(Encoding.UTF8.GetBytes(csv));
}
