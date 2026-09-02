using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Xunit;

namespace Planarian.Tests.Integration.AccountArchive;

public sealed class ArchiveEntranceProjectionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ArchiveProjectsEntranceOtherTags()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(ArchiveProjectsEntranceOtherTags));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var other = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.CaveOther, "Distinctive");
        var entranceId = await EntranceTestData.AddEntranceAsync(database, cave, "archiveent");

        await using var db = database.CreateDbContext("archive", cave.AccountId);
        db.EntranceOtherTag.Add(new EntranceOtherTag
        {
            Id = IdGenerator.Generate(), EntranceId = entranceId, TagTypeId = other.Id
        });
        await db.SaveChangesAsync();

        var result = Assert.Single(await new AccountRepository(db, db.RequestUser)
            .GetArchiveEntrances(cave.AccountId, default));

        Assert.Equal("Distinctive", result.EntranceOtherTags);
    }
}
