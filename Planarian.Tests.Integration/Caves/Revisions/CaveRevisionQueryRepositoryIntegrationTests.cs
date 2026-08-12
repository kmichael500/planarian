using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveRevisionQueryRepositoryIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ListOrdersRevisionsByExplicitChainAndIdentifiesCurrentWithinTenant()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ListOrdersRevisionsByExplicitChainAndIdentifiesCurrentWithinTenant));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var other = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        await CavePermissions.GrantViewAsync(database, tenant, "user-a");
        const string secondId = "revision1a";

        await using (var seed = database.CreateDbContext("user-a", tenant.AccountId))
        {
            var first = await seed.CaveRevisions.SingleAsync(row => row.Id == tenant.RevisionId);
            var snapshot = CaveSnapshotJson.Deserialize(first.SnapshotJson, first.SnapshotSchemaVersion) with
            {
                Name = "Changed cave"
            };
            seed.CaveRevisions.Add(new CaveRevision
            {
                Id = secondId,
                AccountId = tenant.AccountId,
                CaveId = tenant.CaveId,
                PreviousRevisionId = first.Id,
                Source = CaveRevisionSource.ManagerEdit,
                Operation = CaveRevisionOperation.Update,
                SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
            });
            await seed.SaveChangesAsync();
            first.CreatedOn = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
            (await seed.CaveRevisions.SingleAsync(row => row.Id == secondId)).CreatedOn =
                first.CreatedOn.AddHours(-1);
            var cave = await seed.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
            cave.CurrentRevisionId = secondId;
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext("user-a", tenant.AccountId);
        var repository = new CaveRevisionQueryRepository(db, db.RequestUser);
        var result = await repository.ListAsync(tenant.CaveId, default);

        Assert.NotNull(result);
        Assert.Equal(secondId, result.Value.CurrentRevisionId);
        Assert.Equal([tenant.RevisionId, secondId], result.Value.Item2.Select(row => row.Revision.Id));
        var comparison = await new CaveRevisionService(new CaveRepository(db, db.RequestUser), repository)
            .CompareAsync(tenant.CaveId, secondId, default);
        Assert.Equal(tenant.RevisionId, comparison.PreviousRevision!.Id);
        Assert.Equal("Cave A", comparison.Previous!.Name);
        Assert.Equal("Changed cave", comparison.Current.Name);
        Assert.Null(await repository.ListAsync(other.CaveId, default));
        Assert.Null(await repository.GetAsync(tenant.CaveId, other.RevisionId, default));
    }

    [Fact]
    public async Task ListRejectsACyclicRevisionChain()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(ListRejectsACyclicRevisionChain));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "user-a");
        await using (var seed = database.CreateDbContext("user-a", tenant.AccountId))
        {
            var revision = await seed.CaveRevisions.SingleAsync(row => row.Id == tenant.RevisionId);
            revision.PreviousRevisionId = revision.Id;
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext("user-a", tenant.AccountId);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CaveRevisionQueryRepository(db, db.RequestUser).ListAsync(tenant.CaveId, default));
    }

}
