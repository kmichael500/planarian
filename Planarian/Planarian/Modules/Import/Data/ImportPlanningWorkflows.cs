using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Import.Parsing;
using Planarian.Modules.Import.Planning;

namespace Planarian.Modules.Import.Data;

/// <summary>
/// Convenience orchestration boundary for callers that already own a scoped context,
/// primarily focused integration tests. Production application services receive each
/// parser, repository, and pure planner independently through DI.
/// </summary>
public sealed class CaveImportPlanningWorkflow
{
    private readonly CaveImportCsvParser _parser = new();
    private readonly CaveImportPlanningRepository _repository;
    private readonly CaveImportPlanner _planner = new();

    public CaveImportPlanningWorkflow(PlanarianDbContext db, RequestUser requestUser) =>
        _repository = new CaveImportPlanningRepository(db, requestUser);

    public async Task<CaveImportPlan> PlanAsync(Stream stream, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var records = await _parser.ParseAsync(stream, cancellationToken);
        var state = await _repository.LoadAsync(records, syncExisting, cancellationToken);
        return _planner.Plan(records, state, syncExisting, cancellationToken);
    }
}

public sealed class EntranceImportPlanningWorkflow
{
    private readonly EntranceImportCsvParser _parser = new();
    private readonly EntranceImportPlanningRepository _repository;
    private readonly EntranceImportPlanner _planner = new();

    public EntranceImportPlanningWorkflow(PlanarianDbContext db, RequestUser requestUser) =>
        _repository = new EntranceImportPlanningRepository(db, requestUser);

    public async Task<EntranceImportPlan> PlanAsync(Stream stream, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var records = await _parser.ParseAsync(stream, cancellationToken);
        var state = await _repository.LoadAsync(records, cancellationToken);
        return _planner.Plan(records, state, syncExisting, cancellationToken);
    }
}
