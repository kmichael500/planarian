using LinqToDB;
using LinqToDB.Data;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.TemporaryEntities;
using Planarian.Model.Shared;
using Planarian.Shared.Base;

namespace Planarian.Modules.Caves.Repositories;

public class TemporaryEntranceRepository : RepositoryBase
{
    public TemporaryEntranceRepository(PlanarianDbContext dbContext, RequestUser requestUser)
        : base(dbContext, requestUser)
    {
    }

    public async Task<ITable<TemporaryEntrance>> CreateTable()
    {
        var uniqueTableName = "TemporaryEntrance" + Guid.NewGuid().ToString().Replace("-", "");

        await using var db = DbContext.CreateLinqToDBConnection();
        
        // db.TraceSwitch.Level = TraceLevel.Info;
        // db.OnTraceConnection = (info) => 
        // {
        //     Console.WriteLine($"{info.TraceInfoStep}: {info.}");
        // };

        
        var result = await db
            .CreateTableAsync<TemporaryEntrance>("TemporaryEntrance");
        return result;
    }

    public async Task<BulkCopyRowsCopied> TaskInsert(IEnumerable<TemporaryEntrance> entrances)
    {
        await using var db = DbContext.CreateLinqToDBConnection();
        return await db.BulkCopyAsync(entrances);
    }

    public async Task<IEnumerable<string>> UpdateTemporaryEntranceWithCaveId()
    {
        await using var db = DbContext.CreateLinqToDBConnection();

        var result = await db.GetTable<TemporaryEntrance>()
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
            .Where(e => e.CaveId == null)
            .Select(e => e.Id).ToListAsyncLinqToDB();

        var deleteResult = await db.GetTable<TemporaryEntrance>()
            .Where(e => e.CaveId == null).DeleteAsync();
        
        return unassociatedEntrances;
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
        FROM {nameof(TemporaryEntrance)}";

        
        // execute raw sql
        await DbContext.Database.ExecuteSqlRawAsync(command);
    }

    public List<TemporaryEntrance> GetEntrancesById(string id)
    {
        return DbContext.CreateLinqToDBConnection().GetTable<TemporaryEntrance>().Where(e => e.Id == id).ToList();
    }

    public async Task DropTable()
    {
        await DbContext.CreateLinqToDBConnection().DropTableAsync<TemporaryEntrance>(tableName: "TemporaryEntrance");
    }
} 