using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
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
        var accountA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var accountB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');

        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);

        await Assert.ThrowsAsync<InvalidOperationException>(() => snapshots.BuildAsync(accountB.CaveId));
        await AssertAccountBIsUnchangedAsync(database, accountB);
    }

    [Fact]
    public async Task AccountACannotMutateAccountBCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AccountACannotMutateAccountBCave));
        var accountA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var accountB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');

        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);
        var repository = new CaveMutationRepository(db, db.RequestUser, snapshots);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PublishExistingAsync(
            accountB.CaveId,
            expectedRevisionId: null,
            CaveRevisionSource.ManagerEdit,
            CaveRevisionOperation.Update,
            cave => cave.Name = "attack"));
        await AssertAccountBIsUnchangedAsync(database, accountB);
    }

    [Theory]
    [InlineData("PreviousRevisionId")]
    [InlineData("BaseRevisionId")]
    [InlineData("ApprovedRevisionId")]
    public async Task AccountACannotReferenceAccountBRevision(string relation)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(AccountACannotReferenceAccountBRevision)}_{relation}");
        var accountA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var accountB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var requestA = await TestDataBuilder.CreateChangeRequestAsync(database, accountA);

        await using var db = database.CreateDbContext("a", accountA.AccountId);
        if (relation == "PreviousRevisionId")
        {
            db.CaveRevisions.Add(new CaveRevision
            {
                Id = IdGenerator.Generate(),
                AccountId = accountA.AccountId,
                CaveId = accountA.CaveId,
                PreviousRevisionId = accountB.RevisionId,
                Source = CaveRevisionSource.ManagerEdit,
                Operation = CaveRevisionOperation.Update,
                SnapshotSchemaVersion = 1,
                SnapshotJson = "{\"schemaVersion\":1}"
            });
        }
        else
        {
            var request = await db.CaveChangeRequests.SingleAsync(row => row.Id == requestA.ChangeRequestId);
            db.Entry(request).Property(relation).CurrentValue = accountB.RevisionId;
        }

        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        await AssertAccountBIsUnchangedAsync(database, accountB);
    }

    [Fact]
    public async Task AccountACannotAttachAccountBFileToStagedRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(AccountACannotAttachAccountBFileToStagedRequest));
        var accountA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var accountB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var requestA = await TestDataBuilder.CreateChangeRequestAsync(database, accountA);
        var reviewB = await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, accountB);

        await using (var db = database.CreateDbContext("a", accountA.AccountId))
        {
            db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
            {
                Id = IdGenerator.Generate(),
                AccountId = accountA.AccountId,
                ChangeRequestId = requestA.ChangeRequestId,
                FileId = reviewB.FileId
            });
            await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());
        }

        await using var verify = database.CreateDbContext("b", accountB.AccountId);
        Assert.True(await verify.Files.AnyAsync(file => file.Id == reviewB.FileId));
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(file => file.Id == reviewB.StagedFileId));
    }

    [Fact]
    public async Task WorkflowFiltersHideAllAccountBRowsFromAccountA()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(WorkflowFiltersHideAllAccountBRowsFromAccountA));
        var accountA = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var accountB = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var batchB = await TestDataBuilder.AddImportBatchAsync(database, accountB);
        var reviewB = await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, accountB);

        await using var db = database.CreateDbContext("a", accountA.AccountId);

        Assert.False(await db.CaveRevisions.AnyAsync(row => row.Id == accountB.RevisionId));
        Assert.False(await db.CaveImportBatches.AnyAsync(row => row.Id == batchB));
        Assert.False(await db.CaveChangeRequests.AnyAsync(row => row.Id == reviewB.ChangeRequestId));
        Assert.False(await db.CaveProposalVersions.AnyAsync(row => row.Id == reviewB.ProposalVersionId));
        Assert.False(await db.CaveChangeRequestStagedFiles.AnyAsync(row => row.Id == reviewB.StagedFileId));
        await AssertAccountBIsUnchangedAsync(database, accountB);
    }

    private static async Task AssertAccountBIsUnchangedAsync(
        PostgresTestDatabase database,
        PublishedCaveTestData accountB)
    {
        await using var verify = database.CreateDbContext("b", accountB.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == accountB.CaveId);
        Assert.Equal(accountB.CaveName, cave.Name);
        Assert.True(await verify.CaveRevisions.AnyAsync(row => row.Id == accountB.RevisionId));
    }
}
