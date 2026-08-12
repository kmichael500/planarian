using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportTenantAdversarialIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CaveSyncCannotUpdateForeignCollidingCave()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaveSyncCannotUpdateForeignCollidingCave));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        await MakeCountyCodeCollideAsync(database, accountB, "A01");
        var accountBBefore = await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId);
        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.CaveHeader +
            "\nA Updated,County A,A01,1,AA,,,,100,20,5,1,,,,,,2026-08-01,,false,,A only\n";

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: true);
        await import.ExecuteAsync(plan, "collision.csv");

        // Assert
        Assert.DoesNotContain(plan.Caves, cave => cave.Id == accountB.CaveId);
        Assert.Equal(accountBBefore,
            await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId));
    }

    [Fact]
    public async Task CaveSyncCannotDeleteForeignCave()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(CaveSyncCannotDeleteForeignCave));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var accountBBefore = await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId);
        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var import = new CaveImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.CaveHeader +
            "\nReplacement,Replacement,REP,2,AA,,,,10,2,1,1,,,,,,,,false,,\n";

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: true);
        await import.ExecuteAsync(plan, "delete.csv");

        // Assert
        Assert.DoesNotContain(plan.Deletions, deletion => deletion.CaveId == accountB.CaveId);
        Assert.Equal(accountBBefore,
            await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId));
    }

    [Fact]
    public async Task EntranceCannotAssociateToForeignCave()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(EntranceCannotAssociateToForeignCave));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var accountBBefore = await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId);
        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.EntranceHeader +
            "\nForeign,B01,1,true,35,-86,500,Survey Grade,0,Open,,,,,,\n";

        // Act / Assert
        await Assert.ThrowsAsync<ApiException>(() => import.PlanCsvAsync(csv, syncExisting: false));
        Assert.Equal(accountBBefore,
            await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId));
    }

    [Fact]
    public async Task ForeignPrimaryEntranceDoesNotAffectAccountAPrimaryCount()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ForeignPrimaryEntranceDoesNotAffectAccountAPrimaryCount));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        await AddPrimaryEntranceAsync(database, accountB, "bprimary00");
        await MakeCountyCodeCollideAsync(database, accountB, "A01");
        var accountBBefore = await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId);
        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.EntranceHeader +
            "\nA Primary,A01,1,true,35,-86,500,Survey Grade,0,Open,,,,,,\n";

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: false);
        await import.ExecuteAsync(plan, "primary.csv");

        // Assert
        Assert.Single(plan.Entrances);
        Assert.Equal(accountBBefore,
            await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId));
    }

    [Fact]
    public async Task EntranceSyncDoesNotDeleteForeignEntrancesOrTags()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceSyncDoesNotDeleteForeignEntrancesOrTags));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        await AddPrimaryEntranceAsync(database, accountA, "aprimary00");
        await AddPrimaryEntranceAsync(database, accountB, "bprimary00");
        var accountBBefore = await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId);
        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.EntranceHeader +
            "\nReplacement,A01,1,true,35.2,-86.2,520,Survey Grade,0,Open,Wet,Sink,2026-08-01,Surveyor,Replacement\n";

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: true);
        await import.ExecuteAsync(plan, "sync.csv");

        // Assert
        Assert.Equal(accountBBefore,
            await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId));
    }

    [Fact]
    public async Task ForeignCustomTagIsNeverReusable()
    {
        // Arrange
        await using var database = await fixture.CreateDatabaseAsync(nameof(ForeignCustomTagIsNeverReusable));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var foreignTag = await ReferenceTestData.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Foreign Status");
        var accountBBefore = await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId);
        await using var db = database.CreateDbContext("a", accountA.AccountId);
        var import = new EntranceImportTestHarness(db, db.RequestUser);
        var csv = ImportDryRunIntegrationTests.EntranceHeader +
            "\nA Entrance,A01,1,true,35,-86,500,Survey Grade,0,Foreign Status,,,,,,\n";

        // Act
        var plan = await import.PlanCsvAsync(csv, syncExisting: false);
        await import.ExecuteAsync(plan, "tag.csv");

        // Assert
        Assert.Contains(plan.TagCreations,
            tag => tag.Name == "Foreign Status" && tag.Id != foreignTag.Id);
        Assert.False(await db.EntranceStatusTags.AnyAsync(tag => tag.TagTypeId == foreignTag.Id));
        Assert.Equal(accountBBefore,
            await NormalizedDatabaseState.CaptureTenantAsync(database, accountB.AccountId));
    }

    private static async Task MakeCountyCodeCollideAsync(
        PostgresTestDatabase database, PublishedCaveTestData tenant, string displayId)
    {
        await using var db = database.CreateDbContext("collision-seed", tenant.AccountId);
        var county = await db.Counties.SingleAsync(candidate => candidate.Id == tenant.CountyId);
        county.DisplayId = displayId;
        await db.SaveChangesAsync();
    }

    private static async Task AddPrimaryEntranceAsync(
        PostgresTestDatabase database, PublishedCaveTestData tenant, string entranceId)
    {
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade");
        var status = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Open");
        await EntranceTestData.AddEntranceAsync(database, tenant, entranceId,
            isPrimary: true, locationQualityTagId: quality.Id, entranceStatusTagId: status.Id);
    }
}
