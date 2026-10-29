using Microsoft.EntityFrameworkCore;
using Planarian.Modules.Caves.Repositories;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveCountyNumberRepositoryIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task FirstAvailableIncludesHiddenAndArchivedCaves()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FirstAvailableIncludesHiddenAndArchivedCaves));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var archived = await CaveTestDataFactory.AddCaveAsync(
            database, source, "cave00002a", "Archived Hidden Cave", 2);
        await CaveTestDataFactory.AddCaveAsync(
            database, source, "cave00003a", "Hidden Cave Three", 3);

        await using (var seed = database.CreateDbContext("archive-seed", source.AccountId))
        {
            var cave = await seed.Caves.IgnoreQueryFilters()
                .SingleAsync(row => row.AccountId == source.AccountId && row.Id == archived.CaveId);
            cave.IsArchived = true;
            await seed.SaveChangesAsync();
        }

        await CavePermissions.GrantViewAsync(database, source, "viewer");
        await using var actor = await CaveTestActor.CreateAsync(database, source.AccountId, "viewer");
        Assert.Single(await actor.Db.Caves.Where(cave => cave.CountyId == source.CountyId).ToListAsync());

        var repository = new CaveRepository(actor.Db, actor.Db.RequestUser);
        Assert.Equal(4, await repository.GetNewDisplayId(source.CountyId, useFirstAvailableCountyNumber: true));
        Assert.True(await repository.IsCountyNumberInUse(source.CountyId, 2));
        Assert.True(await repository.IsCountyNumberInUse(source.CountyId, 3));
    }

    [Fact]
    public async Task IgnoreQueryFiltersCountyNumberLookupRemainsAccountQualified()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(IgnoreQueryFiltersCountyNumberLookupRemainsAccountQualified));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        await CaveTestDataFactory.AddCaveAsync(
            database, accountB, "cave00002b", "Foreign Cave Two", 2);

        await using var actor = await CaveTestActor.CreateAsync(database, accountA.AccountId, "account-a-user");
        var repository = new CaveRepository(actor.Db, actor.Db.RequestUser);

        Assert.Equal(1, await repository.GetNewDisplayId(accountB.CountyId));
        Assert.Equal(1, await repository.GetNewDisplayId(accountB.CountyId, useFirstAvailableCountyNumber: true));
        Assert.False(await repository.IsCountyNumberInUse(accountB.CountyId, accountB.CountyNumber));
        Assert.False(await repository.IsCountyNumberInUse(accountB.CountyId, 2));
    }
}
