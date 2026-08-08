using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Planarian.Tests;

public sealed class RevisionTenantFilterIntegrationTests(PostgresIntegrationFixture fixture)
    : IClassFixture<PostgresIntegrationFixture>
{
    [Fact]
    public async Task NormalWorkflowQueriesExposeOnlyActiveAccountRows()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(NormalWorkflowQueriesExposeOnlyActiveAccountRows));
        var a = await IntegrationTestData.SeedTenantAsync(database, 'a');
        var b = await IntegrationTestData.SeedTenantAsync(database, 'b');

        await using var db = database.CreateDbContext("user-a", a.AccountId);
        Assert.True(await db.CaveRevisions.AnyAsync(r => r.Id == a.RevisionId));
        Assert.False(await db.CaveRevisions.AnyAsync(r => r.Id == b.RevisionId));
        Assert.True(await db.CaveImportBatches.AnyAsync(r => r.Id == a.ImportBatchId));
        Assert.False(await db.CaveImportBatches.AnyAsync(r => r.Id == b.ImportBatchId));
        Assert.True(await db.CaveChangeRequests.AnyAsync(r => r.Id == a.ChangeRequestId));
        Assert.False(await db.CaveChangeRequests.AnyAsync(r => r.Id == b.ChangeRequestId));
        Assert.True(await db.CaveProposalVersions.AnyAsync(r => r.Id == a.ProposalVersionId));
        Assert.False(await db.CaveProposalVersions.AnyAsync(r => r.Id == b.ProposalVersionId));
        Assert.True(await db.CaveChangeRequestStagedFiles.AnyAsync(r => r.Id == a.StagedFileId));
        Assert.False(await db.CaveChangeRequestStagedFiles.AnyAsync(r => r.Id == b.StagedFileId));
    }

    [Fact]
    public async Task NullAccountWorkflowQueriesFailClosed()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(NullAccountWorkflowQueriesFailClosed));
        await IntegrationTestData.SeedTenantAsync(database, 'a');
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
        var a = await IntegrationTestData.SeedTenantAsync(database, 'a');
        var b = await IntegrationTestData.SeedTenantAsync(database, 'b');
        await using var db = database.CreateDbContext("trusted-a", a.AccountId);

        Assert.Equal([a.RevisionId], await db.CaveRevisions.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
        Assert.Equal([a.ImportBatchId], await db.CaveImportBatches.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
        Assert.Equal([a.ChangeRequestId], await db.CaveChangeRequests.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
        Assert.Equal([a.ProposalVersionId], await db.CaveProposalVersions.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
        Assert.Equal([a.StagedFileId], await db.CaveChangeRequestStagedFiles.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());

        Assert.DoesNotContain(b.RevisionId, await db.CaveRevisions.IgnoreQueryFilters()
            .Where(r => r.AccountId == a.AccountId).Select(r => r.Id).ToListAsync());
    }
}
