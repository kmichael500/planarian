using System.Text;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveAuthoringFieldPersistenceIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private const string FullGeoJson =
        """{"type":"FeatureCollection","features":[{"type":"Feature","properties":{"name":"full"},"geometry":{"type":"LineString","coordinates":[[-86,35],[-85.9,35.1]]}}]}""";

    [Fact]
    public async Task ProposalLifecyclePersistsEditsAndRemovalsForEveryAuthorableField()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalLifecyclePersistsEditsAndRemovalsForEveryAuthorableField));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var references = await CreateReferenceCatalogAsync(database, tenant);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        var blobs = new TestFileBlobStore();

        string stagedFileId;
        (string Partition, string Key) stagedAddress;
        string firstRequestId;
        string firstVersionId;
        string primaryEntranceId;
        string secondaryEntranceId;
        string linePlotId;

        await using (var contributor =
                     await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor", blobs))
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("field contract bytes"));
            var staged = await contributor.Services.Files.StageAuthoringFile(content, "source.pdf", default);
            stagedFileId = staged.Id;
            var stagedRow = await contributor.Db.Files.AsNoTracking().SingleAsync(file => file.Id == stagedFileId);
            stagedAddress = (stagedRow.BlobContainer!, stagedRow.BlobKey!);

            var values = FullValues(tenant, references, stagedFileId);
            firstRequestId = await contributor.ChangeRequests.CreateAsync(
                tenant.CaveId, values, tenant.RevisionId, default);
            firstVersionId = await CurrentVersionAsync(contributor.Db, firstRequestId);

            var version = await contributor.Db.CaveProposalVersions
                .SingleAsync(row => row.Id == firstVersionId);
            var proposal = CaveProposalJson.Deserialize(version.ProposalJson, version.SchemaVersion);

            Assert.Equal("Fully Populated Cave", proposal.Name);
            Assert.Equal(["Alpha Alias", "Zulu Alias"], proposal.AlternateNames);
            Assert.Equal(references.TargetStateId, proposal.StateId);
            Assert.Equal("Field State", proposal.StateNameAtRevision);
            Assert.Equal("FS", proposal.StateAbbreviationAtRevision);
            Assert.Equal(references.TargetCountyId, proposal.CountyId);
            Assert.Equal("Field County", proposal.CountyNameAtRevision);
            Assert.Equal("F02", proposal.CountyDisplayIdAtRevision);
            Assert.Equal(CountyNumberIntent.Manual, proposal.CountyNumberIntent);
            Assert.Equal(42, proposal.RequestedCountyNumber);
            Assert.Equal(123.5, proposal.LengthFeet);
            Assert.Equal(54.25, proposal.DepthFeet);
            Assert.Equal(31.75, proposal.MaxPitDepthFeet);
            Assert.Equal(4, proposal.NumberOfPits);
            Assert.Equal("Full narrative", proposal.Narrative);
            Assert.Equal(CaveReportedOn, proposal.ReportedOn);
            Assert.False(proposal.IsArchived);
            Assert.Empty(proposal.NewPeopleTagIntents);

            AssertTag(proposal.Tags, SnapshotTagRole.Geology, references.Geology);
            AssertTag(proposal.Tags, SnapshotTagRole.GeologicAge, references.GeologicAge);
            AssertTag(proposal.Tags, SnapshotTagRole.MapStatus, references.MapStatus);
            AssertTag(proposal.Tags, SnapshotTagRole.PhysiographicProvince, references.PhysiographicProvince);
            AssertTag(proposal.Tags, SnapshotTagRole.Archeology, references.Archeology);
            AssertTag(proposal.Tags, SnapshotTagRole.Biology, references.Biology);
            AssertTag(proposal.Tags, SnapshotTagRole.CaveOther, references.CaveOther);
            AssertTag(proposal.Tags, SnapshotTagRole.Cartographer, references.Cartographer);
            AssertTag(proposal.Tags, SnapshotTagRole.CaveReportedBy, references.CaveReporter);

            Assert.Equal(2, proposal.Entrances.Count);
            var primary = Assert.Single(proposal.Entrances.Where(entrance => entrance.IsPrimary));
            primaryEntranceId = primary.EntranceId;
            Assert.False(string.IsNullOrWhiteSpace(primaryEntranceId));
            Assert.Equal("Main Entrance", primary.Name);
            Assert.Equal("Detailed entrance description", primary.Description);
            Assert.Equal(35.125, primary.Latitude);
            Assert.Equal(-86.875, primary.Longitude);
            Assert.Equal(612.25, primary.Elevation);
            Assert.Equal(references.LocationQuality.Id, primary.LocationQualityTagId);
            Assert.Equal(references.LocationQuality.Name, primary.LocationQualityNameAtRevision);
            Assert.Equal(EntranceReportedOn, primary.ReportedOn);
            Assert.Equal(88.5, primary.PitDepthFeet);
            Assert.Empty(primary.NewPeopleTagIntents);
            AssertTag(primary.Tags, SnapshotTagRole.EntranceStatus, references.EntranceStatus);
            AssertTag(primary.Tags, SnapshotTagRole.EntranceHydrology, references.EntranceHydrology);
            AssertTag(primary.Tags, SnapshotTagRole.FieldIndication, references.FieldIndication);
            AssertTag(primary.Tags, SnapshotTagRole.EntranceReportedBy, references.EntranceReporter);
            AssertTag(primary.Tags, SnapshotTagRole.EntranceOther, references.EntranceOther);

            var secondary = Assert.Single(proposal.Entrances.Where(entrance => !entrance.IsPrimary));
            secondaryEntranceId = secondary.EntranceId;
            Assert.False(string.IsNullOrWhiteSpace(secondaryEntranceId));
            Assert.Equal("Secondary Entrance", secondary.Name);

            var file = Assert.Single(proposal.Files);
            Assert.Equal(stagedFileId, file.FileId);
            Assert.Equal(ProposalFileDisposition.PublishStaged, file.Disposition);
            Assert.Equal(references.SurveyFile.Id, file.FileTypeTagId);
            Assert.Equal(references.SurveyFile.Name, file.FileTypeName);
            Assert.Equal("Field evidence", file.Name);
            Assert.Equal(".pdf", file.Extension);

            var linePlot = Assert.Single(proposal.LinePlots);
            linePlotId = linePlot.Id;
            Assert.False(string.IsNullOrWhiteSpace(linePlotId));
            Assert.Equal("Main Traverse", linePlot.Name);
            Assert.Equal(CaveJsonContent.NormalizeFeatureCollection(FullGeoJson), linePlot.GeoJson);
        }

        string firstRevisionId;
        await using (var reviewer =
                     await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
        {
            firstRevisionId = (await reviewer.ChangeRequests.ApproveAsync(
                firstRequestId, firstVersionId, "Full field approval", default)).PublishedRevisionId!;
        }

        string firstRevisionJson;
        await using (var verify =
                     await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
        {
            verify.Db.ChangeTracker.Clear();
            var live = await new CaveRepository(verify.Db, verify.Db.RequestUser).GetCave(tenant.CaveId);
            Assert.NotNull(live);
            Assert.Equal(references.TargetStateId, live!.StateId);
            Assert.Equal(references.TargetCountyId, live.CountyId);
            Assert.Equal(42, live.CountyNumber);
            Assert.Equal("Fully Populated Cave", live.Name);
            Assert.Equal(["Alpha Alias", "Zulu Alias"], live.AlternateNames);
            Assert.Equal(123.5, live.LengthFeet);
            Assert.Equal(54.25, live.DepthFeet);
            Assert.Equal(31.75, live.MaxPitDepthFeet);
            Assert.Equal(4, live.NumberOfPits);
            Assert.Equal("Full narrative", live.Narrative);
            Assert.Equal(CaveReportedOn, live.ReportedOn);
            Assert.Equal([references.Geology.Id], live.GeologyTagIds);
            Assert.Equal([references.GeologicAge.Id], live.GeologicAgeTagIds);
            Assert.Equal([references.MapStatus.Id], live.MapStatusTagIds);
            Assert.Equal([references.PhysiographicProvince.Id], live.PhysiographicProvinceTagIds);
            Assert.Equal([references.Archeology.Id], live.ArcheologyTagIds);
            Assert.Equal([references.Biology.Id], live.BiologyTagIds);
            Assert.Equal([references.CaveOther.Id], live.OtherTagIds);
            Assert.Equal([references.Cartographer.Id], live.CartographerNameTagIds);
            Assert.Equal([references.CaveReporter.Id], live.ReportedByNameTagIds);

            var livePrimary = Assert.Single(live.Entrances.Where(entrance => entrance.Id == primaryEntranceId));
            Assert.True(livePrimary.IsPrimary);
            Assert.Equal("Main Entrance", livePrimary.Name);
            Assert.Equal("Detailed entrance description", livePrimary.Description);
            Assert.Equal(35.125, livePrimary.Latitude);
            Assert.Equal(-86.875, livePrimary.Longitude);
            Assert.Equal(612.25, livePrimary.ElevationFeet);
            Assert.Equal(references.LocationQuality.Id, livePrimary.LocationQualityTagId);
            Assert.Equal(EntranceReportedOn, livePrimary.ReportedOn);
            Assert.Equal(88.5, livePrimary.PitFeet);
            Assert.Equal([references.EntranceStatus.Id], livePrimary.EntranceStatusTagIds);
            Assert.Equal([references.EntranceHydrology.Id], livePrimary.EntranceHydrologyTagIds);
            Assert.Equal([references.FieldIndication.Id], livePrimary.FieldIndicationTagIds);
            Assert.Equal([references.EntranceReporter.Id], livePrimary.ReportedByNameTagIds);
            Assert.Equal([references.EntranceOther.Id], livePrimary.EntranceOtherTagIds);
            Assert.Contains(live.Entrances, entrance => entrance.Id == secondaryEntranceId);

            var liveFile = Assert.Single(await verify.Db.Files.AsNoTracking()
                .Where(row => row.CaveId == tenant.CaveId).ToListAsync());
            Assert.Equal(stagedFileId, liveFile.Id);
            Assert.Equal(references.SurveyFile.Id, liveFile.FileTypeTagId);
            Assert.Equal("Field evidence", liveFile.Name);
            Assert.Equal(".pdf", liveFile.Extension);
            Assert.Equal(stagedAddress, (liveFile.BlobContainer!, liveFile.BlobKey!));
            Assert.Null(liveFile.ExpiresOn);

            var liveLinePlot = Assert.Single(await verify.Db.CaveGeoJsons.AsNoTracking()
                .Where(row => row.CaveId == tenant.CaveId).ToListAsync());
            Assert.Equal(linePlotId, liveLinePlot.Id);
            Assert.Equal("Main Traverse", liveLinePlot.Name);
            Assert.Equal(CaveJsonContent.NormalizeFeatureCollection(FullGeoJson),
                CaveJsonContent.NormalizeFeatureCollection(liveLinePlot.GeoJson));

            var revision = await verify.Db.CaveRevisions.SingleAsync(row => row.Id == firstRevisionId);
            var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
            firstRevisionJson = CaveSnapshotJson.Serialize(snapshot);
            AssertFullPublishedSnapshot(snapshot, references, stagedFileId, primaryEntranceId, secondaryEntranceId,
                linePlotId);
            var rebuilt = await new CavePublishedSnapshotRepository(verify.Db, verify.Db.RequestUser)
                .BuildAsync(tenant.CaveId);
            Assert.Equal(CaveSnapshotJson.Serialize(rebuilt), CaveSnapshotJson.Serialize(snapshot));
        }

        string secondRequestId;
        string secondVersionId;
        await using (var contributor =
                     await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor", blobs))
        {
            var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
            var cleared = ValuesFromCave(context.Cave);
            cleared.Name = "Cleared Cave";
            cleared.AlternateNames = [];
            cleared.StateId = tenant.StateId;
            cleared.CountyId = tenant.CountyId;
            cleared.IsCountyNumberManuallySet = true;
            cleared.UseFirstAvailableCountyNumber = false;
            cleared.CountyNumber = tenant.CountyNumber;
            cleared.LengthFeet = null;
            cleared.DepthFeet = null;
            cleared.MaxPitDepthFeet = null;
            cleared.NumberOfPits = null;
            cleared.Narrative = null;
            cleared.ReportedOn = null;
            cleared.GeologyTagIds = [];
            cleared.GeologicAgeTagIds = [];
            cleared.MapStatusTagIds = [];
            cleared.PhysiographicProvinceTagIds = [];
            cleared.ArcheologyTagIds = [];
            cleared.BiologyTagIds = [];
            cleared.OtherTagIds = [];
            cleared.CartographerNameTagIds = [];
            cleared.ReportedByNameTagIds = [];
            cleared.Files = [];
            cleared.LinePlots = [];
            cleared.Entrances =
            [
                new AddEntranceVm
                {
                    Id = primaryEntranceId,
                    IsPrimary = true,
                    LocationQualityTagId = references.RevisedLocationQuality.Id,
                    Name = null,
                    Description = null,
                    Latitude = 0.25,
                    Longitude = -0.5,
                    ElevationFeet = 0,
                    ReportedOn = null,
                    PitFeet = null,
                    EntranceStatusTagIds = [],
                    EntranceHydrologyTagIds = [],
                    FieldIndicationTagIds = [],
                    ReportedByNameTagIds = [],
                    EntranceOtherTagIds = []
                }
            ];

            secondRequestId = await contributor.ChangeRequests.CreateAsync(
                tenant.CaveId, cleared, context.ExpectedBaseRevisionId, default);
            secondVersionId = await CurrentVersionAsync(contributor.Db, secondRequestId);

            var persisted = await contributor.Db.CaveProposalVersions
                .SingleAsync(row => row.Id == secondVersionId);
            var proposal = CaveProposalJson.Deserialize(persisted.ProposalJson, persisted.SchemaVersion);
            Assert.Equal("Cleared Cave", proposal.Name);
            Assert.Empty(proposal.AlternateNames);
            Assert.Equal(tenant.StateId, proposal.StateId);
            Assert.Equal(tenant.CountyId, proposal.CountyId);
            Assert.Null(proposal.LengthFeet);
            Assert.Null(proposal.DepthFeet);
            Assert.Null(proposal.MaxPitDepthFeet);
            Assert.Null(proposal.NumberOfPits);
            Assert.Null(proposal.Narrative);
            Assert.Null(proposal.ReportedOn);
            Assert.Empty(proposal.Tags);
            var removedFile = Assert.Single(proposal.Files);
            Assert.Equal(stagedFileId, removedFile.FileId);
            Assert.Equal(ProposalFileDisposition.RemovePublished, removedFile.Disposition);
            Assert.Null(removedFile.FileTypeTagId);
            Assert.Null(removedFile.Name);
            Assert.Empty(proposal.LinePlots);

            var retainedEntrance = Assert.Single(proposal.Entrances);
            Assert.Equal(primaryEntranceId, retainedEntrance.EntranceId);
            Assert.True(retainedEntrance.IsPrimary);
            Assert.Null(retainedEntrance.Name);
            Assert.Null(retainedEntrance.Description);
            Assert.Equal(0.25, retainedEntrance.Latitude);
            Assert.Equal(-0.5, retainedEntrance.Longitude);
            Assert.Equal(0, retainedEntrance.Elevation);
            Assert.Equal(references.RevisedLocationQuality.Id, retainedEntrance.LocationQualityTagId);
            Assert.Null(retainedEntrance.ReportedOn);
            Assert.Null(retainedEntrance.PitDepthFeet);
            Assert.Empty(retainedEntrance.Tags);
        }

        string secondRevisionId;
        await using (var reviewer =
                     await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
        {
            secondRevisionId = (await reviewer.ChangeRequests.ApproveAsync(
                secondRequestId, secondVersionId, "Clear field approval", default)).PublishedRevisionId!;
        }

        await using (var verify =
                     await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer", blobs))
        {
            verify.Db.ChangeTracker.Clear();
            var live = await new CaveRepository(verify.Db, verify.Db.RequestUser).GetCave(tenant.CaveId);
            Assert.NotNull(live);
            Assert.Equal("Cleared Cave", live!.Name);
            Assert.Equal(tenant.StateId, live.StateId);
            Assert.Equal(tenant.CountyId, live.CountyId);
            Assert.Equal(tenant.CountyNumber, live.CountyNumber);
            Assert.Empty(live.AlternateNames);
            Assert.Null(live.LengthFeet);
            Assert.Null(live.DepthFeet);
            Assert.Null(live.MaxPitDepthFeet);
            Assert.Null(live.NumberOfPits);
            Assert.Null(live.Narrative);
            Assert.Null(live.ReportedOn);
            Assert.Empty(live.GeologyTagIds);
            Assert.Empty(live.GeologicAgeTagIds);
            Assert.Empty(live.MapStatusTagIds);
            Assert.Empty(live.PhysiographicProvinceTagIds);
            Assert.Empty(live.ArcheologyTagIds);
            Assert.Empty(live.BiologyTagIds);
            Assert.Empty(live.OtherTagIds);
            Assert.Empty(live.CartographerNameTagIds);
            Assert.Empty(live.ReportedByNameTagIds);
            Assert.Empty(live.Files);

            var entrance = Assert.Single(live.Entrances);
            Assert.Equal(primaryEntranceId, entrance.Id);
            Assert.True(entrance.IsPrimary);
            Assert.Equal(references.RevisedLocationQuality.Id, entrance.LocationQualityTagId);
            Assert.Null(entrance.Name);
            Assert.Null(entrance.Description);
            Assert.Equal(0.25, entrance.Latitude);
            Assert.Equal(-0.5, entrance.Longitude);
            Assert.Equal(0, entrance.ElevationFeet);
            Assert.Null(entrance.ReportedOn);
            Assert.Null(entrance.PitFeet);
            Assert.Empty(entrance.EntranceStatusTagIds);
            Assert.Empty(entrance.EntranceHydrologyTagIds);
            Assert.Empty(entrance.FieldIndicationTagIds);
            Assert.Empty(entrance.ReportedByNameTagIds);
            Assert.Empty(entrance.EntranceOtherTagIds);

            Assert.False(await verify.Db.Entrances.AsNoTracking()
                .AnyAsync(row => row.Id == secondaryEntranceId));
            Assert.False(await verify.Db.Files.AsNoTracking().AnyAsync(row => row.Id == stagedFileId));
            Assert.False(await verify.Db.StorageCleanupTargets.AsNoTracking().AnyAsync(row =>
                row.Purpose == StorageCleanupPurpose.CaveAssetRemoval));
            Assert.False(blobs.Contains(stagedAddress.Partition, stagedAddress.Key));
            Assert.False(await verify.Db.CaveGeoJsons.AsNoTracking().AnyAsync(row => row.Id == linePlotId));

            var revision = await verify.Db.CaveRevisions.SingleAsync(row => row.Id == secondRevisionId);
            var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
            Assert.Equal("Cleared Cave", snapshot.Name);
            Assert.Empty(snapshot.AlternateNames);
            Assert.Equal(tenant.StateId, snapshot.State.Id);
            Assert.Equal(tenant.CountyId, snapshot.County.Id);
            Assert.Equal(tenant.CountyNumber, snapshot.CountyNumber);
            Assert.Null(snapshot.LengthFeet);
            Assert.Null(snapshot.DepthFeet);
            Assert.Null(snapshot.MaxPitDepthFeet);
            Assert.Null(snapshot.NumberOfPits);
            Assert.Null(snapshot.Narrative);
            Assert.Null(snapshot.ReportedOn);
            Assert.Empty(snapshot.Tags);
            Assert.Empty(snapshot.Files);
            Assert.Empty(snapshot.LinePlots);
            var snapshotEntrance = Assert.Single(snapshot.Entrances);
            Assert.Equal(primaryEntranceId, snapshotEntrance.Id);
            Assert.Null(snapshotEntrance.Name);
            Assert.Null(snapshotEntrance.Description);
            Assert.Equal(0.25, snapshotEntrance.Latitude);
            Assert.Equal(-0.5, snapshotEntrance.Longitude);
            Assert.Equal(0, snapshotEntrance.Elevation);
            Assert.Equal(references.RevisedLocationQuality.Id, snapshotEntrance.LocationQualityTagId);
            Assert.Null(snapshotEntrance.ReportedOn);
            Assert.Null(snapshotEntrance.PitDepthFeet);
            Assert.Empty(snapshotEntrance.Tags);

            var rebuilt = await new CavePublishedSnapshotRepository(verify.Db, verify.Db.RequestUser)
                .BuildAsync(tenant.CaveId);
            Assert.Equal(CaveSnapshotJson.Serialize(rebuilt), CaveSnapshotJson.Serialize(snapshot));

            var historical = await verify.Db.CaveRevisions.SingleAsync(row => row.Id == firstRevisionId);
            var historicalSnapshot = CaveSnapshotJson.Deserialize(
                historical.SnapshotJson, historical.SnapshotSchemaVersion);
            Assert.Equal(firstRevisionJson, CaveSnapshotJson.Serialize(historicalSnapshot));
        }
    }


    [Fact]
    public async Task DirectManagerLifecyclePersistsAndClearsEveryScalarAndEntranceField()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectManagerLifecyclePersistsAndClearsEveryScalarAndEntranceField));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var references = await CreateReferenceCatalogAsync(database, tenant);
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        string primaryEntranceId;
        string secondaryEntranceId;
        string firstRevisionId;

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var values = PublishableValues(tenant, references.LocationQuality.Id, "Direct Full Cave");
            values.AlternateNames = ["Direct Zulu", "Direct Alpha"];
            values.StateId = references.TargetStateId;
            values.CountyId = references.TargetCountyId;
            values.IsCountyNumberManuallySet = true;
            values.UseFirstAvailableCountyNumber = false;
            values.CountyNumber = 43;
            values.LengthFeet = 321.5;
            values.DepthFeet = 87.25;
            values.MaxPitDepthFeet = 44.75;
            values.NumberOfPits = 6;
            values.Narrative = "Direct narrative";
            values.ReportedOn = CaveReportedOn;
            values.Entrances =
            [
                new AddEntranceVm
                {
                    IsPrimary = true,
                    LocationQualityTagId = references.LocationQuality.Id,
                    Name = "Direct Main",
                    Description = "Direct description",
                    Latitude = 36.125,
                    Longitude = -85.875,
                    ElevationFeet = 701.5,
                    ReportedOn = EntranceReportedOn,
                    PitFeet = 91.25
                },
                new AddEntranceVm
                {
                    IsPrimary = false,
                    LocationQualityTagId = references.LocationQuality.Id,
                    Name = "Direct Secondary",
                    Description = "Remove me later",
                    Latitude = 36.25,
                    Longitude = -85.75,
                    ElevationFeet = 450.5,
                    ReportedOn = EntranceReportedOn,
                    PitFeet = 12.5
                }
            ];

            await manager.Services.Caves.AddCave(values, default);
            manager.Db.ChangeTracker.Clear();

            var live = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            Assert.NotNull(live);
            Assert.Equal(references.TargetStateId, live!.StateId);
            Assert.Equal(references.TargetCountyId, live.CountyId);
            Assert.Equal(43, live.CountyNumber);
            Assert.Equal("Direct Full Cave", live.Name);
            Assert.Equal(["Direct Alpha", "Direct Zulu"], live.AlternateNames);
            Assert.Equal(321.5, live.LengthFeet);
            Assert.Equal(87.25, live.DepthFeet);
            Assert.Equal(44.75, live.MaxPitDepthFeet);
            Assert.Equal(6, live.NumberOfPits);
            Assert.Equal("Direct narrative", live.Narrative);
            Assert.Equal(CaveReportedOn, live.ReportedOn);

            var primary = Assert.Single(live.Entrances.Where(entrance => entrance.IsPrimary));
            primaryEntranceId = primary.Id;
            Assert.Equal("Direct Main", primary.Name);
            Assert.Equal("Direct description", primary.Description);
            Assert.Equal(36.125, primary.Latitude);
            Assert.Equal(-85.875, primary.Longitude);
            Assert.Equal(701.5, primary.ElevationFeet);
            Assert.Equal(references.LocationQuality.Id, primary.LocationQualityTagId);
            Assert.Equal(EntranceReportedOn, primary.ReportedOn);
            Assert.Equal(91.25, primary.PitFeet);

            var secondary = Assert.Single(live.Entrances.Where(entrance => !entrance.IsPrimary));
            secondaryEntranceId = secondary.Id;
            Assert.Equal("Direct Secondary", secondary.Name);
            Assert.Equal("Remove me later", secondary.Description);
            Assert.Equal(36.25, secondary.Latitude);
            Assert.Equal(-85.75, secondary.Longitude);
            Assert.Equal(450.5, secondary.ElevationFeet);
            Assert.Equal(12.5, secondary.PitFeet);

            firstRevisionId = live.CurrentRevisionId!;
            var revision = await manager.Db.CaveRevisions.SingleAsync(row => row.Id == firstRevisionId);
            var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
            var rebuilt = await new CavePublishedSnapshotRepository(manager.Db, manager.Db.RequestUser)
                .BuildAsync(tenant.CaveId);
            Assert.Equal(CaveSnapshotJson.Serialize(rebuilt), CaveSnapshotJson.Serialize(snapshot));
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.Name = "Direct Cleared Cave";
            values.AlternateNames = [];
            values.StateId = tenant.StateId;
            values.CountyId = tenant.CountyId;
            values.IsCountyNumberManuallySet = true;
            values.UseFirstAvailableCountyNumber = false;
            values.CountyNumber = tenant.CountyNumber;
            values.LengthFeet = null;
            values.DepthFeet = null;
            values.MaxPitDepthFeet = null;
            values.NumberOfPits = null;
            values.Narrative = null;
            values.ReportedOn = null;
            values.Entrances =
            [
                new AddEntranceVm
                {
                    Id = primaryEntranceId,
                    IsPrimary = true,
                    LocationQualityTagId = references.RevisedLocationQuality.Id,
                    Name = null,
                    Description = null,
                    Latitude = 0.75,
                    Longitude = -1.25,
                    ElevationFeet = 0,
                    ReportedOn = null,
                    PitFeet = null
                }
            ];

            await manager.Services.Caves.AddCave(values, default);
            manager.Db.ChangeTracker.Clear();

            var live = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            Assert.NotNull(live);
            Assert.Equal("Direct Cleared Cave", live!.Name);
            Assert.Equal(tenant.StateId, live.StateId);
            Assert.Equal(tenant.CountyId, live.CountyId);
            Assert.Equal(tenant.CountyNumber, live.CountyNumber);
            Assert.Empty(live.AlternateNames);
            Assert.Null(live.LengthFeet);
            Assert.Null(live.DepthFeet);
            Assert.Null(live.MaxPitDepthFeet);
            Assert.Null(live.NumberOfPits);
            Assert.Null(live.Narrative);
            Assert.Null(live.ReportedOn);

            var entrance = Assert.Single(live.Entrances);
            Assert.Equal(primaryEntranceId, entrance.Id);
            Assert.True(entrance.IsPrimary);
            Assert.Equal(references.RevisedLocationQuality.Id, entrance.LocationQualityTagId);
            Assert.Null(entrance.Name);
            Assert.Null(entrance.Description);
            Assert.Equal(0.75, entrance.Latitude);
            Assert.Equal(-1.25, entrance.Longitude);
            Assert.Equal(0, entrance.ElevationFeet);
            Assert.Null(entrance.ReportedOn);
            Assert.Null(entrance.PitFeet);

            Assert.False(await manager.Db.Entrances.AsNoTracking()
                .AnyAsync(row => row.Id == secondaryEntranceId));
            Assert.NotEqual(firstRevisionId, live.CurrentRevisionId);
            Assert.Equal(3, await manager.Db.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));

            var revision = await manager.Db.CaveRevisions.SingleAsync(row => row.Id == live.CurrentRevisionId);
            var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
            var rebuilt = await new CavePublishedSnapshotRepository(manager.Db, manager.Db.RequestUser)
                .BuildAsync(tenant.CaveId);
            Assert.Equal(CaveSnapshotJson.Serialize(rebuilt), CaveSnapshotJson.Serialize(snapshot));
        }
    }

    [Fact]
    public async Task NewCaveCreationPersistsEveryAuthorableFieldAndCreatesOneCompleteRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NewCaveCreationPersistsEveryAuthorableFieldAndCreatesOneCompleteRevision));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var shell = new PublishedCaveTestData(
            account.AccountId, account.StateId, account.StateName, account.StateAbbreviation,
            account.CountyId, account.CountyName, account.CountyDisplayId,
            string.Empty, string.Empty, 1, string.Empty);
        var references = await CreateReferenceCatalogAsync(database, shell);
        await GrantScopedManagerAsync(database, account.AccountId, references.TargetCountyId, "manager");
        var blobs = new TestFileBlobStore();

        string stagedFileId;
        string createdCaveId;
        await using (var manager = await CaveTestActor.CreateAsync(database, account.AccountId, "manager", blobs))
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("new Cave field contract bytes"));
            var staged = await manager.Services.Files.StageAuthoringFile(content, "source.pdf", default);
            stagedFileId = staged.Id;

            var values = FullValues(shell, references, stagedFileId);
            values.Id = null;
            values.ExpectedRevisionId = null;

            createdCaveId = await manager.Services.Caves.AddCave(values, default);
            Assert.False(string.IsNullOrWhiteSpace(createdCaveId));
        }

        await using var verify = await CaveTestActor.CreateAsync(database, account.AccountId, "manager", blobs);
        verify.Db.ChangeTracker.Clear();
        var live = await new CaveRepository(verify.Db, verify.Db.RequestUser).GetCave(createdCaveId);
        Assert.NotNull(live);
        Assert.Equal("Fully Populated Cave", live!.Name);
        Assert.Equal(["Alpha Alias", "Zulu Alias"], live.AlternateNames);
        Assert.Equal(references.TargetStateId, live.StateId);
        Assert.Equal(references.TargetCountyId, live.CountyId);
        Assert.Equal(42, live.CountyNumber);
        Assert.Equal(123.5, live.LengthFeet);
        Assert.Equal(54.25, live.DepthFeet);
        Assert.Equal(31.75, live.MaxPitDepthFeet);
        Assert.Equal(4, live.NumberOfPits);
        Assert.Equal("Full narrative", live.Narrative);
        Assert.Equal(CaveReportedOn, live.ReportedOn);
        Assert.Equal([references.Geology.Id], live.GeologyTagIds);
        Assert.Equal([references.GeologicAge.Id], live.GeologicAgeTagIds);
        Assert.Equal([references.MapStatus.Id], live.MapStatusTagIds);
        Assert.Equal([references.PhysiographicProvince.Id], live.PhysiographicProvinceTagIds);
        Assert.Equal([references.Archeology.Id], live.ArcheologyTagIds);
        Assert.Equal([references.Biology.Id], live.BiologyTagIds);
        Assert.Equal([references.CaveOther.Id], live.OtherTagIds);
        Assert.Equal([references.Cartographer.Id], live.CartographerNameTagIds);
        Assert.Equal([references.CaveReporter.Id], live.ReportedByNameTagIds);

        var primary = Assert.Single(live.Entrances.Where(entrance => entrance.IsPrimary));
        var secondary = Assert.Single(live.Entrances.Where(entrance => !entrance.IsPrimary));
        Assert.Equal("Main Entrance", primary.Name);
        Assert.Equal("Detailed entrance description", primary.Description);
        Assert.Equal(references.LocationQuality.Id, primary.LocationQualityTagId);
        Assert.Equal([references.EntranceStatus.Id], primary.EntranceStatusTagIds);
        Assert.Equal([references.EntranceHydrology.Id], primary.EntranceHydrologyTagIds);
        Assert.Equal([references.FieldIndication.Id], primary.FieldIndicationTagIds);
        Assert.Equal([references.EntranceReporter.Id], primary.ReportedByNameTagIds);
        Assert.Equal([references.EntranceOther.Id], primary.EntranceOtherTagIds);

        var file = Assert.Single(await verify.Db.Files.AsNoTracking()
            .Where(row => row.CaveId == createdCaveId).ToListAsync());
        Assert.Equal(stagedFileId, file.Id);
        Assert.Equal(references.SurveyFile.Id, file.FileTypeTagId);
        Assert.Equal("Field evidence", file.Name);
        Assert.Null(file.ExpiresOn);

        var linePlot = Assert.Single(await verify.Db.CaveGeoJsons.AsNoTracking()
            .Where(row => row.CaveId == createdCaveId).ToListAsync());
        Assert.Equal("Main Traverse", linePlot.Name);

        var revisions = await verify.Db.CaveRevisions.Where(row => row.CaveId == createdCaveId).ToListAsync();
        var revision = Assert.Single(revisions);
        Assert.Equal(CaveRevisionSource.ManagerEdit, revision.Source);
        Assert.Equal(CaveRevisionOperation.Create, revision.Operation);
        Assert.Null(revision.PreviousRevisionId);
        Assert.Equal(revision.Id, live.CurrentRevisionId);

        var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
        AssertFullPublishedSnapshot(snapshot, references, stagedFileId, primary.Id, secondary.Id, linePlot.Id);
        var rebuilt = await new CavePublishedSnapshotRepository(verify.Db, verify.Db.RequestUser)
            .BuildAsync(createdCaveId);
        Assert.Equal(CaveSnapshotJson.Serialize(rebuilt), CaveSnapshotJson.Serialize(snapshot));
    }

    [Fact]
    public async Task DirectEditAndProposalApprovalProduceIdenticalPublishedStateForTheSameIntent()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectEditAndProposalApprovalProduceIdenticalPublishedStateForTheSameIntent));
        var (tenant, locationQualityId) =
            await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var biology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Biology, "Parity Bat", "parbio0001");
        var hydrology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.EntranceHydrology, "Parity Wet", "parhyd0001");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        AddCaveVm baselineValues;
        CavePublishedSnapshotV1 directSnapshot;
        string directRevisionId;

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var baselineContext = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            baselineValues = ValuesFromCave(baselineContext.Cave);

            var directTarget = ValuesFromCave(baselineContext.Cave);
            ApplyParityTarget(directTarget, biology.Id, hydrology.Id);
            await manager.Services.Caves.AddCave(directTarget, default);
            manager.Db.ChangeTracker.Clear();

            var directLive = await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(tenant.CaveId);
            directRevisionId = directLive!.CurrentRevisionId!;
            var directRevision = await manager.Db.CaveRevisions.SingleAsync(row => row.Id == directRevisionId);
            directSnapshot = CaveSnapshotJson.Deserialize(
                directRevision.SnapshotJson, directRevision.SnapshotSchemaVersion);

            baselineValues.ExpectedRevisionId = directRevisionId;
            await manager.Services.Caves.AddCave(baselineValues, default);
        }

        string requestId;
        string proposalVersionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
            var proposalTarget = ValuesFromCave(context.Cave);
            ApplyParityTarget(proposalTarget, biology.Id, hydrology.Id);
            requestId = await contributor.ChangeRequests.CreateAsync(
                tenant.CaveId, proposalTarget, context.ExpectedBaseRevisionId, default);
            proposalVersionId = await CurrentVersionAsync(contributor.Db, requestId);
        }

        string approvedRevisionId;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            approvedRevisionId = (await reviewer.ChangeRequests.ApproveAsync(
                requestId, proposalVersionId, "Parity approval", default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var approvedRevision = await verify.CaveRevisions.SingleAsync(row => row.Id == approvedRevisionId);
        var approvedSnapshot = CaveSnapshotJson.Deserialize(
            approvedRevision.SnapshotJson, approvedRevision.SnapshotSchemaVersion);

        Assert.True(new CaveRevisionDiffService().IsSemanticEqual(directSnapshot, approvedSnapshot));
        Assert.Equal(CaveSnapshotJson.Serialize(directSnapshot), CaveSnapshotJson.Serialize(approvedSnapshot));
        Assert.NotEqual(directRevisionId, approvedRevisionId);
    }

    private static void ApplyParityTarget(AddCaveVm values, string biologyTagId, string hydrologyTagId)
    {
        values.Name = "Path Parity Cave";
        values.AlternateNames = ["Parity Alpha", "Parity Zulu"];
        values.LengthFeet = 234.5;
        values.DepthFeet = 67.25;
        values.MaxPitDepthFeet = 23.75;
        values.NumberOfPits = 5;
        values.Narrative = "Identical desired state";
        values.ReportedOn = CaveReportedOn;
        values.BiologyTagIds = [biologyTagId];

        var entrance = values.Entrances.Single();
        entrance.Name = "Parity Entrance";
        entrance.Description = "Identical entrance state";
        entrance.Latitude = 35.75;
        entrance.Longitude = -86.25;
        entrance.ElevationFeet = 345.5;
        entrance.ReportedOn = EntranceReportedOn;
        entrance.PitFeet = 21.5;
        entrance.EntranceHydrologyTagIds = [hydrologyTagId];
    }

    private static async Task GrantScopedManagerAsync(
        PostgresTestDatabase database, string accountId, string countyId, string userId)
    {
        const string permissionId = "mAnageR000";
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = permissionId,
            Key = "Manager",
            Name = "Manager",
            Description = "Manage Caves",
            PermissionType = "Cave"
        });

        await using var db = database.CreateDbContext(userId, accountId);
        await CavePermissions.EnsureAccountUserAsync(db, accountId);
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = accountId,
            CountyId = countyId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }

    private static readonly DateTime CaveReportedOn =
        new(2025, 5, 6, 7, 8, 9, DateTimeKind.Utc);

    private static readonly DateTime EntranceReportedOn =
        new(2025, 5, 7, 8, 9, 10, DateTimeKind.Utc);

    internal static AddCaveVm FullValues(PublishedCaveTestData tenant, ReferenceCatalog references, string stagedFileId) =>
        new()
        {
            Id = tenant.CaveId,
            ExpectedRevisionId = tenant.RevisionId,
            Name = " Fully Populated Cave ",
            AlternateNames = ["Zulu Alias", "Alpha Alias"],
            StateId = references.TargetStateId,
            CountyId = references.TargetCountyId,
            IsCountyNumberManuallySet = true,
            UseFirstAvailableCountyNumber = false,
            CountyNumber = 42,
            LengthFeet = 123.5,
            DepthFeet = 54.25,
            MaxPitDepthFeet = 31.75,
            NumberOfPits = 4,
            Narrative = " Full narrative ",
            ReportedOn = CaveReportedOn,
            GeologyTagIds = [references.Geology.Id],
            GeologicAgeTagIds = [references.GeologicAge.Id],
            MapStatusTagIds = [references.MapStatus.Id],
            PhysiographicProvinceTagIds = [references.PhysiographicProvince.Id],
            ArcheologyTagIds = [references.Archeology.Id],
            BiologyTagIds = [references.Biology.Id],
            OtherTagIds = [references.CaveOther.Id],
            CartographerNameTagIds = [references.Cartographer.Id],
            ReportedByNameTagIds = [references.CaveReporter.Id],
            Entrances =
            [
                new AddEntranceVm
                {
                    IsPrimary = true,
                    LocationQualityTagId = references.LocationQuality.Id,
                    Name = "Main Entrance",
                    Description = "Detailed entrance description",
                    Latitude = 35.125,
                    Longitude = -86.875,
                    ElevationFeet = 612.25,
                    ReportedOn = EntranceReportedOn,
                    PitFeet = 88.5,
                    EntranceStatusTagIds = [references.EntranceStatus.Id],
                    EntranceHydrologyTagIds = [references.EntranceHydrology.Id],
                    FieldIndicationTagIds = [references.FieldIndication.Id],
                    ReportedByNameTagIds = [references.EntranceReporter.Id],
                    EntranceOtherTagIds = [references.EntranceOther.Id]
                },
                new AddEntranceVm
                {
                    IsPrimary = false,
                    LocationQualityTagId = references.LocationQuality.Id,
                    Name = "Secondary Entrance",
                    Latitude = 35.25,
                    Longitude = -86.75,
                    ElevationFeet = 500
                }
            ],
            Files =
            [
                new EditFileMetadataVm
                {
                    Id = stagedFileId,
                    FileTypeTagId = references.SurveyFile.Id,
                    Name = "Field evidence"
                }
            ],
            LinePlots = [new GeoJsonUploadVm { Name = "Main Traverse", GeoJson = FullGeoJson }]
        };

    internal static async Task<ReferenceCatalog> CreateReferenceCatalogAsync(
        PostgresTestDatabase database, PublishedCaveTestData tenant)
    {
        const string targetStateId = "statefld2";
        const string targetCountyId = "countyfld2";
        await GlobalStateTestData.AddAsync(database, targetStateId, "Field State", "FS");
        await using (var seed = database.CreateDbContext("field-location-seed", tenant.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = targetCountyId,
                AccountId = tenant.AccountId,
                StateId = targetStateId,
                DisplayId = "F02",
                Name = "Field County"
            });
            await seed.SaveChangesAsync();
        }

        return new ReferenceCatalog(
            targetStateId,
            targetCountyId,
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.Geology, "Limestone", "geofld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.GeologicAge, "Mississippian", "agefld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.MapStatus, "Mapped", "mapfld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.PhysiographicProvince, "Highland Rim", "phyfld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.Archeology, "Historic", "arcfld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.Biology, "Bat", "biofld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.CaveOther, "Other cave value", "othfld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.People, "Field Cartographer", "peoplf001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.People, "Cave Reporter", "peoplf002"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.People, "Entrance Reporter", "peoplf003"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.LocationQuality, "Survey Grade", "locfld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.LocationQuality, "Estimated", "locfld002"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.EntranceStatus, "Open", "stafld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.EntranceHydrology, "Wet", "hydfld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.FieldIndication, "Karst", "indfld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.CaveOther, "Entrance other value", "eotfld001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.File, "Other", "filoth001"),
            await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
                TagTypeKeyConstant.File, "Survey", "filsur001"));
    }

    private static void AssertFullPublishedSnapshot(
        CavePublishedSnapshotV1 snapshot,
        ReferenceCatalog references,
        string fileId,
        string primaryEntranceId,
        string secondaryEntranceId,
        string linePlotId)
    {
        Assert.Equal("Fully Populated Cave", snapshot.Name);
        Assert.Equal(["Alpha Alias", "Zulu Alias"], snapshot.AlternateNames);
        Assert.Equal(references.TargetStateId, snapshot.State.Id);
        Assert.Equal("Field State", snapshot.State.NameAtRevision);
        Assert.Equal("FS", snapshot.State.AbbreviationAtRevision);
        Assert.Equal(references.TargetCountyId, snapshot.County.Id);
        Assert.Equal("Field County", snapshot.County.NameAtRevision);
        Assert.Equal("F02", snapshot.County.DisplayIdAtRevision);
        Assert.Equal(42, snapshot.CountyNumber);
        Assert.Equal(123.5, snapshot.LengthFeet);
        Assert.Equal(54.25, snapshot.DepthFeet);
        Assert.Equal(31.75, snapshot.MaxPitDepthFeet);
        Assert.Equal(4, snapshot.NumberOfPits);
        Assert.Equal("Full narrative", snapshot.Narrative);
        Assert.Equal(CaveReportedOn, snapshot.ReportedOn);
        Assert.False(snapshot.IsArchived);

        AssertTag(snapshot.Tags, SnapshotTagRole.Geology, references.Geology);
        AssertTag(snapshot.Tags, SnapshotTagRole.GeologicAge, references.GeologicAge);
        AssertTag(snapshot.Tags, SnapshotTagRole.MapStatus, references.MapStatus);
        AssertTag(snapshot.Tags, SnapshotTagRole.PhysiographicProvince, references.PhysiographicProvince);
        AssertTag(snapshot.Tags, SnapshotTagRole.Archeology, references.Archeology);
        AssertTag(snapshot.Tags, SnapshotTagRole.Biology, references.Biology);
        AssertTag(snapshot.Tags, SnapshotTagRole.CaveOther, references.CaveOther);
        AssertTag(snapshot.Tags, SnapshotTagRole.Cartographer, references.Cartographer);
        AssertTag(snapshot.Tags, SnapshotTagRole.CaveReportedBy, references.CaveReporter);

        var primary = snapshot.Entrances.Single(entrance => entrance.Id == primaryEntranceId);
        Assert.True(primary.IsPrimary);
        Assert.Equal("Main Entrance", primary.Name);
        Assert.Equal("Detailed entrance description", primary.Description);
        Assert.Equal(35.125, primary.Latitude);
        Assert.Equal(-86.875, primary.Longitude);
        Assert.Equal(612.25, primary.Elevation);
        Assert.Equal(references.LocationQuality.Id, primary.LocationQualityTagId);
        Assert.Equal(references.LocationQuality.Name, primary.LocationQualityNameAtRevision);
        Assert.Equal(EntranceReportedOn, primary.ReportedOn);
        Assert.Equal(88.5, primary.PitDepthFeet);
        AssertTag(primary.Tags, SnapshotTagRole.EntranceStatus, references.EntranceStatus);
        AssertTag(primary.Tags, SnapshotTagRole.EntranceHydrology, references.EntranceHydrology);
        AssertTag(primary.Tags, SnapshotTagRole.FieldIndication, references.FieldIndication);
        AssertTag(primary.Tags, SnapshotTagRole.EntranceReportedBy, references.EntranceReporter);
        AssertTag(primary.Tags, SnapshotTagRole.EntranceOther, references.EntranceOther);
        Assert.Contains(snapshot.Entrances, entrance => entrance.Id == secondaryEntranceId);

        var file = Assert.Single(snapshot.Files);
        Assert.Equal(fileId, file.Id);
        Assert.Equal(references.SurveyFile.Id, file.FileTypeTagId);
        Assert.Equal(references.SurveyFile.Name, file.FileTypeNameAtRevision);
        Assert.Equal("Field evidence", file.Name);
        Assert.Equal(".pdf", file.Extension);

        var linePlot = Assert.Single(snapshot.LinePlots);
        Assert.Equal(linePlotId, linePlot.Id);
        Assert.Equal("Main Traverse", linePlot.Name);
        Assert.Equal(CaveJsonContent.NormalizeAndHash(FullGeoJson).ContentHash, linePlot.ContentHash);
    }

    private static void AssertTag(
        IEnumerable<SnapshotTagReference> tags, SnapshotTagRole role, TagType expected)
    {
        var tag = Assert.Single(tags.Where(candidate => candidate.Role == role));
        Assert.Equal(expected.Id, tag.TagTypeId);
        Assert.Equal(expected.Name, tag.NameAtRevision);
    }

    internal sealed record ReferenceCatalog(
        string TargetStateId,
        string TargetCountyId,
        TagType Geology,
        TagType GeologicAge,
        TagType MapStatus,
        TagType PhysiographicProvince,
        TagType Archeology,
        TagType Biology,
        TagType CaveOther,
        TagType Cartographer,
        TagType CaveReporter,
        TagType EntranceReporter,
        TagType LocationQuality,
        TagType RevisedLocationQuality,
        TagType EntranceStatus,
        TagType EntranceHydrology,
        TagType FieldIndication,
        TagType EntranceOther,
        TagType OtherFile,
        TagType SurveyFile);
}
