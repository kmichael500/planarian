using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;

namespace Planarian.Modules.Import.Data;

public sealed class EntranceImportPlanningRepository
{
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public EntranceImportPlanningRepository(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task<EntranceImportPlanningState> LoadAsync(IReadOnlyList<EntranceCsvModel> records,
        CancellationToken cancellationToken = default)
    {
        var tagKeys = new[] { TagTypeKeyConstant.LocationQuality, TagTypeKeyConstant.EntranceStatus,
            TagTypeKeyConstant.EntranceHydrology, TagTypeKeyConstant.FieldIndication, TagTypeKeyConstant.People };
        var tags = await _db.TagTypes.Where(t => tagKeys.Contains(t.Key) && (t.AccountId == _scope.AccountId || t.IsDefault))
            .AsNoTracking().Select(t => new ImportTagLookup(t.Id, t.Key, t.Name, t.AccountId, t.IsDefault, false))
            .ToListAsync(cancellationToken);
        var keys = records.Select(r => (r.CountyCode, Number: int.TryParse(r.CountyCaveNumber, out var n) ? n : int.MinValue)).ToList();
        var countyCodes = keys.Select(k => k.CountyCode).Where(c => c != null).Distinct(StringComparer.Ordinal).ToList();
        var caveNumbers = keys.Select(k => k.Number).Distinct().ToList();
        var caves = await _db.Caves.IgnoreQueryFilters()
            .Where(c => c.AccountId == _scope.AccountId && countyCodes.Contains(c.County.DisplayId) && caveNumbers.Contains(c.CountyNumber))
            .AsNoTracking().Select(c => new EntranceImportCaveLookup(c.Id, c.Name, c.County.DisplayId, c.CountyNumber,
                c.Version, c.CurrentRevisionId)).ToListAsync(cancellationToken);
        var caveIds = caves.Select(c => c.Id).ToList();
        var counts = caveIds.Count == 0 ? [] : await _db.Entrances.IgnoreQueryFilters()
            .Where(e => caveIds.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == _scope.AccountId)
            .AsNoTracking().GroupBy(e => e.CaveId)
            .Select(g => new { CaveId = g.Key, Count = g.Count(), Primary = g.Count(e => e.IsPrimary) })
            .ToListAsync(cancellationToken);
        return new EntranceImportPlanningState(_scope.AccountId, tags, caves,
            counts.ToDictionary(x => x.CaveId, x => x.Count, StringComparer.Ordinal),
            counts.ToDictionary(x => x.CaveId, x => x.Primary, StringComparer.Ordinal));
    }
}
