using System.Text;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Data;
using Planarian.Modules.Import.Parsing;
using Planarian.Modules.Import.Planning;
using Planarian.Modules.Tags.Repositories;

using Planarian.Modules.Account.Persistence;
using Planarian.Modules.Tags.Persistence;
namespace Planarian.Tests;

/// <summary>
/// Test-only composition for the real Cave import path. Semantic inputs remain
/// visible in each test while repetitive parser/reader/persistence wiring is
/// kept out of production workflow types.
/// </summary>
internal sealed class CaveImportTestHarness
{
    private readonly PlanarianDbContext _db;
    private readonly CaveImportCsvParser _parser = new();
    private readonly CaveImportPlanningReader _planningReader;
    private readonly CaveImportPlanner _planner = new();

    public CaveImportTestHarness(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _planningReader = new CaveImportPlanningReader(db, requestUser);
    }

    public async Task<CaveImportPlan> PlanAsync(Stream csv, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var records = await _parser.ParseAsync(csv, cancellationToken);
        var state = await _planningReader.LoadAsync(records, syncExisting, cancellationToken);
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
        var persistence = CreatePersistence();
        return persistence.ExecuteAsync(plan, sourceFileName, cancellationToken);
    }

    public Task<CaveImportExecutionResult> ExecuteUploadedAsync(CaveImportPlan plan, string sourceFileId,
        CancellationToken cancellationToken = default)
    {
        var persistence = CreatePersistence();
        return persistence.ExecuteUploadedAsync(plan, sourceFileId, cancellationToken);
    }

    private CaveImportPersistence CreatePersistence()
    {
        var snapshotReader = new CavePublishedSnapshotReader(_db, _db.RequestUser);
        var revisionPublisher = new CaveBulkRevisionPublisher(_db, _db.RequestUser);
        var tagReferenceLocker = new TagReferenceLocker(_db, _db.RequestUser);
        return new CaveImportPersistence(_db, _db.RequestUser, snapshotReader, revisionPublisher, tagReferenceLocker,
            new CountyReferenceLocker(_db, _db.RequestUser));
    }

    private static MemoryStream CsvStream(string csv) => new(Encoding.UTF8.GetBytes(csv));
}

/// <summary>Test-only composition for the real Entrance import path.</summary>
internal sealed class EntranceImportTestHarness
{
    private readonly PlanarianDbContext _db;
    private readonly EntranceImportCsvParser _parser = new();
    private readonly EntranceImportPlanningReader _planningReader;
    private readonly EntranceImportPlanner _planner = new();

    public EntranceImportTestHarness(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _planningReader = new EntranceImportPlanningReader(db, requestUser);
    }

    public async Task<EntranceImportPlan> PlanAsync(Stream csv, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var records = await _parser.ParseAsync(csv, cancellationToken);
        var state = await _planningReader.LoadAsync(records, cancellationToken);
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
        var persistence = CreatePersistence();
        return persistence.ExecuteAsync(plan, sourceFileName, cancellationToken);
    }

    public Task<string> ExecuteUploadedAsync(EntranceImportPlan plan, string sourceFileId,
        CancellationToken cancellationToken = default)
    {
        var persistence = CreatePersistence();
        return persistence.ExecuteUploadedAsync(plan, sourceFileId, cancellationToken);
    }

    private EntranceImportPersistence CreatePersistence()
    {
        var snapshotReader = new CavePublishedSnapshotReader(_db, _db.RequestUser);
        var revisionPublisher = new CaveBulkRevisionPublisher(_db, _db.RequestUser);
        var tagReferenceLocker = new TagReferenceLocker(_db, _db.RequestUser);
        return new EntranceImportPersistence(_db, _db.RequestUser, snapshotReader, revisionPublisher, tagReferenceLocker);
    }

    private static MemoryStream CsvStream(string csv) => new(Encoding.UTF8.GetBytes(csv));
}
