using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Extensions.String;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Shared;
using Planarian.Modules.Import.Models;
using Planarian.Shared.Base;

namespace Planarian.Modules.Import.Repositories;

public class TemporaryEntranceRepository : RepositoryBase<PlanarianDbContextBase>
{
    private readonly string _temporaryEntranceTableName;

    public TemporaryEntranceRepository(PlanarianDbContextBase dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
        _temporaryEntranceTableName = "TemporaryEntrance" + Guid.NewGuid().ToString().Replace("-", "");
    }


    public async Task<ITable<TemporaryEntrance>> CreateTable()
    {
        await using var db = DbContext.CreateLinqToDBConnection();

        var result = await db
            .CreateTableAsync<TemporaryEntrance>(_temporaryEntranceTableName);
        return result;
    }

    public async Task<BulkCopyRowsCopied> InsertEntrances(IEnumerable<TemporaryEntrance> entrances,
        Action<int, int> onBatchProcessed)
    {
        await using var db = DbContext.CreateLinqToDBConnection();


        var options = new BulkCopyOptions
        {
            TableName = _temporaryEntranceTableName,
            NotifyAfter = 1000,
            RowsCopiedCallback = copied => onBatchProcessed((int)copied.RowsCopied, entrances.Count())
        };

        return await db.BulkCopyAsync(options, entrances);
    }

    public async Task<(List<string> unassociatedEntrances, List<TemporaryEntranceResult> associatedEntrances)> UpdateTemporaryEntranceWithCaveId()
    {
        if (string.IsNullOrEmpty(_temporaryEntranceTableName))
            throw new InvalidOperationException("The temporary table has not been created.");

        await using var db = DbContext.CreateLinqToDBConnection();

        var result = await db.GetTable<TemporaryEntrance>()
            .TableName(_temporaryEntranceTableName)
            .Set(te => te.CaveId, te => db.GetTable<Cave>()
                .Join(db.GetTable<County>(),
                    cave => cave.CountyId,
                    county => county.Id,
                    (cave, county) => new { cave, county })
                .Where(joined => joined.cave.CountyNumber == te.CountyCaveNumber
                                 && joined.county.DisplayId == te.CountyDisplayId
                                 && joined.cave.AccountId == RequestUser.AccountId)
                .Select(joined => joined.cave.Id)
                .FirstOrDefault())
            .UpdateAsync();


        var unassociatedEntrances = await db.GetTable<TemporaryEntrance>()
            .TableName(_temporaryEntranceTableName)
            .Where(e => e.CaveId == null)
            .Select(e => e.Id)
            .ToListAsyncLinqToDB();

        var delimiter = "-";
        
        var associatedEntrances = await db.GetTable<TemporaryEntrance>()
            .TableName(_temporaryEntranceTableName) 
            .Where(e => e.CaveId != null)
            .Join(db.GetTable<Cave>().Where(e => e.AccountId == RequestUser.AccountId),
                te => te.CaveId,
                cave => cave.Id,
                (te, cave) => new TemporaryEntranceResult
                {
                 Id = te.Id,
                 CaveId = te.CaveId,
                 CaveName = cave.Name,
                 DisplayId = $"{te.CountyDisplayId}{delimiter}{te.CountyCaveNumber}",
                })
            .IgnoreQueryFilters()
            .ToListAsyncLinqToDB();


        var deleteResult = await db.GetTable<TemporaryEntrance>()
            .TableName(_temporaryEntranceTableName)
            .IgnoreQueryFilters()
            .Where(e => e.CaveId == null).DeleteAsync();

        return (unassociatedEntrances, associatedEntrances);
    }

