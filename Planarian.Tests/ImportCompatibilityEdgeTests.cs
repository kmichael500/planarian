using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportCompatibilityEdgeTests(PostgresIntegrationFixture fixture) : IClassFixture<PostgresIntegrationFixture>
{
    [Fact]
    public async Task CaveNameOverModelMaximumIsRejected()
    {
        await using var d = await fixture.CreateDatabaseAsync(nameof(CaveNameOverModelMaximumIsRejected));
        var t = await IntegrationTestData.SeedTenantAsync(d, 'a');
        await using var db = d.CreateDbContext("a", t.AccountId);
        var planner = new CaveImportPlanner(db, db.RequestUser);
        var name = new string('x', PropertyLength.Name + 1);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader + "\n" +
            $"{name},County A,A01,50,AA,,,,100,20,5,1,,,,,,,,false,,\n");
        await Assert.ThrowsAnyAsync<Exception>(() => planner.PlanAsync(csv, false));
    }

    [Fact]
    public async Task EntranceNameOverModelMaximumIsRejected()
    {
        await using var d = await fixture.CreateDatabaseAsync(nameof(EntranceNameOverModelMaximumIsRejected));
        var t = await IntegrationTestData.SeedTenantAsync(d, 'a');
        await using var db = d.CreateDbContext("a", t.AccountId);
        var planner = new EntranceImportPlanner(db, db.RequestUser);
        var name = new string('x', PropertyLength.Name + 1);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader + "\n" +
            $"{name},A01,1,true,35,-86,500,Survey Grade,0,,,,,,,\n");
        await Assert.ThrowsAnyAsync<Exception>(() => planner.PlanAsync(csv, false));
    }

    [Fact]
    public async Task DuplicateEntranceTagInputProducesOneSemanticAssociation()
    {
        await using var d = await fixture.CreateDatabaseAsync(nameof(DuplicateEntranceTagInputProducesOneSemanticAssociation));
        var t = await IntegrationTestData.SeedTenantAsync(d, 'a');
        await using var db = d.CreateDbContext("a", t.AccountId);
        var planner = new EntranceImportPlanner(db, db.RequestUser);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader + "\n" +
            "Duplicate,A01,1,true,35,-86,500,Survey Grade,0,\"Open,open\",,,,,,\n");
        var plan = await planner.PlanAsync(csv, false);
        var statuses = Assert.Single(plan.Entrances).Tags.Where(t => t.Role == EntranceImportTagRole.Status).ToList();
        Assert.Single(statuses);
        var creation = Assert.Single(plan.TagCreations,
            tag => tag.Key == TagTypeKeyConstant.EntranceStatus);
        Assert.Equal("Open", creation.Name);
        Assert.Equal(creation.Id, statuses[0].TagTypeId);
    }

    [Fact]
    public async Task CaveSyncDeleteReturnsBlobCleanupOnlyAfterRelationalCommit()
    {
        await using var d = await fixture.CreateDatabaseAsync(nameof(CaveSyncDeleteReturnsBlobCleanupOnlyAfterRelationalCommit));
        var t = await IntegrationTestData.SeedTenantAsync(d, 'a');
        await using (var seed = d.CreateDbContext("a", t.AccountId))
        {
            var file = await seed.Files.SingleAsync(f => f.Id == t.FileId);
            file.CaveId = t.CaveId;
            file.BlobKey = "delete-after-commit";
            file.BlobContainer = "test";
            await seed.SaveChangesAsync();
        }
        await using var db = d.CreateDbContext("a", t.AccountId);
        var planner = new CaveImportPlanner(db, db.RequestUser);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.CaveHeader + "\n" +
            "Replacement,Replacement County,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,\n");
        var plan = await planner.PlanAsync(csv, true);
        Assert.Contains(plan.Deletions, x => x.CaveId == t.CaveId);
        var reader = new CavePublishedSnapshotReader(db, db.RequestUser);
        var result = await new CaveImportExecutor(db, db.RequestUser, reader,
            new ImportRevisionPublisher(db, db.RequestUser)).ExecuteAsync(plan, "delete.csv");
        Assert.False(await db.Caves.IgnoreQueryFilters().AnyAsync(c => c.Id == t.CaveId));
        Assert.Contains(result.BlobDeletes, b => b.BlobKey == "delete-after-commit" && b.BlobContainer == "test");
    }

    [Fact]
    public async Task DefaultEntranceTagIsReusable()
    {
        await using var d = await fixture.CreateDatabaseAsync(nameof(DefaultEntranceTagIsReusable));
        var t = await IntegrationTestData.SeedTenantAsync(d, 'a');
        string id;
        await using (var seed = d.CreateDbContext("a", t.AccountId))
        {
            var tag = new TagType("Default Status", TagTypeKeyConstant.EntranceStatus)
                { Id = IdGenerator.Generate(), AccountId = null, IsDefault = true };
            seed.TagTypes.Add(tag);
            await seed.SaveChangesAsync();
            id = tag.Id;
        }
        await using var db = d.CreateDbContext("a", t.AccountId);
        var planner = new EntranceImportPlanner(db, db.RequestUser);
        await using var csv = ImportDryRunIntegrationTests.CsvStream(ImportDryRunIntegrationTests.EntranceHeader + "\n" +
            "Default,A01,1,true,35,-86,500,Survey Grade,0,Default Status,,,,,,\n");
        var plan = await planner.PlanAsync(csv, false);
        Assert.Contains(Assert.Single(plan.Entrances).Tags, x => x.TagTypeId == id);
        Assert.DoesNotContain(plan.TagCreations, x => x.Name == "Default Status");
    }
}
