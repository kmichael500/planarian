using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Import.Data;
using Planarian.Modules.Import.Models;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportPlanningRepositoryIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CaveProjectionIsAccountSafeCompleteAndUntracked()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaveProjectionIsAccountSafeCompleteAndUntracked));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var local = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            TagTypeKeyConstant.Geology, "Local", "localtag01");
        var global = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            TagTypeKeyConstant.Geology, "Default", "default001", isDefault: true);
        var foreign = await ReferenceTestData.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.Geology, "Foreign", "foreign001");
        await using (var seed = database.CreateDbContext("seed", accountA.AccountId))
        {
            seed.GeologyTags.Add(new GeologyTag
            {
                Id = "cavegeo001",
                CaveId = accountA.CaveId,
                TagTypeId = local.Id
            });
            await seed.SaveChangesAsync();
        }

        await using var db = database.CreateDbContext("repository", accountA.AccountId);
        var records = new[] { CaveRecord(accountA) };

        // Act
        var state = await new CaveImportPlanningRepository(db, db.RequestUser)
            .LoadAsync(records, syncExisting: true);

        // Assert
        var cave = Assert.Single(state.ExistingCaves);
        Assert.Equal(accountA.CaveId, cave.Id);
        Assert.Equal(accountA.RevisionId, cave.CurrentRevisionId);
        Assert.Contains(local.Id, cave.GeologyTagIds);
        Assert.Contains(state.EligibleTags, tag => tag.Id == local.Id);
        Assert.Contains(state.EligibleTags, tag => tag.Id == global.Id);
        Assert.DoesNotContain(state.EligibleTags, tag => tag.Id == foreign.Id);
        Assert.Contains(new CaveImportUsedCountyNumber(accountA.CountyId, accountA.CountyNumber),
            state.UsedCountyNumbers);
        Assert.DoesNotContain(state.ExistingCaves, candidate => candidate.Id == accountB.CaveId);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [Fact]
    public async Task EntranceProjectionIsAccountSafeAndLargeTargetLoadIsSetOriented()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceProjectionIsAccountSafeAndLargeTargetLoadIsSetOriented));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "quality001");
        var foreign = await ReferenceTestData.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Foreign", "foreign002");

        const int targetCount = 300;
        await using (var seed = database.CreateDbContext("seed", accountA.AccountId))
        {
            seed.Caves.AddRange(Enumerable.Range(2, targetCount - 1).Select(number => new Cave
            {
                Id = $"r{number:D9}",
                AccountId = accountA.AccountId,
                StateId = accountA.StateId,
                CountyId = accountA.CountyId,
                Name = $"Repository Cave {number}",
                CountyNumber = number,
                IsArchived = false
            }));
            await seed.SaveChangesAsync();
        }

        var counter = new DbCommandCounter();
        await using var db = database.CreateDbContext("repository", accountA.AccountId, counter);
        var records = Enumerable.Range(1, targetCount).Select(number => new EntranceCsvModel
        {
            CountyCode = accountA.CountyDisplayId,
            CountyCaveNumber = number.ToString(),
            LocationQuality = quality.Name
        }).ToList();
        counter.Reset();

        // Act
        var state = await new EntranceImportPlanningRepository(db, db.RequestUser).LoadAsync(records);

        // Assert
        Assert.Equal(targetCount, state.Caves.Count);
        Assert.DoesNotContain(state.Caves, cave => cave.Id == accountB.CaveId);
        Assert.Contains(state.EligibleTags, tag => tag.Id == quality.Id);
        Assert.DoesNotContain(state.EligibleTags, tag => tag.Id == foreign.Id);
        Assert.InRange(counter.Commands.Count, 2, 4);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    private static CaveCsvModel CaveRecord(PublishedCaveTestData cave) => new()
    {
        CaveName = cave.CaveName,
        State = cave.StateAbbreviation,
        CountyCode = cave.CountyDisplayId,
        CountyName = cave.CountyName,
        CountyCaveNumber = cave.CountyNumber,
        IsArchived = false
    };
}
