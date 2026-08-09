using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests;

public sealed class TenantAdversarialIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task AccountACannotSnapshotAccountBCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AccountACannotSnapshotAccountBCave));
        var a = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'a'); var b = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'b');
        await using var db = database.CreateDbContext("a",a.AccountId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CavePublishedSnapshotRepository(db,db.RequestUser).BuildAsync(b.CaveId));
        await AssertB(database,b,"Cave B");
    }

    [Fact]
    public async Task AccountACannotMutateAccountBCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AccountACannotMutateAccountBCave));
        var a = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'a'); var b = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'b');
        await using var db = database.CreateDbContext("a",a.AccountId);
        var reader = new CavePublishedSnapshotRepository(db,db.RequestUser); var coordinator = new CaveMutationRepository(db,db.RequestUser,reader);
        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.PublishExistingAsync(b.CaveId,null,CaveRevisionSource.ManagerEdit,CaveRevisionOperation.Update,c=>c.Name="attack"));
        await AssertB(database,b,"Cave B");
    }

    [Theory]
    [InlineData("PreviousRevisionId")]
    [InlineData("BaseRevisionId")]
    [InlineData("ApprovedRevisionId")]
    public async Task AccountACannotReferenceAccountBRevision(string relation)
    {
        await using var database = await fixture.CreateDatabaseAsync($"{nameof(AccountACannotReferenceAccountBRevision)}_{relation}");
        var a = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'a'); var b = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'b');
        await using var db = database.CreateDbContext("a",a.AccountId);
        if (relation == "PreviousRevisionId")
        {
            db.CaveRevisions.Add(new CaveRevision { Id=IdGenerator.Generate(), AccountId=a.AccountId, CaveId=a.CaveId, PreviousRevisionId=b.RevisionId, Source=CaveRevisionSource.ManagerEdit, Operation=CaveRevisionOperation.Update, SnapshotSchemaVersion=1, SnapshotJson="{\"schemaVersion\":1}" });
        }
        else
        {
            var request = await db.CaveChangeRequests.SingleAsync(r=>r.Id==a.ChangeRequestId);
            db.Entry(request).Property(relation).CurrentValue = b.RevisionId;
        }
        await Assert.ThrowsAnyAsync<Exception>(()=>db.SaveChangesAsync());
        await AssertB(database,b,"Cave B");
    }

    [Fact]
    public async Task AccountACannotAttachAccountBFileToStagedRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AccountACannotAttachAccountBFileToStagedRequest));
        var a=await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'a'); var b=await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'b');
        await using(var db=database.CreateDbContext("a",a.AccountId))
        {
            db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile { Id=IdGenerator.Generate(), AccountId=a.AccountId, ChangeRequestId=a.ChangeRequestId, FileId=b.FileId });
            await Assert.ThrowsAnyAsync<Exception>(()=>db.SaveChangesAsync());
        }
        await using var verify=database.CreateDbContext("b",b.AccountId);
        Assert.True(await verify.Files.AnyAsync(f=>f.Id==b.FileId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(f=>f.Id==b.StagedFileId));
    }

    [Fact]
    public async Task WorkflowFiltersHideAllAccountBRowsFromAccountA()
    {
        await using var database=await fixture.CreateDatabaseAsync(nameof(WorkflowFiltersHideAllAccountBRowsFromAccountA));
        var a=await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'a'); var b=await TestDataScenarios.CreatePublishedCaveScenarioAsync(database,'b');
        await using var db=database.CreateDbContext("a",a.AccountId);
        Assert.False(await db.CaveRevisions.AnyAsync(r=>r.Id==b.RevisionId)); Assert.False(await db.CaveImportBatches.AnyAsync(r=>r.Id==b.ImportBatchId));
        Assert.False(await db.CaveChangeRequests.AnyAsync(r=>r.Id==b.ChangeRequestId)); Assert.False(await db.CaveProposalVersions.AnyAsync(r=>r.Id==b.ProposalVersionId));
        Assert.False(await db.CaveChangeRequestStagedFiles.AnyAsync(r=>r.Id==b.StagedFileId));
        await AssertB(database,b,"Cave B");
    }

    private static async Task AssertB(PostgresTestDatabase database,PublishedCaveScenario b,string name)
    {
        await using var verify=database.CreateDbContext("b",b.AccountId);
        var cave=await verify.Caves.IgnoreQueryFilters().SingleAsync(c=>c.Id==b.CaveId); Assert.Equal(name,cave.Name);
        Assert.True(await verify.CaveRevisions.AnyAsync(r=>r.Id==b.RevisionId));
    }
}
