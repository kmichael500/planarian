using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Shared;
using Planarian.Modules.Import.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Import.Repositories;

/// <summary>
/// Typed, non-EF-tracked entrance import staging. CSV rows are normalized in
/// memory and resolved against account-scoped reference data; no dynamic SQL
/// table or temporary database lifecycle is needed.
/// </summary>
public class EntranceImportPlanStore : RepositoryBase<PlanarianDbContextBase>
{
    private readonly List<TemporaryEntrance> _rows = [];
    private readonly AccountExecutionScope _scope;

    public EntranceImportPlanStore(PlanarianDbContextBase dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public Task Reset() { _rows.Clear(); return Task.CompletedTask; }

    public async Task<int> InsertEntrances(IEnumerable<TemporaryEntrance> entrances, Func<int, int, Task> onBatchProcessed)
    {
        _rows.Clear();
        _rows.AddRange(entrances);
        await onBatchProcessed(_rows.Count, _rows.Count);
        return _rows.Count;
    }

    public async Task<(List<string> unassociatedEntrances, List<TemporaryEntranceResult> associatedEntrances)> UpdateTemporaryEntranceWithCaveId()
    {
        var caves = await DbContext.Caves.IgnoreQueryFilters()
            .Where(c => c.AccountId == _scope.AccountId)
            .Select(c => new { c.Id, c.Name, c.CountyNumber, CountyDisplayId = c.County.DisplayId })
            .ToListAsync();
        var lookup = caves.ToDictionary(c => (c.CountyDisplayId, c.CountyNumber));
        var unassociated = new List<string>();
        var associated = new List<TemporaryEntranceResult>();

        foreach (var row in _rows)
        {
            if (!lookup.TryGetValue((row.CountyDisplayId, row.CountyCaveNumber), out var cave))
            {
                unassociated.Add(row.Id);
                continue;
            }

            row.CaveId = cave.Id;
            associated.Add(new TemporaryEntranceResult
            {
                Id = row.Id, CaveId = cave.Id, CaveName = cave.Name,
                DisplayId = $"{cave.CountyDisplayId}-{cave.CountyNumber}"
            });
        }

        _rows.RemoveAll(row => row.CaveId is null);
        return (unassociated, associated);
    }

    public async Task<List<string>> GetInvalidIsPrimaryRecords()
    {
        var importedByCave = _rows.Where(row => row.CaveId is not null)
            .GroupBy(row => row.CaveId!)
            .ToDictionary(group => group.Key, group => group.ToList());
        if (importedByCave.Count == 0) return [];

        var caveIds = importedByCave.Keys.ToList();
        var existingPrimary = await DbContext.Entrances.IgnoreQueryFilters()
            .Where(e => caveIds.Contains(e.CaveId) && e.Cave!.AccountId == _scope.AccountId && e.IsPrimary)
            .GroupBy(e => e.CaveId)
            .Select(g => new { CaveId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(e => e.CaveId, e => e.Count);

        var invalid = new List<string>();
        foreach (var (caveId, rows) in importedByCave)
        {
            var count = rows.Count(row => row.IsPrimary) + existingPrimary.GetValueOrDefault(caveId);
            if (count != 1) invalid.AddRange(rows.Select(row => row.Id));
        }

        return invalid;
    }

    public async Task<Dictionary<string, int>> GetExistingEntranceCounts(IEnumerable<string> caveIds,
        CancellationToken cancellationToken)
    {
        var ids = await OwnedCaveIds(caveIds, cancellationToken);
        return await DbContext.Entrances.IgnoreQueryFilters().Where(e => ids.Contains(e.CaveId))
            .GroupBy(e => e.CaveId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(e => e.Key, e => e.Count, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetExistingPrimaryEntranceCounts(IEnumerable<string> caveIds,
        CancellationToken cancellationToken)
    {
        var ids = await OwnedCaveIds(caveIds, cancellationToken);
        return await DbContext.Entrances.IgnoreQueryFilters().Where(e => ids.Contains(e.CaveId) && e.IsPrimary)
            .GroupBy(e => e.CaveId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(e => e.Key, e => e.Count, cancellationToken);
    }

    private async Task<List<string>> OwnedCaveIds(IEnumerable<string> caveIds, CancellationToken cancellationToken) =>
        await DbContext.Caves.IgnoreQueryFilters()
            .Where(c => caveIds.Contains(c.Id) && c.AccountId == _scope.AccountId)
            .Select(c => c.Id).ToListAsync(cancellationToken);

    public async Task MigrateTemporaryEntrancesAsync()
    {
        var caveIds = _rows.Where(row => row.CaveId is not null).Select(row => row.CaveId!).Distinct().ToList();
        var caves = await DbContext.Caves.IgnoreQueryFilters()
            .Where(c => caveIds.Contains(c.Id) && c.AccountId == _scope.AccountId)
            .ToDictionaryAsync(c => c.Id);
        var entities = _rows.Where(row => row.CaveId is not null).Select(row => new Entrance
        {
            Id = row.Id, CaveId = row.CaveId!, LocationQualityTagId = row.LocationQualityTagId,
            Name = row.Name, IsPrimary = row.IsPrimary, Description = row.Description,
            Location = new Point(row.Longitude, row.Latitude, row.Elevation) { SRID = 4326 },
            ReportedOn = row.ReportedOn, PitDepthFeet = row.PitFeet,
            ReportedByUserId = row.ReportedByUserId, CreatedByUserId = row.CreatedByUserId,
            ModifiedByUserId = row.ModifiedByUserId, CreatedOn = row.CreatedOn, ModifiedOn = row.ModifiedOn,
            Cave = caves[row.CaveId!]
        }).ToList();
        AddRange(entities);
    }

    public List<TemporaryEntrance> GetEntrancesById(string id) => _rows.Where(row => row.Id == id).ToList();
    public Task<List<TemporaryEntrance>> GetAllEntrances() => Task.FromResult(_rows.ToList());

    public async Task DeleteExistingEntrancesForImportedCaves(CancellationToken cancellationToken)
    {
        var caveIds = _rows.Where(row => row.CaveId is not null).Select(row => row.CaveId!).Distinct().ToList();
        var ownedCaveIds = await OwnedCaveIds(caveIds, cancellationToken);
        var entranceIds = await DbContext.Entrances.IgnoreQueryFilters()
            .Where(e => ownedCaveIds.Contains(e.CaveId)).Select(e => e.Id).ToListAsync(cancellationToken);
        if (entranceIds.Count == 0) return;

        await DbContext.EntranceStatusTags.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceHydrologyTags.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.FieldIndicationTags.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceReportedByNameTags.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceOtherTag.Where(e => entranceIds.Contains(e.EntranceId)).ExecuteDeleteAsync(cancellationToken);
        await DbContext.Entrances.IgnoreQueryFilters().Where(e => entranceIds.Contains(e.Id)).ExecuteDeleteAsync(cancellationToken);
    }

    public Task Clear()
    {
        _rows.Clear();
        return Task.CompletedTask;
    }
}
