using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Extensions.String;
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
                                 && joined.county.DisplayId == te.CountyDisplayId
                                 && joined.cave.AccountId == RequestUser.AccountId) // Include AccountId in the join condition
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

        var invalidTempEntrances = new List<string>();
        int batchSize = 1000;
        int offset = 0;
    
        while(true)  // Continue processing batches until the end of the table is reached
        {
            var batchQuery = (from temp in tempEntranceTable.Skip(offset).Take(batchSize).Where(e => e.IsPrimary)
                where tempEntranceTable.Any(e => e.CaveId == temp.CaveId && e.IsPrimary && e.Id != temp.Id)
                      || entranceTable.Any(e => e.CaveId == temp.CaveId && e.IsPrimary)
                select temp.Id);

            var batchResult = await batchQuery.ToListAsyncLinqToDB();
            invalidTempEntrances.AddRange(batchResult);

            if(batchResult.Count < batchSize)
            {
                break;  // Exit loop if the end of the table is reached
            }

            offset += batchSize;  // Prepare to process the next batch
        }

        return invalidTempEntrances;
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
            {nameof(Entrance.ReportedByName).Quote()},
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
            {nameof(TemporaryEntrance.ReportedByName).Quote()},
            {nameof(TemporaryEntrance.PitFeet).Quote()},
            {nameof(TemporaryEntrance.CreatedByUserId).Quote()},
            {nameof(TemporaryEntrance.ModifiedByUserId).Quote()},
            {nameof(TemporaryEntrance.CreatedOn).Quote()},
            {nameof(TemporaryEntrance.ModifiedOn).Quote()}
        FROM {_temporaryEntranceTableName.Quote()}";


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