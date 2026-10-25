using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Files.Controllers;
using Xunit;
using Planarian.Tests.Integration.Infrastructure.Services;
using static Planarian.Tests.MigrationTestSupport;

namespace Planarian.Tests;

[Collection(MigrationIntegrationCollection.Name)]
public sealed class LegacyRevisionMigrationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task MigratedLegacyCaveInitializesOneBaselineForProposalAuthoringAndCanBeSubmitted()
    {
        await using var database = await fixture.CreateUnmigratedDatabaseAsync(
            nameof(MigratedLegacyCaveInitializesOneBaselineForProposalAuthoringAndCanBeSubmitted));
        var migrations = database.GetMigrationNames().ToList();
        var baseline = migrations.FindIndex(migration =>
            migration.EndsWith(HistoricalV29BaselineMigration, StringComparison.Ordinal));
        await database.MigrateAsync(migrations[baseline]);
        await V29DatabaseSeeder.SeedRepresentativeTenantAsync(database);
        await database.MigrateAsync(null);

        const string accountId = "mainacct01";
        const string caveId = "maincave01";
        const string contributorId = "contrib001";
        await using (var permissions = database.CreateDbContext(contributorId, accountId))
        {
            permissions.AccountUsers.Add(new AccountUser
            {
                AccountId = accountId,
                UserId = permissions.RequestUser.Id,
                InvitationAcceptedOn = DateTime.UtcNow
            });
            permissions.Permissions.Add(new Permission
            {
                Id = "legacyview", Key = "View", Name = "View", Description = "View Caves",
                PermissionType = "Cave"
            });
            permissions.CavePermissions.Add(new CavePermission
            {
                AccountId = accountId, CaveId = caveId, UserId = permissions.RequestUser.Id,
                PermissionId = "legacyview"
            });
            await permissions.SaveChangesAsync();
        }

        async Task<CaveProposalAuthoringContextVm> InitializeAsync(string contextName)
        {
            await using var db = database.CreateDbContext(contextName, accountId);
            await db.RequestUser.Initialize(accountId, contributorId);
            return await IntegrationTestServices.For(db).CaveChangeRequests.GetAuthoringContextAsync(caveId, default);
        }

        var contexts = await Task.WhenAll(InitializeAsync("legacy-init-a"), InitializeAsync("legacy-init-b"));
        Assert.All(contexts, context => Assert.False(string.IsNullOrWhiteSpace(context.ExpectedBaseRevisionId)));
        Assert.Equal(contexts[0].ExpectedBaseRevisionId, contexts[1].ExpectedBaseRevisionId);

        string requestId;
        await using (var contributor = database.CreateDbContext(contributorId, accountId))
        {
            await contributor.RequestUser.Initialize(accountId, contributorId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            var authoring = await service.GetAuthoringContextAsync(caveId, default);
            var values = LegacyValues(authoring.Cave);
            values.Name = "Proposed legacy Cave name";
            var preview = await service.PreviewAsync(caveId, values, authoring.ExpectedBaseRevisionId, default);
            Assert.Equal("Proposed legacy Cave name", preview.Proposed.Name);
            requestId = await service.CreateAsync(caveId, values, authoring.ExpectedBaseRevisionId, default);
        }

        await using var verify = database.CreateDbContext("verify", accountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == caveId);
        Assert.Equal(contexts[0].ExpectedBaseRevisionId, cave.CurrentRevisionId);
        var revisions = await verify.CaveRevisions.Where(revision => revision.CaveId == caveId).ToListAsync();
        Assert.Single(revisions);
        Assert.Equal(CaveRevisionSource.SystemBaseline, revisions[0].Source);
        Assert.Equal(CaveRevisionOperation.Create, revisions[0].Operation);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Pending, request.Status);
        Assert.NotNull(request.CurrentProposalVersionId);
    }


    private static AddCaveVm LegacyValues(CaveVm cave) => new()
    {
        Id = cave.Id,
        Name = cave.Name,
        AlternateNames = cave.AlternateNames,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumber = cave.CountyNumber,
        IsCountyNumberManuallySet = true,
        LengthFeet = cave.LengthFeet ?? 0,
        DepthFeet = cave.DepthFeet ?? 0,
        MaxPitDepthFeet = cave.MaxPitDepthFeet ?? 0,
        NumberOfPits = cave.NumberOfPits ?? 0,
        Narrative = cave.Narrative,
        ReportedOn = cave.ReportedOn,
        GeologyTagIds = cave.GeologyTagIds,
        ReportedByNameTagIds = cave.ReportedByNameTagIds,
        BiologyTagIds = cave.BiologyTagIds,
        ArcheologyTagIds = cave.ArcheologyTagIds,
        CartographerNameTagIds = cave.CartographerNameTagIds,
        MapStatusTagIds = cave.MapStatusTagIds,
        GeologicAgeTagIds = cave.GeologicAgeTagIds,
        PhysiographicProvinceTagIds = cave.PhysiographicProvinceTagIds,
        OtherTagIds = cave.OtherTagIds,
        Files = cave.Files.Select(file => new EditFileMetadataVm
            { Id = file.Id, FileTypeTagId = file.FileTypeTagId, Name = file.Name }).ToList(),
        Entrances = cave.Entrances.Select(entrance => new AddEntranceVm
        {
            Id = entrance.Id,
            IsPrimary = entrance.IsPrimary,
            LocationQualityTagId = entrance.LocationQualityTagId,
            Name = entrance.Name,
            Description = entrance.Description,
            Latitude = entrance.Latitude,
            Longitude = entrance.Longitude,
            ElevationFeet = entrance.ElevationFeet,
            ReportedOn = entrance.ReportedOn,
            PitFeet = entrance.PitFeet,
            EntranceStatusTagIds = entrance.EntranceStatusTagIds,
            FieldIndicationTagIds = entrance.FieldIndicationTagIds,
            EntranceHydrologyTagIds = entrance.EntranceHydrologyTagIds,
            EntranceOtherTagIds = entrance.EntranceOtherTagIds,
            ReportedByNameTagIds = entrance.ReportedByNameTagIds
        }).ToList()
    };

}
