using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared.Helpers;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CountyMergeRevisionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task MergeMovesCavesWithoutRenumberingAndPublishesManagerRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MergeMovesCavesWithoutRenumberingAndPublishesManagerRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string destinationId = "county00az";
        await AddCountyAsync(database, cave.AccountId, cave.StateId, destinationId, "Destination", "A99");

        await using (var admin = database.CreateDbContext("manager", cave.AccountId))
            await IntegrationTestServices.For(admin).Account.MergeCounties(
                cave.StateId, [cave.CountyId], destinationId, default);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(destinationId, persisted.CountyId);
        Assert.Equal(cave.CountyNumber, persisted.CountyNumber);
        Assert.Equal("manager", persisted.ModifiedByUserId);
        Assert.NotNull(persisted.ModifiedOn);
        Assert.True(await verify.Counties.AnyAsync(row => row.Id == cave.CountyId));

        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == persisted.CurrentRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, revision.Source);
        Assert.Equal(CaveRevisionOperation.Update, revision.Operation);
        Assert.Equal(cave.RevisionId, revision.PreviousRevisionId);
        var before = CaveSnapshotJson.Deserialize((await verify.CaveRevisions
            .SingleAsync(row => row.Id == cave.RevisionId)).SnapshotJson, 1);
        var after = CaveSnapshotJson.Deserialize(revision.SnapshotJson, 1);
        Assert.Equal(cave.CountyId, before.County.Id);
        Assert.Equal(destinationId, after.County.Id);
        Assert.Equal(cave.CountyNumber, after.CountyNumber);
    }

    [Fact]
    public async Task CountyTableCountsAllTenantCavesOutsideActorVisibilityFilter()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyTableCountsAllTenantCavesOutsideActorVisibilityFilter));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');

        await using var reader = database.CreateDbContext("tableread", cave.AccountId);
        Assert.Equal(0, await reader.Caves.CountAsync());

        var counties = (await IntegrationTestServices.For(reader).Account
            .GetCountiesForTable(cave.StateId, default)).ToList();
        var county = Assert.Single(counties);
        Assert.Equal(cave.CountyId, county.TagTypeId);
        Assert.Equal(1, county.Occurrences);
        Assert.False(county.IsUserModifiable);
    }

    [Fact]
    public async Task MergeRejectsCountyNumberCollisionWithDestinationWithoutPartialMutation()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MergeRejectsCountyNumberCollisionWithDestinationWithoutPartialMutation));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string destinationId = "county00az";
        await AddCountyAsync(database, source.AccountId, source.StateId, destinationId, "Destination", "A99");
        var destinationTenant = TenantForCounty(source, destinationId, "Destination", "A99");
        var destinationCave = await CaveTestDataFactory.AddCaveAsync(
            database, destinationTenant, "cave00000z", "Destination Cave", source.CountyNumber);
        var publishedDestination = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, destinationCave, "revision0z");

        await using (var admin = database.CreateDbContext("manager", source.AccountId))
        {
            var failure = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(admin).Account
                .MergeCounties(source.StateId, [source.CountyId], destinationId, default));
            Assert.Equal(400, failure.StatusCode);
            Assert.Contains($"County Number {source.CountyNumber}", failure.Message);
        }

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        Assert.Equal(source.CountyId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == source.CaveId)).CountyId);
        Assert.Equal(destinationId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == publishedDestination.CaveId)).CountyId);
        Assert.Equal(source.RevisionId, await CurrentRevisionAsync(verify, source.CaveId));
        Assert.Equal(publishedDestination.RevisionId,
            await CurrentRevisionAsync(verify, publishedDestination.CaveId));
    }

    [Fact]
    public async Task MergeRejectsSameCountyNumberAcrossTwoSources()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MergeRejectsSameCountyNumberAcrossTwoSources));
        var first = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string secondSourceId = "county00ay";
        const string destinationId = "county00az";
        await AddCountyAsync(database, first.AccountId, first.StateId, secondSourceId, "Second Source", "A98");
        await AddCountyAsync(database, first.AccountId, first.StateId, destinationId, "Destination", "A99");
        var secondTenant = TenantForCounty(first, secondSourceId, "Second Source", "A98");
        var secondCave = await CaveTestDataFactory.AddCaveAsync(
            database, secondTenant, "cave00000y", "Second Source Cave", first.CountyNumber);
        var second = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, secondCave, "revision0y");

        await using (var admin = database.CreateDbContext("manager", first.AccountId))
        {
            var failure = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(admin).Account
                .MergeCounties(first.StateId, [first.CountyId, secondSourceId], destinationId, default));
            Assert.Equal(400, failure.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", first.AccountId);
        Assert.Equal(first.CountyId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == first.CaveId)).CountyId);
        Assert.Equal(secondSourceId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == second.CaveId)).CountyId);
        Assert.Equal(first.RevisionId, await CurrentRevisionAsync(verify, first.CaveId));
        Assert.Equal(second.RevisionId, await CurrentRevisionAsync(verify, second.CaveId));
    }

    [Fact]
    public async Task MergeRejectsCountiesOutsideSelectedStateOrCurrentAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MergeRejectsCountiesOutsideSelectedStateOrCurrentAccount));
        var current = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string otherStateId = "state0000z";
        const string otherStateCountyId = "county00az";
        await GlobalStateTestData.AddAsync(database, otherStateId, "State Z", "ZZ");
        await AddCountyAsync(database, current.AccountId, otherStateId, otherStateCountyId, "Other State", "Z01");
        var otherAccount = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');

        await using var admin = database.CreateDbContext("manager", current.AccountId);
        var crossState = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(admin).Account
            .MergeCounties(current.StateId, [current.CountyId], otherStateCountyId, default));
        Assert.Equal(400, crossState.StatusCode);
        var crossAccount = await Assert.ThrowsAsync<ApiException>(() => IntegrationTestServices.For(admin).Account
            .MergeCounties(current.StateId, [otherAccount.CountyId], current.CountyId, default));
        Assert.Equal(400, crossAccount.StatusCode);

        await using var verify = database.CreateDbContext("verify", current.AccountId);
        Assert.Equal(current.CountyId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == current.CaveId)).CountyId);
        Assert.Equal(current.RevisionId, await CurrentRevisionAsync(verify, current.CaveId));
    }

    [Fact]
    public async Task LateRevisionFailureRollsBackCountyRemapAndRevisionPointer()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LateRevisionFailureRollsBackCountyRemapAndRevisionPointer));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string destinationId = "county00az";
        await AddCountyAsync(database, cave.AccountId, cave.StateId, destinationId, "Destination", "A99");
        var fault = new FailAtRevisionPublicationInterceptor();

        await using (var admin = database.CreateDbContext("manager", cave.AccountId, fault))
            await Assert.ThrowsAsync<DbUpdateException>(() => IntegrationTestServices.For(admin).Account
                .MergeCounties(cave.StateId, [cave.CountyId], destinationId, default));

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var persisted = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.Equal(cave.CountyId, persisted.CountyId);
        Assert.Equal(cave.RevisionId, persisted.CurrentRevisionId);
        Assert.Empty(await verify.CaveRevisions
            .Where(row => row.PreviousRevisionId == cave.RevisionId).ToListAsync());
    }

    private static async Task AddCountyAsync(PostgresTestDatabase database, string accountId, string stateId,
        string countyId, string name, string displayId)
    {
        await using var db = database.CreateDbContext("county-seed", accountId);
        db.Counties.Add(new County
        {
            Id = countyId,
            AccountId = accountId,
            StateId = stateId,
            Name = name,
            DisplayId = displayId
        });
        await db.SaveChangesAsync();
    }

    private static AccountCountyTestData TenantForCounty(PublishedCaveTestData source, string countyId,
        string countyName, string displayId) =>
        new(source.AccountId, source.StateId, source.StateName, source.StateAbbreviation,
            countyId, countyName, displayId);

    private static Task<string?> CurrentRevisionAsync(
        Planarian.Model.Database.PlanarianDbContext db, string caveId) =>
        db.Caves.IgnoreQueryFilters().Where(row => row.Id == caveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
}
