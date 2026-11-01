using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;
using Planarian.Shared.Base;

namespace Planarian.Modules.Import.Data;

public sealed class EntranceImportPlanningReader : AccountScopedDataAccessBase
{

    public EntranceImportPlanningReader(PlanarianDbContext db, RequestUser requestUser) : base(db, requestUser)
    {
    }

    public async Task<EntranceImportPlanningState> LoadAsync(IReadOnlyList<EntranceCsvModel> records,
        CancellationToken cancellationToken = default)
    {
        var tagKeys = new[] { TagTypeKeyConstant.LocationQuality, TagTypeKeyConstant.EntranceStatus,
            TagTypeKeyConstant.EntranceHydrology, TagTypeKeyConstant.FieldIndication, TagTypeKeyConstant.People,
            TagTypeKeyConstant.CaveOther };
        var tags = await DbContext.TagTypes.Where(t => tagKeys.Contains(t.Key) && (t.AccountId == Scope.AccountId || t.IsDefault))
            .AsNoTracking().Select(t => new ImportTagLookup(t.Id, t.Key, t.Name, t.AccountId, t.IsDefault, false))
            .ToListAsync(cancellationToken);
        var keys = records.Select(r => (r.CountyCode, Number: int.TryParse(r.CountyCaveNumber, out var n) ? n : int.MinValue)).ToList();
        var countyCodes = keys.Select(k => k.CountyCode).Where(c => c != null).Distinct(StringComparer.Ordinal).ToList();
        var caveNumbers = keys.Select(k => k.Number).Distinct().ToList();
        var caves = await DbContext.Caves.IgnoreQueryFilters()
            .Where(c => c.AccountId == Scope.AccountId && countyCodes.Contains(c.County.DisplayId) && caveNumbers.Contains(c.CountyNumber))
            .AsNoTracking().Select(c => new EntranceImportCaveLookup(c.Id, c.Name, c.County.DisplayId, c.CountyNumber,
                c.Version, c.CurrentRevisionId)).ToListAsync(cancellationToken);
        var caveIds = caves.Select(c => c.Id).ToList();
        var counts = caveIds.Count == 0 ? [] : await DbContext.Entrances.IgnoreQueryFilters()
            .Where(e => caveIds.Contains(e.CaveId) && e.Cave != null && e.Cave.AccountId == Scope.AccountId)
            .AsNoTracking().GroupBy(e => e.CaveId)
            .Select(g => new { CaveId = g.Key, Count = g.Count(), Primary = g.Count(e => e.IsPrimary) })
            .ToListAsync(cancellationToken);
        return new EntranceImportPlanningState(Scope.AccountId, tags, caves,
            counts.ToDictionary(x => x.CaveId, x => x.Count, StringComparer.Ordinal),
            counts.ToDictionary(x => x.CaveId, x => x.Primary, StringComparer.Ordinal));
    }
}
