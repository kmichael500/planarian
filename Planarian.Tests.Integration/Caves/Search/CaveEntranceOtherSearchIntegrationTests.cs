using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Query.Models;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Search;

public sealed class CaveEntranceOtherSearchIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task EntranceOtherFilterMatchesCaveThroughEntranceAssociation()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceOtherFilterMatchesCaveThroughEntranceAssociation));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var other = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.CaveOther, "Distinctive");
        var different = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.CaveOther, "Different");
        var entranceId = await EntranceTestData.AddEntranceAsync(database, cave, "searchent1");
        var otherCave = await CaveTestDataFactory.AddCaveAsync(database, cave, "cave00000b", "Other Cave", 2);
        var publishedOtherCave = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, otherCave, "revision0b");
        var otherEntranceId = await EntranceTestData.AddEntranceAsync(database, publishedOtherCave, "searchent2");
        await using (var seed = database.CreateDbContext("other-seed", cave.AccountId))
        {
            seed.EntranceOtherTag.AddRange(
                new EntranceOtherTag
                {
                    Id = IdGenerator.Generate(),
                    EntranceId = entranceId,
                    TagTypeId = other.Id
                },
                new EntranceOtherTag
                {
                    Id = IdGenerator.Generate(),
                    EntranceId = otherEntranceId,
                    TagTypeId = different.Id
                });
            await seed.SaveChangesAsync();
        }
        await CavePermissions.GrantViewAsync(database, cave, "viewer");
        await CavePermissions.GrantViewAsync(database, publishedOtherCave, "viewer");

        await using var viewer = await CaveTestActor.CreateAsync(database, cave.AccountId, "viewer");
        var repository = new CaveRepository(viewer.Db, viewer.Db.RequestUser);
        var query = new FilterQuery
        {
            Conditions =
            [
                new QueryCondition(nameof(CaveSearchParamsVm.EntranceOtherTagIds), QueryOperator.In, other.Id)
            ]
        };

        var ids = await repository.GetCaveIds(query).OrderBy(id => id).ToListAsync();
        Assert.Equal([cave.CaveId], ids);
    }
}
