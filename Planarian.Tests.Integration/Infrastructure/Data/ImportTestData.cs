using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure.Data;
internal static class ImportTestData
{
    public static async Task<string> AddImportBatchAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave)
    {
        var id = $"batch0000{cave.AccountId[^1]}";
        await using var db = database.CreateDbContext("batch-seed", cave.AccountId);
        db.CaveImportBatches.Add(new CaveImportBatch
        {
            Id = id,
            AccountId = cave.AccountId,
            SourceFileName = $"seed-{cave.AccountId[^1]}.csv",
            SyncExisting = false,
            Kind = CaveImportKind.CaveCsv,
            SourceRecordCount = 1
        });
        await db.SaveChangesAsync();
        return id;
    }
}
