using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveRevisionQueryRepositoryIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ListOrdersRevisionsAndIdentifiesCurrentWithinTenant()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(ListOrdersRevisionsAndIdentifiesCurrentWithinTenant));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var other = await TestDataBuilder.CreatePublishedCaveAsync(database, 'b');
        await GrantViewAsync(database, tenant, "user-a");
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
        Assert.Null(await repository.ListAsync(other.CaveId, default));
        Assert.Null(await repository.GetAsync(tenant.CaveId, other.RevisionId, default));
    }

    private static async Task GrantViewAsync(PostgresTestDatabase database, PublishedCaveTestData cave, string userId)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
        const string permissionId = "vIeWPz9a00";
        if (!await db.Permissions.AnyAsync(row => row.Id == permissionId))
        {
            db.Permissions.Add(new Permission
            {
                Id = permissionId, Key = "View", Name = "View", Description = "View Caves",
                PermissionType = "Cave"
            });
            await db.SaveChangesAsync();
        }
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }
}