    public async Task<List<string>> GetInvalidIsPrimaryRecords()
    {
        await using var db = DbContext.CreateLinqToDBConnection();

        var tempEntranceTable = db.GetTable<TemporaryEntrance>().TableName(_temporaryEntranceTableName);
        var importedCaveIds = await tempEntranceTable
            .Where(e => e.CaveId != null)
            .Select(e => e.CaveId!)
            .Distinct()
            .ToListAsyncLinqToDB();

        if (!importedCaveIds.Any()) return [];

        var scopedImportedCaveIds = await db.GetTable<Cave>()
            .Where(e => importedCaveIds.Contains(e.Id) && e.AccountId == RequestUser.AccountId)
            .Select(e => e.Id)
            .ToListAsyncLinqToDB();

        if (!scopedImportedCaveIds.Any()) return [];

        var entranceTable = db.GetTable<Entrance>();
        var tempPrimaryCounts = (await tempEntranceTable
            .Where(e => e.CaveId != null && e.IsPrimary)
            .GroupBy(e => e.CaveId!)
            .Select(g => new { CaveId = g.Key, Count = g.Count() })
            .ToListAsyncLinqToDB())
            .ToDictionary(x => x.CaveId, x => x.Count);

        var existingPrimaryCounts = (await entranceTable
            .Join(db.GetTable<Cave>().Where(e => e.AccountId == RequestUser.AccountId),
                entrance => entrance.CaveId,
                cave => cave.Id,
                (entrance, cave) => entrance)
            .Where(e => scopedImportedCaveIds.Contains(e.CaveId) && e.IsPrimary)
            .GroupBy(e => e.CaveId)
            .Select(g => new { CaveId = g.Key, Count = g.Count() })
            .ToListAsyncLinqToDB())
            .ToDictionary(x => x.CaveId, x => x.Count);

        var invalidCaveIds = scopedImportedCaveIds
            .Where(caveId =>
                tempPrimaryCounts.GetValueOrDefault(caveId) + existingPrimaryCounts.GetValueOrDefault(caveId) != 1)
            .ToList();

        if (!invalidCaveIds.Any()) return [];

        return await tempEntranceTable
            .Where(e => e.CaveId != null && invalidCaveIds.Contains(e.CaveId))
            .Select(e => e.Id)
            .ToListAsyncLinqToDB();
    }

    public async Task<Dictionary<string, int>> GetExistingEntranceCounts(IEnumerable<string> caveIds,
        CancellationToken cancellationToken)
    {
        var caveIdList = caveIds.Distinct().ToList();
        if (!caveIdList.Any()) return [];

        var scopedCaveIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(DbContext.Caves
            .IgnoreQueryFilters()
            .Where(e => caveIdList.Contains(e.Id) && e.AccountId == RequestUser.AccountId)
            .Select(e => e.Id)
            , cancellationToken);

        if (!scopedCaveIds.Any()) return [];

        return (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(DbContext.Entrances
            .IgnoreQueryFilters()
            .Where(e => scopedCaveIds.Contains(e.CaveId))
            .GroupBy(e => e.CaveId)
            .Select(g => new { CaveId = g.Key, Count = g.Count() }), cancellationToken))
            .ToDictionary(x => x.CaveId, x => x.Count);
    }

    public async Task<Dictionary<string, int>> GetExistingPrimaryEntranceCounts(IEnumerable<string> caveIds,
        CancellationToken cancellationToken)
    {
        var caveIdList = caveIds.Distinct().ToList();
        if (!caveIdList.Any()) return [];

        var scopedCaveIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(DbContext.Caves
            .IgnoreQueryFilters()
            .Where(e => caveIdList.Contains(e.Id) && e.AccountId == RequestUser.AccountId)
            .Select(e => e.Id)
            , cancellationToken);

        if (!scopedCaveIds.Any()) return [];

        return (await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(DbContext.Entrances
            .IgnoreQueryFilters()
            .Where(e => scopedCaveIds.Contains(e.CaveId) && e.IsPrimary)
            .GroupBy(e => e.CaveId)
            .Select(g => new { CaveId = g.Key, Count = g.Count() }), cancellationToken))
            .ToDictionary(x => x.CaveId, x => x.Count);
    }

