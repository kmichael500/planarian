using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;

namespace Planarian.Modules.Import.Data;

public sealed class CaveImportPlanningRepository
{
    private readonly PlanarianDbContext _db;
    private readonly AccountExecutionScope _scope;

    public CaveImportPlanningRepository(PlanarianDbContext db, RequestUser requestUser)
    {
        _db = db;
        _scope = AccountExecutionScope.Require(requestUser);
    }

    public async Task<CaveImportPlanningState> LoadAsync(IReadOnlyList<CaveCsvModel> records, bool syncExisting,
        CancellationToken cancellationToken = default)
    {
        var stateInputs = records.Select(r => r.State.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var states = await _db.States.Where(s => stateInputs.Contains(s.Name) || stateInputs.Contains(s.Abbreviation))
            .AsNoTracking().Select(s => new CaveImportStateLookup(s.Id, s.Name, s.Abbreviation))
            .ToListAsync(cancellationToken);
        var accountStates = (await _db.AccountStates.Where(a => a.AccountId == _scope.AccountId).AsNoTracking()
            .Select(a => a.StateId).ToListAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var counties = await _db.Counties.Where(c => c.AccountId == _scope.AccountId).AsNoTracking()
            .Select(c => new CaveImportCountyLookup(c.Id, c.StateId, c.DisplayId, c.Name)).ToListAsync(cancellationToken);
        var tagKeys = new[] { TagTypeKeyConstant.Geology, TagTypeKeyConstant.GeologicAge,
            TagTypeKeyConstant.MapStatus, TagTypeKeyConstant.PhysiographicProvince, TagTypeKeyConstant.Archeology,
            TagTypeKeyConstant.Biology, TagTypeKeyConstant.CaveOther, TagTypeKeyConstant.People };
        var tags = await _db.TagTypes.Where(t => tagKeys.Contains(t.Key) && (t.AccountId == _scope.AccountId || t.IsDefault))
            .AsNoTracking().Select(t => new ImportTagLookup(t.Id, t.Key, t.Name, t.AccountId, t.IsDefault, false))
            .ToListAsync(cancellationToken);
        var caves = syncExisting
            ? await _db.Caves.IgnoreQueryFilters().Where(c => c.AccountId == _scope.AccountId).AsNoTracking()
                .Select(c => new CaveImportExistingCave(c.Id, c.Version, c.CurrentRevisionId, c.StateId,
                    c.State.Abbreviation, c.CountyId, c.County.Name, c.County.DisplayId, c.CountyNumber, c.Name,
                    c.AlternateNames, c.LengthFeet, c.DepthFeet, c.MaxPitDepthFeet, c.NumberOfPits, c.Narrative,
                    c.ReportedOn, c.IsArchived, c.GeologyTags.Select(t => t.TagTypeId).ToList(),
                    c.GeologicAgeTags.Select(t => t.TagTypeId).ToList(), c.MapStatusTags.Select(t => t.TagTypeId).ToList(),
                    c.PhysiographicProvinceTags.Select(t => t.TagTypeId).ToList(), c.ArcheologyTags.Select(t => t.TagTypeId).ToList(),
                    c.BiologyTags.Select(t => t.TagTypeId).ToList(), c.CaveOtherTags.Select(t => t.TagTypeId).ToList(),
                    c.CartographerNameTags.Select(t => t.TagTypeId).ToList(), c.CaveReportedByNameTags.Select(t => t.TagTypeId).ToList()))
                .ToListAsync(cancellationToken)
            : [];
        var used = (await _db.Caves.IgnoreQueryFilters().Where(c => c.AccountId == _scope.AccountId).AsNoTracking()
            .Select(c => new CaveImportUsedCountyNumber(c.CountyId, c.CountyNumber)).ToListAsync(cancellationToken)).ToHashSet();
        return new CaveImportPlanningState(_scope.AccountId, states, accountStates, counties, tags, caves, used);
    }
}
