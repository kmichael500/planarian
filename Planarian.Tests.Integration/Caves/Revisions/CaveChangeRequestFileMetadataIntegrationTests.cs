using Microsoft.EntityFrameworkCore;
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

public sealed class CaveChangeRequestFileMetadataIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task PublishedFileTypeAndDisplayNamePreviewMatchApprovedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileTypeAndDisplayNamePreviewMatchApprovedRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "publish00a");
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await using (var seed = database.CreateDbContext("published-file-seed", tenant.AccountId))
        {
            (await seed.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            var entity = await seed.Files.SingleAsync(row => row.Id == file.FileId);
            entity.FileName = "survey.pdf";
            entity.DisplayName = "Survey";
            await seed.SaveChangesAsync();
            var mutations = new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser));
            tenant = tenant with { RevisionId = (await mutations.PublishExistingAsync(tenant.CaveId,
                tenant.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { }))
                .RevisionId! };
        }
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var cave = await new CaveRepository(contributor.Db, contributor.Db.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Entrances = PublishableValues(tenant, locationTag.Id, cave!.Name).Entrances;
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId, FileTypeTagId = mapType.Id, DisplayName = "Survey Map"
            }];
            var service = contributor.ChangeRequests;
            var preview = await service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default);
            var before = Assert.Single(preview.Base.Files);
            var proposed = Assert.Single(preview.Proposed.Files);
            Assert.Equal(file.FileTypeId, before.FileTypeTagId);
            Assert.Equal("Report", before.FileTypeNameAtRevision);
            Assert.Equal(mapType.Id, proposed.FileTypeTagId);
            Assert.Equal("Map", proposed.FileTypeNameAtRevision);
            Assert.Equal("Survey Map.pdf", proposed.FileName);
            Assert.Contains(file.FileId, preview.Diff.ChangedFiles);
            requestId = await service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default);
        }

        string approvedRevisionId;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            var reviewed = Assert.Single(detail.Proposed.Files);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (reviewed.FileTypeTagId, reviewed.FileTypeNameAtRevision, reviewed.FileName));
            approvedRevisionId = (await reviewer.ChangeRequests.ApproveAsync(requestId,
                detail.Request.CurrentProposalVersionId, null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files);
        Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
            (acceptedFile.FileTypeTagId, acceptedFile.FileTypeNameAtRevision, acceptedFile.FileName));
    }

    [Fact]
    public async Task StagedFileSelectedTypeAndFilenameAreImmutableAndMatchApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileSelectedTypeAndFilenameAreImmutableAndMatchApproval));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "stgtype00a");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("typed staged bytes"));
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await using (var seed = database.CreateDbContext("staged-file-type-seed", tenant.AccountId))
        {
            (await seed.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            (await seed.Files.SingleAsync(row => row.Id == file.FileId)).FileName = "survey.pdf";
            await seed.SaveChangesAsync();
        }
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Staged map"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Survey", false, default);
            var stagedVersionId = await CurrentVersionAsync(contributor, requestId);
            var values = PublishableValues(tenant, locationTag.Id, "Staged map");
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId, FileTypeTagId = mapType.Id, DisplayName = "Survey Map"
            }];
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            versionId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId, values,
                againstCurrent: false, expectedBaseRevisionId: tenant.RevisionId,
                expectedProposalVersionId: stagedVersionId, default);
        }

        await using (var inspect = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(inspect, tenant.AccountId);
            var stored = await inspect.CaveProposalVersions.SingleAsync(version => version.Id == versionId);
            var intent = Assert.Single(CaveProposalJson.Deserialize(stored.ProposalJson, stored.SchemaVersion).Files,
                candidate => candidate.FileId == file.FileId);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (intent.FileTypeTagId, intent.FileTypeName, intent.FileName));
            var historical = await IntegrationTestServices.For(inspect).CaveChangeRequests.GetVersionAsync(requestId, versionId, default);
            var presented = Assert.Single(historical.Proposed.Files);
            Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
                (presented.FileTypeTagId, presented.FileTypeNameAtRevision, presented.FileName));
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await IntegrationTestServices.For(reviewer, blobs).CaveChangeRequests.ApproveAsync(requestId, versionId,
                null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files);
        Assert.Equal((mapType.Id, "Map", "Survey Map.pdf"),
            (acceptedFile.FileTypeTagId, acceptedFile.FileTypeNameAtRevision, acceptedFile.FileName));
    }

    [Fact]
    public async Task HistoricalStagedFilePresentationDoesNotDriftWhileLiveFileExists()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalStagedFilePresentationDoesNotDriftWhileLiveFileExists));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "drift0000a");
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string versionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            (await contributor.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            (await contributor.Files.SingleAsync(row => row.Id == file.FileId)).FileName = "survey.pdf";
            await contributor.SaveChangesAsync();
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Immutable staged metadata"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Version One", false, default);
            versionId = await CurrentVersionAsync(contributor, requestId);
        }

        await using (var mutate = database.CreateDbContext("live-file-mutation", tenant.AccountId))
        {
            var live = await mutate.Files.SingleAsync(row => row.Id == file.FileId);
            live.FileTypeTagId = mapType.Id;
            live.FileName = "today.png";
            live.DisplayName = "Today";
            await mutate.SaveChangesAsync();
        }

        await using var audit = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(audit, tenant.AccountId);
        var historical = await IntegrationTestServices.For(audit).CaveChangeRequests.GetVersionAsync(requestId, versionId, default);
        var presented = Assert.Single(historical.Proposed.Files);
        Assert.Equal((file.FileTypeId, "Report", "Version One.pdf", "Version One"),
            (presented.FileTypeTagId, presented.FileTypeNameAtRevision, presented.FileName,
                presented.DisplayName));
        Assert.Empty(historical.UnavailableStagedFileIds);
    }

    [Fact]
    public async Task PublishedFileProposalPreviewMatchesApprovedTypeAndEffectiveFileName()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileProposalPreviewMatchesApprovedTypeAndEffectiveFileName));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var publishedFile = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "publish00a");
        const string mapTypeId = "maptype00a";
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Map", mapTypeId);
        await using (var seed = database.CreateDbContext("file-baseline", tenant.AccountId))
        {
            var reportType = await seed.TagTypes.SingleAsync(tag => tag.Id == publishedFile.FileTypeId);
            reportType.Name = "Report";
            var file = await seed.Files.SingleAsync(candidate => candidate.Id == publishedFile.FileId);
            file.FileName = "survey.pdf";
            file.DisplayName = "Survey";
            await seed.SaveChangesAsync();
            var baseline = await new CaveMutationRepository(seed, seed.RequestUser,
                    new CavePublishedSnapshotRepository(seed, seed.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, _ => { });
            tenant = tenant with { RevisionId = baseline.RevisionId! };
        }
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        CaveChangePreviewVm preview;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Published file metadata");
            values.Files = [new EditFileMetadataVm
            {
                Id = publishedFile.FileId,
                FileTypeTagId = mapTypeId,
                DisplayName = "Renamed Survey"
            }];
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            preview = await service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default);
            requestId = await service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default);
        }

        var proposedFile = Assert.Single(preview.Proposed.Files, file => file.Id == publishedFile.FileId);
        Assert.Equal(mapTypeId, proposedFile.FileTypeTagId);
        Assert.Equal("Map", proposedFile.FileTypeNameAtRevision);
        Assert.Equal("Renamed Survey", proposedFile.DisplayName);
        Assert.Equal("Renamed Survey.pdf", proposedFile.FileName);
        Assert.Contains(publishedFile.FileId, preview.Diff.ChangedFiles);
        Assert.Equal("Report", Assert.Single(preview.Base.Files,
            file => file.Id == publishedFile.FileId).FileTypeNameAtRevision);

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files, file => file.Id == publishedFile.FileId);
        Assert.Equal(proposedFile.FileTypeTagId, acceptedFile.FileTypeTagId);
        Assert.Equal(proposedFile.FileTypeNameAtRevision, acceptedFile.FileTypeNameAtRevision);
        Assert.Equal(proposedFile.DisplayName, acceptedFile.DisplayName);
        Assert.Equal(proposedFile.FileName, acceptedFile.FileName);
    }

    [Fact]
    public async Task HistoricalStagedFileProposalMetadataRemainsImmutableAcrossVersions()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalStagedFileProposalMetadataRemainsImmutableAcrossVersions));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "stagedmeta");
        const string mapTypeId = "maptype00a";
        const string liveTypeId = "livetype0a";
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Map", mapTypeId);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Live type", liveTypeId);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionOneId;
        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var reportType = await contributor.TagTypes.SingleAsync(tag => tag.Id == stagedFile.FileTypeId);
            reportType.Name = "Report";
            var file = await contributor.Files.SingleAsync(candidate => candidate.Id == stagedFile.FileId);
            file.FileName = "survey.pdf";
            file.DisplayName = "Survey";
            await contributor.SaveChangesAsync();
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Staged file metadata"), default);
            await requests.StageFileAsync(requestId, file.Id, file.FileTypeTagId, file.DisplayName,
                reviewer: false, default);
            versionOneId = await CurrentVersionAsync(contributor, requestId);

            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Staged file metadata");
            values.Files = [new EditFileMetadataVm
            {
                Id = stagedFile.FileId,
                FileTypeTagId = mapTypeId,
                DisplayName = "Reviewed Survey"
            }];
            versionTwoId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId, values,
                againstCurrent: false, tenant.RevisionId, versionOneId, default);
        }

        await using (var inspect = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(inspect, tenant.AccountId);
            var service = IntegrationTestServices.For(inspect).CaveChangeRequests;
            var activeRow = await inspect.CaveProposalVersions.SingleAsync(version => version.Id == versionTwoId);
            var activeIntent = Assert.Single(CaveProposalJson.Deserialize(activeRow.ProposalJson,
                activeRow.SchemaVersion).Files, intent => intent.FileId == stagedFile.FileId);
            Assert.Equal(mapTypeId, activeIntent.FileTypeTagId);
            Assert.Equal("Map", activeIntent.FileTypeName);
            Assert.Equal("Reviewed Survey.pdf", activeIntent.FileName);
            var activePreview = await service.GetVersionAsync(requestId, versionTwoId, default);
            var activeFile = Assert.Single(activePreview.Proposed.Files, file => file.Id == stagedFile.FileId);
            Assert.Equal(mapTypeId, activeFile.FileTypeTagId);
            Assert.Equal("Map", activeFile.FileTypeNameAtRevision);
            Assert.Equal("Reviewed Survey.pdf", activeFile.FileName);

            var liveFile = await inspect.Files.SingleAsync(file => file.Id == stagedFile.FileId);
            liveFile.FileTypeTagId = liveTypeId;
            liveFile.FileName = "today.txt";
            liveFile.DisplayName = "Today's mutable metadata";
            await inspect.SaveChangesAsync();

            var historical = await service.GetVersionAsync(requestId, versionOneId, default);
            var historicalFile = Assert.Single(historical.Proposed.Files,
                file => file.Id == stagedFile.FileId);
            Assert.Equal(stagedFile.FileTypeId, historicalFile.FileTypeTagId);
            Assert.Equal("Report", historicalFile.FileTypeNameAtRevision);
            Assert.Equal("survey.pdf", historicalFile.FileName);
            Assert.Equal("Survey", historicalFile.DisplayName);
            Assert.Empty(historical.UnavailableStagedFileIds);

        }

    }

    [Fact]
    public async Task ApprovalPublishesMetadataFromTheActiveStagedFileProposalVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalPublishesMetadataFromTheActiveStagedFileProposalVersion));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "stagedmeta");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("metadata staged bytes"));
        const string mapTypeId = "maptype00a";
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Map", mapTypeId);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string activeVersionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            (await contributor.TagTypes.SingleAsync(tag => tag.Id == stagedFile.FileTypeId)).Name = "Report";
            var file = await contributor.Files.SingleAsync(candidate => candidate.Id == stagedFile.FileId);
            file.FileName = "survey.pdf";
            file.DisplayName = "Survey";
            await contributor.SaveChangesAsync();
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Active staged metadata"), default);
            await requests.StageFileAsync(requestId, file.Id, file.FileTypeTagId, file.DisplayName,
                reviewer: false, default);
            var firstVersionId = await CurrentVersionAsync(contributor, requestId);

            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Active staged metadata");
            values.Files = [new EditFileMetadataVm
            {
                Id = stagedFile.FileId,
                FileTypeTagId = mapTypeId,
                DisplayName = "Reviewed Survey"
            }];
            activeVersionId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(
                requestId, values, againstCurrent: false, tenant.RevisionId, firstVersionId, default);
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await IntegrationTestServices.For(reviewer, blobs).CaveChangeRequests.ApproveAsync(
                requestId, activeVersionId, null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files, file => file.Id == stagedFile.FileId);
        Assert.Equal(mapTypeId, acceptedFile.FileTypeTagId);
        Assert.Equal("Map", acceptedFile.FileTypeNameAtRevision);
        Assert.Equal("Reviewed Survey", acceptedFile.DisplayName);
        Assert.Equal("Reviewed Survey.pdf", acceptedFile.FileName);
    }
}