    public async Task MigrateTemporaryEntrancesAsync()
    {
        var command = $@"
        INSERT INTO {nameof(DbContext.Entrances).Quote()} (
            {nameof(Entrance.Id).Quote()},
            {nameof(Entrance.CaveId).Quote()},
            {nameof(Entrance.LocationQualityTagId).Quote()},
            {nameof(Entrance.Name).Quote()},
            {nameof(Entrance.IsPrimary).Quote()},
            {nameof(Entrance.Description).Quote()},
            {nameof(Entrance.Location).Quote()},
            {nameof(Entrance.ReportedOn).Quote()},
            {nameof(Entrance.ReportedByUserId).Quote()},
            {nameof(Entrance.PitDepthFeet).Quote()},
            {nameof(Entrance.CreatedByUserId).Quote()},
            {nameof(Entrance.ModifiedByUserId).Quote()},
            {nameof(Entrance.CreatedOn).Quote()},
            {nameof(Entrance.ModifiedOn).Quote()}
        )
        SELECT 
            {nameof(TemporaryEntrance.Id).Quote()},
            {nameof(TemporaryEntrance.CaveId).Quote()},
            {nameof(TemporaryEntrance.LocationQualityTagId).Quote()},
            {nameof(TemporaryEntrance.Name).Quote()},
            {nameof(TemporaryEntrance.IsPrimary).Quote()},
            {nameof(TemporaryEntrance.Description).Quote()},
             ST_SetSRID(ST_MakePoint(
                CAST({nameof(TemporaryEntrance.Longitude).Quote()} AS FLOAT),
                CAST({nameof(TemporaryEntrance.Latitude).Quote()} AS FLOAT),
                CAST({nameof(TemporaryEntrance.Elevation).Quote()} AS FLOAT)
            ), 4326),
            {nameof(TemporaryEntrance.ReportedOn).Quote()},
            {nameof(TemporaryEntrance.ReportedByUserId).Quote()},
            {nameof(TemporaryEntrance.PitFeet).Quote()},
            {nameof(TemporaryEntrance.CreatedByUserId).Quote()},
            {nameof(TemporaryEntrance.ModifiedByUserId).Quote()},
            {nameof(TemporaryEntrance.CreatedOn).Quote()},
            {nameof(TemporaryEntrance.ModifiedOn).Quote()}
        FROM {_temporaryEntranceTableName.Quote()}";
        
        await DbContext.Database.ExecuteSqlRawAsync(command);
    }

    public List<TemporaryEntrance> GetEntrancesById(string id)
    {
        return DbContext.CreateLinqToDBConnection().GetTable<TemporaryEntrance>().TableName(_temporaryEntranceTableName)
            .Where(e => e.Id == id).ToList();
    }

    public async Task<List<TemporaryEntrance>> GetAllEntrances()
    {
        return await DbContext.CreateLinqToDBConnection()
            .GetTable<TemporaryEntrance>()
            .TableName(_temporaryEntranceTableName)
            .ToListAsyncLinqToDB();
    }

    public async Task DeleteExistingEntrancesForImportedCaves(CancellationToken cancellationToken)
    {
        var caveIds = await DbContext.CreateLinqToDBConnection()
            .GetTable<TemporaryEntrance>()
            .TableName(_temporaryEntranceTableName)
            .Where(e => e.CaveId != null)
            .Select(e => e.CaveId!)
            .Distinct()
            .ToListAsyncLinqToDB();

        if (!caveIds.Any()) return;

        var scopedCaveIds = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(DbContext.Caves
            .IgnoreQueryFilters()
            .Where(e => caveIds.Contains(e.Id) && e.AccountId == RequestUser.AccountId)
            .Select(e => e.Id)
            , cancellationToken);

        if (!scopedCaveIds.Any()) return;

        var entranceIds = await DbContext.Entrances
            .IgnoreQueryFilters()
            .Where(e => scopedCaveIds.Contains(e.CaveId))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken: cancellationToken);

        if (!entranceIds.Any()) return;

        await DbContext.EntranceStatusTags
            .Where(e => entranceIds.Contains(e.EntranceId))
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceHydrologyTags
            .Where(e => entranceIds.Contains(e.EntranceId))
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.FieldIndicationTags
            .Where(e => entranceIds.Contains(e.EntranceId))
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceReportedByNameTags
            .Where(e => entranceIds.Contains(e.EntranceId))
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.EntranceOtherTag
            .Where(e => entranceIds.Contains(e.EntranceId))
            .ExecuteDeleteAsync(cancellationToken);
        await DbContext.Entrances
            .IgnoreQueryFilters()
            .Where(e => entranceIds.Contains(e.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DropTable()
    {
        await DbContext.CreateLinqToDBConnection()
            .DropTableAsync<TemporaryEntrance>(_temporaryEntranceTableName);
    }
}
