using Microsoft.EntityFrameworkCore;
using Planarian.Model.Shared;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveDirectPeopleTagIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ManagerSaveReusesExistingPeopleIdentityByTypedName()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ManagerSaveReusesExistingPeopleIdentityByTypedName));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var person = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Existing Person", "people000a");
        var nonAsciiPerson = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Élodie Person", "people000b");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var values = PublishableValues(tenant, quality.Id, "Direct People identity");
            values.CartographerNameTagIds = ["existing person"];
            values.ReportedByNameTagIds = ["élodie person"];
            Assert.Equal(tenant.CaveId, await manager.Services.Caves.AddCave(values, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(person.Id, Assert.Single(await verify.CartographerNameTags
            .Where(tag => tag.CaveId == tenant.CaveId).ToListAsync()).TagTypeId);
        Assert.Equal(nonAsciiPerson.Id, Assert.Single(await verify.CaveReportedByNameTags
            .Where(tag => tag.CaveId == tenant.CaveId).ToListAsync()).TagTypeId);
        Assert.Equal(2, await verify.TagTypes.CountAsync(tag => tag.Key == TagTypeKeyConstant.People &&
            tag.AccountId == tenant.AccountId));
        Assert.False(await verify.TagTypes.AnyAsync(tag => tag.Key == TagTypeKeyConstant.People &&
            tag.AccountId == tenant.AccountId && tag.Name == "existing person"));
    }
}
