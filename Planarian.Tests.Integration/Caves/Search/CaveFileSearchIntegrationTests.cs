using Microsoft.EntityFrameworkCore;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Query.Models;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Search;

public sealed class CaveFileSearchIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData("Entrance", true)]
    [InlineData("PDF", false)]
    public async Task FileNameSearchMatchesThePersistedNameRatherThanTheExtension(string value, bool expected)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(FileNameSearchMatchesThePersistedNameRatherThanTheExtension)}_{value}_{expected}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "search001a", name: "Entrance Survey", extension: ".PDF");
        await CavePermissions.GrantViewAsync(database, tenant, "viewer");

        await using var viewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "viewer");
        var repository = new CaveRepository(viewer.Db, viewer.Db.RequestUser);
        var query = new FilterQuery
        {
            Conditions = [new QueryCondition(nameof(CaveSearchParamsVm.FileName), QueryOperator.Contains, value)]
        };

        var matchesCave = await repository.GetCaveIds(query).AnyAsync(id => id == tenant.CaveId);

        Assert.Equal(expected, matchesCave);
    }

    [Theory]
    [InlineData("pdf", true)]
    [InlineData(".pdf", true)]
    [InlineData("PDF", true)]
    [InlineData("jpg", false)]
    public async Task FileExtensionSearchNormalizesTheLeadingDotAndMatchesCaseInsensitively(
        string value, bool expected)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(FileExtensionSearchNormalizesTheLeadingDotAndMatchesCaseInsensitively)}_{value}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "search002a", name: "Entrance Survey", extension: ".PDF");
        await CavePermissions.GrantViewAsync(database, tenant, "viewer");

        await using var viewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "viewer");
        var repository = new CaveRepository(viewer.Db, viewer.Db.RequestUser);
        var query = new FilterQuery
        {
            Conditions = [new QueryCondition(nameof(CaveSearchParamsVm.FileExtension), QueryOperator.Equal, value)]
        };

        var matchesCave = await repository.GetCaveIds(query).AnyAsync(id => id == tenant.CaveId);

        Assert.Equal(expected, matchesCave);
    }
}
