using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Planarian.Tests;

public sealed class RevisionTenantFilterIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task NormalWorkflowQueriesExposeOnlyActiveAccountRows()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(NormalWorkflowQueriesExposeOnlyActiveAccountRows));
        var a = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var b = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var aBatch = await TestDataBuilder.AddImportBatchAsync(database, a);
        var bBatch = await TestDataBuilder.AddImportBatchAsync(database, b);
        var aReview = await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, a);
        var bReview = await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, b);

        await using var db = database.CreateDbContext("user-a", a.AccountId);
        Assert.True(await db.CaveRevisions.AnyAsync(r => r.Id == a.RevisionId));
        Assert.False(await db.CaveRevisions.AnyAsync(r => r.Id == b.RevisionId));
        Assert.True(await db.CaveImportBatches.AnyAsync(r => r.Id == aBatch));
        Assert.False(await db.CaveImportBatches.AnyAsync(r => r.Id == bBatch));
        Assert.True(await db.CaveChangeRequests.AnyAsync(r => r.Id == aReview.ChangeRequestId));
        Assert.False(await db.CaveChangeRequests.AnyAsync(r => r.Id == bReview.ChangeRequestId));
        Assert.True(await db.CaveProposalVersions.AnyAsync(r => r.Id == aReview.ProposalVersionId));
        Assert.False(await db.CaveProposalVersions.AnyAsync(r => r.Id == bReview.ProposalVersionId));
        Assert.True(await db.CaveChangeRequestStagedFiles.AnyAsync(r => r.Id == aReview.StagedFileId));
        Assert.False(await db.CaveChangeRequestStagedFiles.AnyAsync(r => r.Id == bReview.StagedFileId));
    }

    [Fact]
    public async Task NullAccountWorkflowQueriesFailClosed()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(NullAccountWorkflowQueriesFailClosed));
        var cave = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await TestDataBuilder.AddImportBatchAsync(database, cave);
        await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, cave);
        await using var db = database.CreateDbContext("anonymous", null);
        Assert.Empty(await db.CaveRevisions.ToListAsync());
        Assert.Empty(await db.CaveImportBatches.ToListAsync());
        Assert.Empty(await db.CaveChangeRequests.ToListAsync());
        Assert.Empty(await db.CaveProposalVersions.ToListAsync());
        Assert.Empty(await db.CaveChangeRequestStagedFiles.ToListAsync());
    }

    [Fact]
    public async Task TrustedBypassRestoresExplicitAccountPredicate()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(TrustedBypassRestoresExplicitAccountPredicate));
        var a = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var b = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        var aBatch = await TestDataBuilder.AddImportBatchAsync(database, a);
        await TestDataBuilder.AddImportBatchAsync(database, b);
        var aReview = await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, a);
        await TestDataBuilder.CreatePendingReviewWithStagedFileAsync(database, b);
        await using var db = database.CreateDbContext("trusted-a", a.AccountId);

        Assert.Equal([a.RevisionId], await db.CaveRevisions.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
        Assert.Equal([aBatch], await db.CaveImportBatches.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
        Assert.Equal([aReview.ChangeRequestId], await db.CaveChangeRequests.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
        Assert.Equal([aReview.ProposalVersionId], await db.CaveProposalVersions.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
        Assert.Equal([aReview.StagedFileId], await db.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());

        Assert.DoesNotContain(b.RevisionId, await db.CaveRevisions.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
    }
}
