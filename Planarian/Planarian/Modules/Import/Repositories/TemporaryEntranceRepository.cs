using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Import.Repositories;

public class TemporaryEntranceRepository : RepositoryBase
{
    private readonly string _temporaryEntranceTableName;

    public TemporaryEntranceRepository(PlanarianDbContext dbContext, RequestUser requestUser)
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

    public async Task<IEnumerable<string>> UpdateTemporaryEntranceWithCaveId()
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
                                 && joined.county.DisplayId == te.CountyDisplayId)
                .Select(joined => joined.cave.Id)
                .FirstOrDefault())
            .UpdateAsync();

        var unassociatedEntrances = await db.GetTable<TemporaryEntrance>()
            .TableName(_temporaryEntranceTableName) // Use dynamic table name
            .Where(e => e.CaveId == null)
            .Select(e => e.Id).ToListAsyncLinqToDB();

        var deleteResult = await db.GetTable<TemporaryEntrance>()
            .TableName(_temporaryEntranceTableName) // Use dynamic table name
            .Where(e => e.CaveId == null).DeleteAsync();

        return unassociatedEntrances;
    }

    public async Task<List<string>> GetInvalidIsPrimaryRecords()
    {
        await using var db = DbContext.CreateLinqToDBConnection();

        var tempEntranceTable = db.GetTable<TemporaryEntrance>().TableName(_temporaryEntranceTableName);
        var entranceTable = db.GetTable<Entrance>();

        // Selecting TemporaryEntrance records that are marked as IsPrimary
        var invalidTempEntrances = await (from temp in tempEntranceTable.Where(e => e.IsPrimary)

            // Check if there is another TemporaryEntrance record marked as IsPrimary with the same CaveId
            where tempEntranceTable.Any(e => e.CaveId == temp.CaveId && e.IsPrimary && e.Id != temp.Id)

                  // Union with TemporaryEntrance records that have a corresponding Entrance record marked as IsPrimary with the same CaveId
                  || entranceTable.Any(e => e.CaveId == temp.CaveId && e.IsPrimary)
            select temp.Id).ToListAsyncLinqToDB();
        return invalidTempEntrances;
    }


    public async Task MigrateTemporaryEntrancesAsync()
    {
        var command = $@"
        INSERT INTO {nameof(DbContext.Entrances)} (
            {nameof(Entrance.Id)},
            {nameof(Entrance.CaveId)},
            {nameof(Entrance.LocationQualityTagId)},
            {nameof(Entrance.Name)},
            {nameof(Entrance.IsPrimary)},
            {nameof(Entrance.Description)},
            {nameof(Entrance.Location)},
            {nameof(Entrance.ReportedOn)},
            {nameof(Entrance.ReportedByUserId)},
            {nameof(Entrance.ReportedByName)},
            {nameof(Entrance.PitFeet)},
            {nameof(Entrance.CreatedByUserId)},
            {nameof(Entrance.ModifiedByUserId)},
            {nameof(Entrance.CreatedOn)},
            {nameof(Entrance.ModifiedOn)}
        )
        SELECT 
            {nameof(TemporaryEntrance.Id)},
            {nameof(TemporaryEntrance.CaveId)},
            {nameof(TemporaryEntrance.LocationQualityTagId)},
            {nameof(TemporaryEntrance.Name)},
            {nameof(TemporaryEntrance.IsPrimary)},
            {nameof(TemporaryEntrance.Description)},
            geography::STPointFromText(
                'POINT(' + 
                CAST({nameof(TemporaryEntrance.Longitude)} AS VARCHAR(20)) + ' ' + 
                CAST({nameof(TemporaryEntrance.Latitude)} AS VARCHAR(20)) + ' ' + 
                CAST({nameof(TemporaryEntrance.Elevation)} AS VARCHAR(20)) + ')', 
                4326
            ), 
            {nameof(TemporaryEntrance.ReportedOn)},
            {nameof(TemporaryEntrance.ReportedByUserId)},
            {nameof(TemporaryEntrance.ReportedByName)},
            {nameof(TemporaryEntrance.PitFeet)},
            {nameof(TemporaryEntrance.CreatedByUserId)},
            {nameof(TemporaryEntrance.ModifiedByUserId)},
            {nameof(TemporaryEntrance.CreatedOn)},
            {nameof(TemporaryEntrance.ModifiedOn)}
        FROM {_temporaryEntranceTableName}";


        // execute raw sql
        await DbContext.Database.ExecuteSqlRawAsync(command);
    }

    public List<TemporaryEntrance> GetEntrancesById(string id)
    {
        return DbContext.CreateLinqToDBConnection().GetTable<TemporaryEntrance>().TableName(_temporaryEntranceTableName)
            .Where(e => e.Id == id).ToList();
    }

    public async Task DropTable()
    {
        await DbContext.CreateLinqToDBConnection()
            .DropTableAsync<TemporaryEntrance>(_temporaryEntranceTableName);
    }
}