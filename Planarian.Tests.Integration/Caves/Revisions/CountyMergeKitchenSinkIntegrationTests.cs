using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CountyMergeKitchenSinkIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task MultiSourceMergeMovesEverySourceCavePreservesNumbersAndLeavesDestinationCaveUntouched()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MultiSourceMergeMovesEverySourceCavePreservesNumbersAndLeavesDestinationCaveUntouched));
        var first = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string sourceBId = "county00ay";
        const string destinationId = "county00az";
        await AddCountyAsync(database, first.AccountId, first.StateId, sourceBId, "Source B", "A98");
        await AddCountyAsync(database, first.AccountId, first.StateId, destinationId, "Destination", "A99");

        var sourceASecondCave = await CaveTestDataFactory.AddCaveAsync(
            database, first, "cave00000b", "Source A second", 2);
        var sourceASecond = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, sourceASecondCave, "revision0b");

        var sourceBTenant = TenantForCounty(first, sourceBId, "Source B", "A98");
        var sourceBFirstCave = await CaveTestDataFactory.AddCaveAsync(
            database, sourceBTenant, "cave00000c", "Source B first", 3);
        var sourceBFirst = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, sourceBFirstCave, "revision0c");
        var sourceBSecondCave = await CaveTestDataFactory.AddCaveAsync(
            database, sourceBTenant, "cave00000d", "Source B second", 4);
        var sourceBSecond = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, sourceBSecondCave, "revision0d");

        var destinationTenant = TenantForCounty(first, destinationId, "Destination", "A99");
        var destinationCave = await CaveTestDataFactory.AddCaveAsync(
            database, destinationTenant, "cave00000e", "Already destination", 10);
        var destination = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, destinationCave, "revision0e");

        await using (var admin = database.CreateDbContext("manager", first.AccountId))
            await IntegrationTestServices.For(admin).Account.MergeCounties(
                first.StateId,
                [first.CountyId, sourceBId, first.CountyId, destinationId, " ", sourceBId],
                destinationId, default);

        await using var verify = database.CreateDbContext("verify", first.AccountId);
        var caves = await verify.Caves.IgnoreQueryFilters()
            .Where(cave => new[]
            {
                first.CaveId, sourceASecond.CaveId, sourceBFirst.CaveId, sourceBSecond.CaveId, destination.CaveId
            }.Contains(cave.Id))
            .ToDictionaryAsync(cave => cave.Id);

        Assert.Equal(destinationId, caves[first.CaveId].CountyId);
        Assert.Equal(destinationId, caves[sourceASecond.CaveId].CountyId);
        Assert.Equal(destinationId, caves[sourceBFirst.CaveId].CountyId);
        Assert.Equal(destinationId, caves[sourceBSecond.CaveId].CountyId);
        Assert.Equal(destinationId, caves[destination.CaveId].CountyId);
        Assert.Equal(1, caves[first.CaveId].CountyNumber);
        Assert.Equal(2, caves[sourceASecond.CaveId].CountyNumber);
        Assert.Equal(3, caves[sourceBFirst.CaveId].CountyNumber);
        Assert.Equal(4, caves[sourceBSecond.CaveId].CountyNumber);
        Assert.Equal(10, caves[destination.CaveId].CountyNumber);

        Assert.NotEqual(first.RevisionId, caves[first.CaveId].CurrentRevisionId);
        Assert.NotEqual(sourceASecond.RevisionId, caves[sourceASecond.CaveId].CurrentRevisionId);
        Assert.NotEqual(sourceBFirst.RevisionId, caves[sourceBFirst.CaveId].CurrentRevisionId);
        Assert.NotEqual(sourceBSecond.RevisionId, caves[sourceBSecond.CaveId].CurrentRevisionId);
        Assert.Equal(destination.RevisionId, caves[destination.CaveId].CurrentRevisionId);
        Assert.Equal(4, await verify.CaveRevisions.CountAsync(revision =>
            revision.Source == CaveRevisionSource.ManagerEdit &&
            new[]
            {
                first.RevisionId, sourceASecond.RevisionId, sourceBFirst.RevisionId, sourceBSecond.RevisionId
            }.Contains(revision.PreviousRevisionId!)));
        Assert.True(await verify.Counties.AnyAsync(county => county.Id == first.CountyId));
        Assert.True(await verify.Counties.AnyAsync(county => county.Id == sourceBId));
    }

    [Fact]
    public async Task AffectingCountyMergeMakesPendingProposalStaleWithoutRewritingProposalVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AffectingCountyMergeMakesPendingProposalStaleWithoutRewritingProposalVersion));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string destinationId = "county00az";
        await AddCountyAsync(database, cave.AccountId, cave.StateId, destinationId, "Destination", "A99");
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor"))
        {
            var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Pending county proposal");
            requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId, values, cave.RevisionId, default);
            versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(contributor.Db, requestId);
        }

        await using (var admin = database.CreateDbContext("manager", cave.AccountId))
            await IntegrationTestServices.For(admin).Account.MergeCounties(
                cave.StateId, [cave.CountyId], destinationId, default);

        await using (var reviewer = await CaveTestActor.CreateAsync(database, cave.AccountId, "reviewer"))
        {
            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            Assert.True(detail.Request.IsStale);
            await Assert.ThrowsAsync<CaveRevisionConflictException>(
                () => reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default));
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var version = await verify.CaveProposalVersions.SingleAsync(row => row.Id == versionId);
        Assert.Equal(cave.RevisionId, version.BaseRevisionId);
        Assert.Contains(cave.CountyId, version.ProposalJson);
        Assert.DoesNotContain(destinationId, version.ProposalJson);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId)).Status);
    }

    [Fact]
    public async Task CaveWriterWinsThenWaitingCountyMergeRechecksDestinationCollisionAndMutatesNothing()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveWriterWinsThenWaitingCountyMergeRechecksDestinationCollisionAndMutatesNothing));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string writerCountyId = "county00ay";
        const string destinationId = "county00az";
        await AddCountyAsync(database, source.AccountId, source.StateId, writerCountyId, "Writer source", "A98");
        await AddCountyAsync(database, source.AccountId, source.StateId, destinationId, "Destination", "A99");
        var writerTenant = TenantForCounty(source, writerCountyId, "Writer source", "A98");
        var writerCaveRow = await CaveTestDataFactory.AddCaveAsync(
            database, writerTenant, "cave00000w", "Writer cave", source.CountyNumber);
        var writerCave = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, writerCaveRow, "revision0w");
        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantManagerAsync(database, writerCave, "writer");

        var values = CaveChangeRequestTestSupport.PublishableValues(writerCave, quality.Id, "Writer moved");
        values.CountyId = destinationId;
        values.CountyNumber = source.CountyNumber;

        var pause = new HoldCountyLockInterceptor("for update");
        await using var writer = database.CreateDbContext("writer", source.AccountId, pause);
        await CavePermissions.AuthenticateAsync(writer, source.AccountId);
        var write = IntegrationTestServices.For(writer).Caves.AddCave(values, default);
        await pause.LockAcquired.Task.WaitAsync(TimeSpan.FromSeconds(15));

        await using var admin = database.CreateDbContext("manager", source.AccountId);
        await admin.Database.OpenConnectionAsync();
        var adminPid = await PostgresLockAssertions.BackendPidAsync(admin);
        var merge = IntegrationTestServices.For(admin).Account.MergeCounties(
            source.StateId, [source.CountyId], destinationId, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, source.AccountId, adminPid, merge);

        pause.Resume.TrySetResult();
        await write;
        var failure = await Assert.ThrowsAsync<ApiException>(() => merge);
        Assert.Equal(400, failure.StatusCode);
        Assert.Contains($"County Number {source.CountyNumber}", failure.Message);

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        var persistedSource = await verify.Caves.IgnoreQueryFilters().SingleAsync(cave => cave.Id == source.CaveId);
        var persistedWriter = await verify.Caves.IgnoreQueryFilters().SingleAsync(cave => cave.Id == writerCave.CaveId);
        Assert.Equal(source.CountyId, persistedSource.CountyId);
        Assert.Equal(source.RevisionId, persistedSource.CurrentRevisionId);
        Assert.Equal(destinationId, persistedWriter.CountyId);
        Assert.NotEqual(writerCave.RevisionId, persistedWriter.CurrentRevisionId);
        Assert.False(await verify.CaveRevisions.AnyAsync(revision =>
            revision.CaveId == source.CaveId && revision.PreviousRevisionId == source.RevisionId));
    }

    [Fact]
    public async Task UninitializedCaveMergeFailsClosedAndRollsBackCountyRemap()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(UninitializedCaveMergeFailsClosedAndRollsBackCountyRemap));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string destinationId = "county00az";
        await AddCountyAsync(database, cave.AccountId, cave.StateId, destinationId, "Destination", "A99");
        await using (var seed = database.CreateDbContext("legacy", cave.AccountId))
        {
            (await seed.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId))
                .CurrentRevisionId = null;
            await seed.SaveChangesAsync();
        }

        await using (var admin = database.CreateDbContext("manager", cave.AccountId))
        {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                IntegrationTestServices.For(admin).Account.MergeCounties(
                    cave.StateId, [cave.CountyId], destinationId, default));
            Assert.Contains("Revision history rollout must complete", failure.Message, StringComparison.Ordinal);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CountyId, persisted.CountyId);
        Assert.Null(persisted.CurrentRevisionId);
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == cave.CaveId).ToListAsync());
    }

    [Fact]
    public async Task MergeRejectsBlankOrDestinationOnlySourceListWithoutMutation()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MergeRejectsBlankOrDestinationOnlySourceListWithoutMutation));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string destinationId = "county00az";
        await AddCountyAsync(database, cave.AccountId, cave.StateId, destinationId, "Destination", "A99");

        await using (var admin = database.CreateDbContext("manager", cave.AccountId))
        {
            var failure = await Assert.ThrowsAsync<ApiException>(() =>
                IntegrationTestServices.For(admin).Account.MergeCounties(
                    cave.StateId, [destinationId, " ", destinationId], destinationId, default));
            Assert.Equal(400, failure.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CountyId, persisted.CountyId);
        Assert.Equal(cave.RevisionId, persisted.CurrentRevisionId);
    }

    private static async Task AddCountyAsync(PostgresTestDatabase database, string accountId, string stateId,
        string countyId, string name, string displayId)
    {
        await using var db = database.CreateDbContext("county-seed", accountId);
        db.Counties.Add(new County
            { Id = countyId, AccountId = accountId, StateId = stateId, Name = name, DisplayId = displayId });
        await db.SaveChangesAsync();
    }

    private static AccountCountyTestData TenantForCounty(PublishedCaveTestData source, string countyId,
        string countyName, string displayId) =>
        new(source.AccountId, source.StateId, source.StateName, source.StateAbbreviation,
            countyId, countyName, displayId);
}
