using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.AccountReset;

public sealed class AccountResetKitchenSinkIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ResetRemovesCompleteAccountGraphAndPreservesOtherTenantsAndAdministrativeIdentity()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ResetRemovesCompleteAccountGraphAndPreservesOtherTenantsAndAdministrativeIdentity));
        var caveA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var caveB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var blobs = new TestFileBlobStore();

        await SeedAccountAsync(database, caveA, blobs);
        await SeedAccountAsync(database, caveB, blobs);

        await using var before = database.CreateDbContext("observer", caveA.AccountId);
        var preResetA = await CaptureGraphAsync(before, caveA);
        var preResetB = await CaptureGraphAsync(before, caveB);
        // Whole-row JSON projections catch modifications to non-key columns that an
        // ID-only graph comparison would miss.
        var untouchedTenantBefore = await CaptureTenantRowsAsync(database.ConnectionString, caveB.AccountId);
        Assert.All(preResetA.Values, values => Assert.NotEmpty(values));
        Assert.Equal(preResetA.Keys.Order(), preResetB.Keys.Order());

        await using (var reset = database.CreateDbContext("a", caveA.AccountId))
            await IntegrationTestServices.For(reset, blobs, maintenanceConnectionString: database.ConnectionString).Account.ResetAccount(default);

        await using var after = database.CreateDbContext("observer", caveA.AccountId);
        var postResetA = await CaptureGraphAsync(after, caveA);
        var postResetB = await CaptureGraphAsync(after, caveB);
        Assert.All(postResetA.Values, values => Assert.Empty(values));
        Assert.Equal(preResetB.Keys.Order(), postResetB.Keys.Order());
        foreach (var key in preResetB.Keys)
            Assert.Equal(preResetB[key], postResetB[key]);
        var untouchedTenantAfter = await CaptureTenantRowsAsync(database.ConnectionString, caveB.AccountId);
        Assert.Equal(untouchedTenantBefore.Keys.Order(), untouchedTenantAfter.Keys.Order());
        foreach (var key in untouchedTenantBefore.Keys)
            Assert.Equal(untouchedTenantBefore[key], untouchedTenantAfter[key]);

        // Reset means account *data*, not deleting the account, its users, global references,
        // account-wide permissions, settings, or the other tenant.
        Assert.True(await after.Accounts.IgnoreQueryFilters().AnyAsync(a => a.Id == caveA.AccountId));
        Assert.True(await after.Accounts.IgnoreQueryFilters().AnyAsync(a => a.Id == caveB.AccountId));
        Assert.True(await after.AccountUsers.IgnoreQueryFilters().AnyAsync(a => a.AccountId == caveA.AccountId));
        Assert.True(await after.AccountUsers.IgnoreQueryFilters().AnyAsync(a => a.AccountId == caveB.AccountId));
        Assert.True(await after.FeatureSettings.IgnoreQueryFilters().AnyAsync(a => a.AccountId == caveA.AccountId));
        Assert.True(await after.FeatureSettings.IgnoreQueryFilters().AnyAsync(a => a.AccountId == caveB.AccountId));

        Assert.DoesNotContain(blobs.Keys, key => key.Container == "account-" + caveA.AccountId);
        Assert.DoesNotContain(blobs.Keys, key => key.Key.EndsWith("-a", StringComparison.Ordinal));
        Assert.Contains(blobs.Keys, key => key.Key == "archives/backup-b");
        Assert.Contains(blobs.Keys, key => key.Key == "archives/temp/working-b");
        Assert.Contains(blobs.Keys, key => key.Key == "portable/retained-b");
    }

    private static async Task<Dictionary<string, string>> CaptureTenantRowsAsync(
        string connectionString, string accountId)
    {
        // A fixed whitelist: the SQL here is a test observation, not domain setup.
        string[] tables =
        [
            "Caves", "Files", "StorageCleanupTargets", "CaveRevisions",
            "CaveChangeRequests", "CaveProposalVersions", "CaveChangeRequestReviewEvents",
            "CaveImportBatches", "Favorites", "Counties", "TagTypes",
            "AccountStates", "AccountUsers", "FeatureSettings", "CavePermission"
        ];
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var rows = new Dictionary<string, string>();
        foreach (var table in tables)
        {
            await using var command = new NpgsqlCommand(
                $"select coalesce(jsonb_agg(to_jsonb(t) order by t.\"Id\"), '[]'::jsonb)::text " +
                $"from \"{table}\" as t where t.\"AccountId\" = @account", connection);
            command.Parameters.AddWithValue("account", accountId);
            rows[table] = (string)(await command.ExecuteScalarAsync() ?? "[]");
        }

        return rows;
    }

    private static async Task SeedAccountAsync(PostgresTestDatabase database, PublishedCaveTestData cave,
        TestFileBlobStore blobs)
    {
        var suffix = cave.AccountId[^1];
        var actor = suffix.ToString();
        await CavePermissions.GrantViewAsync(database, cave, actor);
        var tagIds = new Dictionary<string, string>();
        foreach (var key in new[]
        {
            TagTypeKeyConstant.Geology, TagTypeKeyConstant.Biology, TagTypeKeyConstant.MapStatus,
            TagTypeKeyConstant.GeologicAge, TagTypeKeyConstant.PhysiographicProvince,
            TagTypeKeyConstant.Archeology, TagTypeKeyConstant.People, TagTypeKeyConstant.CaveOther,
            TagTypeKeyConstant.EntranceStatus, TagTypeKeyConstant.EntranceHydrology,
            TagTypeKeyConstant.FieldIndication
        })
        {
            var tag = await ReferenceTestData.AddTagAsync(database, cave.AccountId, key,
                key + " " + actor);
            tagIds[key] = tag.Id;
        }
        var entranceId = await EntranceTestData.AddEntranceAsync(database, cave,
            "entrance-" + actor);
        var file = await FileTestDataFactory.AddFileAsync(database, cave,
            fileId: "staged000" + actor);

        var pending = await CaveChangeRequestTestDataFactory.CreateChangeRequestAsync(database, cave,
            addProposalVersion: true);
        await using var db = database.CreateDbContext(actor, cave.AccountId);
        db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
        {
            Id = "staged-link-" + actor, AccountId = cave.AccountId,
            ChangeRequestId = pending.ChangeRequestId, FileId = file.FileId
        });
        db.CaveGeoJsons.Add(new CaveGeoJson
        {
            Id = "lineplot-" + actor, CaveId = cave.CaveId, Name = "Line plot " + actor,
            GeoJson = "{\"type\":\"FeatureCollection\",\"features\":[]}"
        });
        db.Favorites.Add(new Favorite
        {
            Id = "favorite-" + actor, CaveId = cave.CaveId,
            AccountId = cave.AccountId, UserId = db.RequestUser.Id
        });
        db.AccountStates.Add(new AccountState
        {
            AccountId = cave.AccountId, StateId = cave.StateId
        });
        db.FeatureSettings.Add(new FeatureSetting
        {
            AccountId = cave.AccountId, Key = FeatureKey.EnabledFieldCaveName,
            IsEnabled = true
        });
        db.GeologyTags.Add(new GeologyTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.Geology] });
        db.BiologyTags.Add(new BiologyTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.Biology] });
        db.MapStatusTags.Add(new MapStatusTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.MapStatus] });
        db.GeologicAgeTags.Add(new GeologicAgeTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.GeologicAge] });
        db.PhysiographicProvinceTags.Add(new PhysiographicProvinceTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.PhysiographicProvince] });
        db.ArcheologyTags.Add(new ArcheologyTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.Archeology] });
        db.CartographerNameTags.Add(new CartographerNameTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.People] });
        db.CaveReportedByNameTags.Add(new CaveReportedByNameTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.People] });
        db.CaveOtherTags.Add(new CaveOtherTag { CaveId = cave.CaveId, TagTypeId = tagIds[TagTypeKeyConstant.CaveOther] });
        db.EntranceStatusTags.Add(new EntranceStatusTag { EntranceId = entranceId, TagTypeId = tagIds[TagTypeKeyConstant.EntranceStatus] });
        db.EntranceHydrologyTags.Add(new EntranceHydrologyTag { EntranceId = entranceId, TagTypeId = tagIds[TagTypeKeyConstant.EntranceHydrology] });
        db.FieldIndicationTags.Add(new FieldIndicationTag { EntranceId = entranceId, TagTypeId = tagIds[TagTypeKeyConstant.FieldIndication] });
        db.EntranceReportedByNameTags.Add(new EntranceReportedByNameTag { EntranceId = entranceId, TagTypeId = tagIds[TagTypeKeyConstant.People] });
        db.EntranceOtherTag.Add(new EntranceOtherTag { EntranceId = entranceId, TagTypeId = tagIds[TagTypeKeyConstant.CaveOther] });
        db.Files.Add(new File
        {
            Id = "live-" + actor, AccountId = cave.AccountId, CaveId = cave.CaveId,
            FileTypeTagId = file.FileTypeId, Name = "Published",
            Extension = ".pdf", BlobContainer = "account-" + cave.AccountId, BlobKey = "objects/live-" + actor
        });
        db.Files.Add(new File
        {
            Id = "temporary-" + actor, AccountId = cave.AccountId,
            FileTypeTagId = file.FileTypeId, Name = "Temporary",
            Extension = ".pdf", BlobContainer = "account-" + cave.AccountId, BlobKey = "objects/temporary-" + actor,
            ExpiresOn = DateTime.UtcNow.AddMinutes(30)
        });
        db.StorageCleanupTargets.Add(new StorageCleanupTarget
        {
            AccountId = cave.AccountId,
            StoragePartition = "portable-account-objects", StorageKey = "portable/retained-" + actor,
            Purpose = StorageCleanupPurpose.CaveAssetRemoval
        });
        await db.SaveChangesAsync();

        var requests = new CaveChangeRequestRepository(db, db.RequestUser);
        var reviewedId = await requests.CreateAsync(cave.CaveId, cave.RevisionId,
            Proposal(cave, "Reviewed " + actor), default);
        var versionId = await CurrentVersionAsync(db, reviewedId);
        await requests.RequestChangesAsync(reviewedId, versionId, "Please revise", default);

        blobs.Seed("account-" + cave.AccountId, "objects/live-" + actor, [1]);
        blobs.Seed("account-" + cave.AccountId, "objects/temporary-" + actor, [2]);
        blobs.Seed("account-" + cave.AccountId, "archives/backup-" + actor, [3]);
        blobs.Seed("account-" + cave.AccountId, "archives/temp/working-" + actor, [4]);
        blobs.Seed("account-" + cave.AccountId, "import/transport-" + actor, [5]);
        blobs.Seed("portable-account-objects", "portable/retained-" + actor, [6]);
    }

    private static async Task<Dictionary<string, string[]>> CaptureGraphAsync(
        Planarian.Model.Database.PlanarianDbContext db, PublishedCaveTestData cave)
    {
        var accountId = cave.AccountId;
        return new Dictionary<string, string[]>
        {
            ["Caves"] = await db.Caves.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Entrances"] = await db.Entrances.IgnoreQueryFilters().Where(x => x.Cave!.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Files"] = await db.Files.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["CleanupTargets"] = await db.StorageCleanupTargets.IgnoreQueryFilters()
                .Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Revisions"] = await db.CaveRevisions.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Requests"] = await db.CaveChangeRequests.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Versions"] = await db.CaveProposalVersions.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Reviews"] = await db.CaveChangeRequestReviewEvents.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Staged"] = await db.CaveChangeRequestStagedFiles.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Favorites"] = await db.Favorites.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["LinePlots"] = await db.CaveGeoJsons.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Counties"] = await db.Counties.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["TagTypes"] = await db.TagTypes.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["AccountStates"] = await db.AccountStates.IgnoreQueryFilters().Where(x => x.AccountId == accountId).Select(x => x.StateId).OrderBy(x => x).ToArrayAsync(),
            ["Permissions"] = await db.CavePermissions.IgnoreQueryFilters().Where(x => x.AccountId == accountId && x.CaveId != null).Select(x => x.Id).OrderBy(x => x).ToArrayAsync(),
            ["Geology"] = await db.GeologyTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["Biology"] = await db.BiologyTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["MapStatus"] = await db.MapStatusTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["GeologicAge"] = await db.GeologicAgeTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["Physiography"] = await db.PhysiographicProvinceTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["Archeology"] = await db.ArcheologyTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["Cartographers"] = await db.CartographerNameTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["ReportedBy"] = await db.CaveReportedByNameTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["CaveOther"] = await db.CaveOtherTags.IgnoreQueryFilters().Where(x => x.Cave.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["EntranceStatus"] = await db.EntranceStatusTags.IgnoreQueryFilters().Where(x => x.Entrance.Cave!.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["EntranceHydrology"] = await db.EntranceHydrologyTags.IgnoreQueryFilters().Where(x => x.Entrance.Cave!.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["FieldIndications"] = await db.FieldIndicationTags.IgnoreQueryFilters().Where(x => x.Entrance.Cave!.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["EntranceReportedBy"] = await db.EntranceReportedByNameTags.IgnoreQueryFilters().Where(x => x.Entrance.Cave!.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync(),
            ["EntranceOther"] = await db.EntranceOtherTag.IgnoreQueryFilters().Where(x => x.Entrance.Cave!.AccountId == accountId).Select(x => x.TagTypeId).OrderBy(x => x).ToArrayAsync()
        };
    }
}
