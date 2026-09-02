using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;
using Xunit;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class CavePermissionHierarchyIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData("cave")]
    [InlineData("county")]
    [InlineData("state")]
    [InlineData("account")]
    public async Task ManagerScopeAlsoGrantsView(string scope)
    {
        await using var database = await fixture.CreateDatabaseAsync($"manager_implies_view_{scope}");
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        const string userId = "manager-only";
        await GrantScopedAsync(database, cave, userId, scope, PermissionKey.Manager, "mAnageR000");

        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, userId);
        Assert.True(await actor.Db.RequestUser.HasCavePermission(PermissionKey.View, false));
        Assert.True(await actor.Db.RequestUser.HasCavePermission(
            PermissionKey.View, cave.CaveId, cave.CountyId, cave.StateId, false));
    }

    [Fact]
    public async Task ViewDoesNotGrantManager()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(ViewDoesNotGrantManager));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, cave, "view-only");
        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, "view-only");

        Assert.True(await actor.Db.RequestUser.HasCavePermission(
            PermissionKey.View, cave.CaveId, cave.CountyId, cave.StateId, false));
        Assert.False(await actor.Db.RequestUser.HasCavePermission(
            PermissionKey.Manager, cave.CaveId, cave.CountyId, cave.StateId, false));
    }

    [Fact]
    public async Task DestinationCountyManagerCannotDirectlyEditAViewOnlyCaveIntoThatCounty()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DestinationCountyManagerCannotDirectlyEditAViewOnlyCaveIntoThatCounty));
        var (cave, locationTagId) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var destination = await AddDestinationCountyAsync(database, cave);
        const string userId = "destination-manager";
        await CavePermissions.GrantViewAsync(database, cave, userId);
        await GrantScopedAsync(database, cave with { CountyId = destination.Id }, userId,
            "county", PermissionKey.Manager, "mAnageR000");

        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, userId);
        var values = PublishableValues(cave, locationTagId, "Unauthorized move");
        values.CountyId = destination.Id;
        values.CountyNumber = 2;

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            actor.Services.Caves.AddCave(values, default));
        Assert.Equal(403, failure.StatusCode);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(cave.CountyId, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == cave.CaveId)).CountyId);
    }

    [Fact]
    public async Task CurrentCountyManagerCanDirectlyMoveCaveToAnotherCounty()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CurrentCountyManagerCanDirectlyMoveCaveToAnotherCounty));
        var (cave, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var destination = await AddDestinationCountyAsync(database, cave);
        const string userId = "source-county-manager";
        await GrantScopedAsync(database, cave, userId, "county", PermissionKey.Manager, "mAnageR000");

        await using var actor = await CaveTestActor.CreateAsync(database, cave.AccountId, userId);
        var context = await actor.Services.Caves.GetEditAuthoringContextAsync(cave.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.CountyId = destination.Id;
        values.CountyNumber = 2;

        Assert.Equal(cave.CaveId, await actor.Services.Caves.AddCave(values, default));
        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(destination.Id, (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == cave.CaveId)).CountyId);
    }

    private static async Task<County> AddDestinationCountyAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave)
    {
        var county = new County
        {
            Id = "county00b", AccountId = cave.AccountId, StateId = cave.StateId,
            DisplayId = "A02", Name = "Destination County"
        };
        await using var db = database.CreateDbContext("county-seed", cave.AccountId);
        db.Counties.Add(county);
        await db.SaveChangesAsync();
        return county;
    }

    private static async Task GrantScopedAsync(PostgresTestDatabase database, PublishedCaveTestData cave,
        string userId, string scope, string permissionKey, string permissionId)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
        await CavePermissions.EnsureAccountUserAsync(db, cave.AccountId);
        if (!await db.Permissions.AnyAsync(row => row.Id == permissionId))
        {
            db.Permissions.Add(new Permission
            {
                Id = permissionId, Key = permissionKey, Name = permissionKey,
                Description = permissionKey, PermissionType = "Cave"
            });
            await db.SaveChangesAsync();
        }

        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = cave.AccountId,
            PermissionId = permissionId,
            CaveId = scope == "cave" ? cave.CaveId : null,
            CountyId = scope == "county" ? cave.CountyId : null,
            StateId = scope == "state" ? cave.StateId : null
        });
        await db.SaveChangesAsync();
    }
}
