using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests;

public sealed class EntranceImportAuthoringParityIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private static readonly DateTime ReportedOn =
        new(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AppendPersistsEveryEntranceFieldLikeDirectAuthoring()
    {
        await using var directDatabase = await fixture.CreateDatabaseAsync(
            nameof(AppendPersistsEveryEntranceFieldLikeDirectAuthoring) + "_direct");
        await using var importDatabase = await fixture.CreateDatabaseAsync(
            nameof(AppendPersistsEveryEntranceFieldLikeDirectAuthoring) + "_import");

        var directTenant = await CaveTestDataFactory.CreatePublishedCaveAsync(directDatabase, 'a');
        var importTenant = await CaveTestDataFactory.CreatePublishedCaveAsync(importDatabase, 'a');
        var directReferences = await SeedReferencesAsync(directDatabase, directTenant.AccountId);
        _ = await SeedReferencesAsync(importDatabase, importTenant.AccountId);
        await CavePermissions.GrantManagerAsync(directDatabase, directTenant, "manager");

        string directRevisionId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(directTenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.Entrances = [FullEntrance(directReferences)];
            await manager.Services.Caves.AddCave(values, default);
            manager.Db.ChangeTracker.Clear();
            directRevisionId = (await CurrentRevisionAsync(manager.Db, directTenant.CaveId)).Id;
        }

        string importRevisionId;
        string importBatchId;
        await using (var db = importDatabase.CreateDbContext("entrance-import", importTenant.AccountId))
        {
            var import = new EntranceImportTestHarness(db, db.RequestUser);
            var plan = await import.PlanCsvAsync(
                EntranceCsv(FullRow(importTenant)), syncExisting: false);
            importBatchId = await import.ExecuteAsync(plan, "entrance-full.csv");
            db.ChangeTracker.Clear();
            importRevisionId = (await CurrentRevisionAsync(db, importTenant.CaveId)).Id;
        }

        await using var directVerify = directDatabase.CreateDbContext("verify", directTenant.AccountId);
        await using var importVerify = importDatabase.CreateDbContext("verify", importTenant.AccountId);
        var directSnapshot = await new CavePublishedSnapshotRepository(directVerify, directVerify.RequestUser)
            .BuildAsync(directTenant.CaveId);
        var importSnapshot = await new CavePublishedSnapshotRepository(importVerify, importVerify.RequestUser)
            .BuildAsync(importTenant.CaveId);
        ImportParityAssertions.AssertPublishedSnapshotsEqual(
            directSnapshot, importSnapshot, normalizeEntranceIds: true);

        var directRevision = await directVerify.CaveRevisions.SingleAsync(row => row.Id == directRevisionId);
        var importRevision = await importVerify.CaveRevisions.SingleAsync(row => row.Id == importRevisionId);
        Assert.Equal((directTenant.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, null),
            (directRevision.PreviousRevisionId, directRevision.Source, directRevision.Operation,
                directRevision.ImportBatchId));
        Assert.Equal((importTenant.RevisionId, CaveRevisionSource.Import, CaveRevisionOperation.Update, importBatchId),
            (importRevision.PreviousRevisionId, importRevision.Source, importRevision.Operation,
                importRevision.ImportBatchId));
        ImportParityAssertions.AssertPublishedSnapshotsEqual(
            ReadSnapshot(directRevision), ReadSnapshot(importRevision), normalizeEntranceIds: true);

        var batch = await importVerify.CaveImportBatches.SingleAsync(row => row.Id == importBatchId);
        Assert.Equal("entrance-full.csv", batch.SourceFileName);
        Assert.Equal(CaveImportKind.EntranceCsv, batch.Kind);
        Assert.Equal((false, 1, 1, 0, 0, 0),
            (batch.SyncExisting, batch.SourceRecordCount, batch.InsertedCount, batch.UpdatedCount,
                batch.DeletedCount, batch.NoChangeCount));
    }

    [Fact]
    public async Task SyncReplacementMatchesClearedDirectStateButRecordsRemoveAndAddIdentityHistory()
    {
        await using var directDatabase = await fixture.CreateDatabaseAsync(
            nameof(SyncReplacementMatchesClearedDirectStateButRecordsRemoveAndAddIdentityHistory) + "_direct");
        await using var importDatabase = await fixture.CreateDatabaseAsync(
            nameof(SyncReplacementMatchesClearedDirectStateButRecordsRemoveAndAddIdentityHistory) + "_import");

        var (directTenant, directSurveyGrade) = await CreateMeasuredPublishedCaveAsync(
            directDatabase, 'a', null, null, null, null);
        var (importTenant, importSurveyGrade) = await CreateMeasuredPublishedCaveAsync(
            importDatabase, 'a', null, null, null, null);
        var directReferences = await SeedReplacementReferencesAsync(
            directDatabase, directTenant.AccountId, directSurveyGrade);
        var importReferences = await SeedReplacementReferencesAsync(
            importDatabase, importTenant.AccountId, importSurveyGrade);
        directTenant = directTenant with
        {
            RevisionId = await PopulateExistingEntranceAsync(
                directDatabase, directTenant, directReferences, "manager")
        };
        importTenant = importTenant with
        {
            RevisionId = await PopulateExistingEntranceAsync(
                importDatabase, importTenant, importReferences, "manager")
        };

        CavePublishedSnapshotV1 directBefore;
        CavePublishedSnapshotV1 importBefore;
        await using (var db = directDatabase.CreateDbContext("before", directTenant.AccountId))
            directBefore = ReadSnapshot(await db.CaveRevisions.SingleAsync(row => row.Id == directTenant.RevisionId));
        await using (var db = importDatabase.CreateDbContext("before", importTenant.AccountId))
            importBefore = ReadSnapshot(await db.CaveRevisions.SingleAsync(row => row.Id == importTenant.RevisionId));
        ImportParityAssertions.AssertPublishedSnapshotsEqual(directBefore, importBefore);

        string directRevisionId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directTenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(directTenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            var entrance = Assert.Single(values.Entrances);
            ApplyClearedReplacement(entrance, directReferences.EstimatedLocationQuality);
            await manager.Services.Caves.AddCave(values, default);
            manager.Db.ChangeTracker.Clear();
            directRevisionId = (await CurrentRevisionAsync(manager.Db, directTenant.CaveId)).Id;
        }

        string importRevisionId;
        string importBatchId;
        await using (var db = importDatabase.CreateDbContext("replace-import", importTenant.AccountId))
        {
            var import = new EntranceImportTestHarness(db, db.RequestUser);
            var plan = await import.PlanCsvAsync(
                EntranceCsv(ClearedReplacementRow(importTenant)), syncExisting: true);
            var planned = Assert.Single(plan.Entrances);
            Assert.NotEqual("entrance0a", planned.Id);
            importBatchId = await import.ExecuteAsync(plan, "entrance-replace.csv");
            db.ChangeTracker.Clear();
            importRevisionId = (await CurrentRevisionAsync(db, importTenant.CaveId)).Id;
        }

        await using var directVerify = directDatabase.CreateDbContext("verify", directTenant.AccountId);
        await using var importVerify = importDatabase.CreateDbContext("verify", importTenant.AccountId);
        var directAfter = await new CavePublishedSnapshotRepository(directVerify, directVerify.RequestUser)
            .BuildAsync(directTenant.CaveId);
        var importAfter = await new CavePublishedSnapshotRepository(importVerify, importVerify.RequestUser)
            .BuildAsync(importTenant.CaveId);
        ImportParityAssertions.AssertPublishedSnapshotsEqual(
            directAfter, importAfter, normalizeEntranceIds: true);

        var directDiff = new CaveRevisionDiffService().Compare(
            directBefore,
            ReadSnapshot(await directVerify.CaveRevisions.SingleAsync(row => row.Id == directRevisionId)));
        Assert.Empty(directDiff.AddedEntrances);
        Assert.Empty(directDiff.RemovedEntrances);
        Assert.Equal(["entrance0a"], directDiff.ChangedEntrances);
        var directEntranceChange = Assert.Single(directDiff.EntranceChanges);
        Assert.Equal("entrance0a", directEntranceChange.EntranceId);
        Assert.Contains(nameof(CaveEntranceSnapshotV1.Name), directEntranceChange.Scalars.Keys);
        Assert.Contains(nameof(CaveEntranceSnapshotV1.Description), directEntranceChange.Scalars.Keys);
        Assert.Contains(nameof(CaveEntranceSnapshotV1.Latitude), directEntranceChange.Scalars.Keys);
        Assert.Contains(nameof(CaveEntranceSnapshotV1.Longitude), directEntranceChange.Scalars.Keys);
        Assert.Contains(nameof(CaveEntranceSnapshotV1.Elevation), directEntranceChange.Scalars.Keys);
        Assert.Contains(nameof(CaveEntranceSnapshotV1.LocationQualityTagId), directEntranceChange.Scalars.Keys);
        Assert.Contains(nameof(CaveEntranceSnapshotV1.ReportedOn), directEntranceChange.Scalars.Keys);
        Assert.Contains(nameof(CaveEntranceSnapshotV1.PitDepthFeet), directEntranceChange.Scalars.Keys);

        var importRevision = await importVerify.CaveRevisions.SingleAsync(row => row.Id == importRevisionId);
        var importDiff = new CaveRevisionDiffService().Compare(importBefore, ReadSnapshot(importRevision));
        Assert.Single(importDiff.AddedEntrances);
        Assert.Equal(["entrance0a"], importDiff.RemovedEntrances);
        Assert.Empty(importDiff.ChangedEntrances);
        Assert.Empty(importDiff.EntranceChanges);
        Assert.Equal(importBatchId, importRevision.ImportBatchId);
        Assert.Equal(CaveRevisionSource.Import, importRevision.Source);
        Assert.Equal(CaveRevisionOperation.Update, importRevision.Operation);
        Assert.Equal(importTenant.RevisionId, importRevision.PreviousRevisionId);

        var batch = await importVerify.CaveImportBatches.SingleAsync(row => row.Id == importBatchId);
        Assert.Equal("entrance-replace.csv", batch.SourceFileName);
        Assert.Equal(CaveImportKind.EntranceCsv, batch.Kind);
        Assert.Equal((true, 1, 1, 0, 1, 0),
            (batch.SyncExisting, batch.SourceRecordCount, batch.InsertedCount, batch.UpdatedCount,
                batch.DeletedCount, batch.NoChangeCount));
    }

    private static AddEntranceVm FullEntrance(EntranceReferences references) => new()
    {
        IsPrimary = true,
        Name = "Main Entrance",
        Description = "Full entrance description",
        Latitude = 35.123456,
        Longitude = -86.654321,
        ElevationFeet = 612.5,
        LocationQualityTagId = references.SurveyGradeLocationQuality,
        PitFeet = 22.5,
        ReportedOn = ReportedOn,
        EntranceStatusTagIds = [references.Status],
        EntranceHydrologyTagIds = [references.Hydrology],
        FieldIndicationTagIds = [references.FieldIndication],
        ReportedByNameTagIds = [references.Reporter],
        EntranceOtherTagIds = [references.Other]
    };

    private static void ApplyClearedReplacement(AddEntranceVm entrance, string estimatedLocationQuality)
    {
        entrance.Name = null;
        entrance.Description = null;
        entrance.Latitude = 35.2;
        entrance.Longitude = -86.2;
        entrance.ElevationFeet = 520;
        entrance.LocationQualityTagId = estimatedLocationQuality;
        entrance.PitFeet = null;
        entrance.ReportedOn = null;
        entrance.IsPrimary = true;
        entrance.EntranceStatusTagIds = [];
        entrance.EntranceHydrologyTagIds = [];
        entrance.FieldIndicationTagIds = [];
        entrance.ReportedByNameTagIds = [];
        entrance.EntranceOtherTagIds = [];
    }

    private static string FullRow(PublishedCaveTestData tenant) =>
        CsvRow("Main Entrance", tenant.CountyDisplayId, tenant.CountyNumber, true,
            35.123456, -86.654321, 612.5, "Survey Grade", 22.5,
            "Open", "Wet", "Sink", "2026-08-02", "Surveyor",
            "Full entrance description", "Distinctive");

    private static string ClearedReplacementRow(PublishedCaveTestData tenant) =>
        CsvRow("", tenant.CountyDisplayId, tenant.CountyNumber, true,
            35.2, -86.2, 520, "Estimated", null,
            "", "", "", "", "", "", "");

    private static string EntranceCsv(string row) =>
        ImportDryRunIntegrationTests.EntranceHeader + "\n" + row + "\n";

    private static string CsvRow(params object?[] values) =>
        string.Join(',', values.Select(value =>
        {
            var text = value switch
            {
                null => "",
                bool boolean => boolean ? "true" : "false",
                IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString() ?? ""
            };
            return text.Contains(',') || text.Contains('"')
                ? "\"" + text.Replace("\"", "\"\"") + "\""
                : text;
        }));

    private static async Task<EntranceReferences> SeedReferencesAsync(
        PostgresTestDatabase database, string accountId)
    {
        async Task<string> Add(string key, string name, string id) =>
            (await ReferenceTestData.AddTagAsync(database, accountId, key, name, id)).Id;

        return new EntranceReferences(
            await Add(TagTypeKeyConstant.LocationQuality, "Survey Grade", "entlocsg01"),
            await Add(TagTypeKeyConstant.LocationQuality, "Estimated", "entlocest1"),
            await Add(TagTypeKeyConstant.EntranceStatus, "Open", "entstat001"),
            await Add(TagTypeKeyConstant.EntranceHydrology, "Wet", "enthyd0001"),
            await Add(TagTypeKeyConstant.FieldIndication, "Sink", "entfld0001"),
            await Add(TagTypeKeyConstant.People, "Surveyor", "entpep0001"),
            await Add(TagTypeKeyConstant.CaveOther, "Distinctive", "entoth0001"));
    }

    private static async Task<ReplacementReferences> SeedReplacementReferencesAsync(
        PostgresTestDatabase database, string accountId, string surveyGradeLocationQuality)
    {
        async Task<string> Add(string key, string name, string id) =>
            (await ReferenceTestData.AddTagAsync(database, accountId, key, name, id)).Id;

        return new ReplacementReferences(
            surveyGradeLocationQuality,
            await Add(TagTypeKeyConstant.LocationQuality, "Estimated", "entlocest1"),
            await Add(TagTypeKeyConstant.EntranceStatus, "Open", "entstat001"),
            await Add(TagTypeKeyConstant.EntranceHydrology, "Wet", "enthyd0001"),
            await Add(TagTypeKeyConstant.FieldIndication, "Sink", "entfld0001"),
            await Add(TagTypeKeyConstant.People, "Surveyor", "entpep0001"),
            await Add(TagTypeKeyConstant.CaveOther, "Distinctive", "entoth0001"));
    }

    private static async Task<string> PopulateExistingEntranceAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave, ReplacementReferences references, string managerId)
    {
        await CavePermissions.GrantManagerAsync(database, cave, managerId);
        await using var manager = await CaveTestActor.CreateAsync(database, cave.AccountId, managerId);
        var context = await manager.Services.Caves.GetEditAuthoringContextAsync(cave.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        var entrance = Assert.Single(values.Entrances);
        entrance.Name = "Original Entrance";
        entrance.Description = "Original description";
        entrance.Latitude = 35.1;
        entrance.Longitude = -86.1;
        entrance.ElevationFeet = 510;
        entrance.LocationQualityTagId = references.SurveyGradeLocationQuality;
        entrance.PitFeet = 12;
        entrance.ReportedOn = ReportedOn;
        entrance.IsPrimary = true;
        entrance.EntranceStatusTagIds = [references.Status];
        entrance.EntranceHydrologyTagIds = [references.Hydrology];
        entrance.FieldIndicationTagIds = [references.FieldIndication];
        entrance.ReportedByNameTagIds = [references.Reporter];
        entrance.EntranceOtherTagIds = [references.Other];
        await manager.Services.Caves.AddCave(values, default);
        manager.Db.ChangeTracker.Clear();
        return (await CurrentRevisionAsync(manager.Db, cave.CaveId)).Id;
    }

    private static async Task<CaveRevision> CurrentRevisionAsync(
        Planarian.Model.Database.PlanarianDbContext db, string caveId)
    {
        var currentId = await db.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == caveId)
            .Select(row => row.CurrentRevisionId)
            .SingleAsync();
        return await db.CaveRevisions.SingleAsync(row => row.Id == currentId);
    }

    private static CavePublishedSnapshotV1 ReadSnapshot(CaveRevision revision) =>
        CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);

    private sealed record EntranceReferences(
        string SurveyGradeLocationQuality,
        string EstimatedLocationQuality,
        string Status,
        string Hydrology,
        string FieldIndication,
        string Reporter,
        string Other);

    private sealed record ReplacementReferences(
        string SurveyGradeLocationQuality,
        string EstimatedLocationQuality,
        string Status,
        string Hydrology,
        string FieldIndication,
        string Reporter,
        string Other);
}
