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
    public async Task PublishedFileMetadataSurvivesPreviewReviewAndApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileMetadataSurvivesPreviewReviewAndApproval));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "publish00a");
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await using (var seed = database.CreateDbContext("published-file-seed", tenant.AccountId))
        {
            (await seed.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            var entity = await seed.Files.SingleAsync(row => row.Id == file.FileId);
            entity.Extension = ".pdf";
            entity.Name = "Survey";
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
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId, FileTypeTagId = mapType.Id, Name = "Survey Map"
            }];
            var service = contributor.ChangeRequests;
            var preview = await service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default);
            var before = Assert.Single(preview.Base.Files);
            var proposed = Assert.Single(preview.Proposed.Files);
            Assert.Equal(file.FileTypeId, before.FileTypeTagId);
            Assert.Equal("Report", before.FileTypeNameAtRevision);
            Assert.Equal((mapType.Id, "Map", "Survey Map", ".pdf"),
                (proposed.FileTypeTagId, proposed.FileTypeNameAtRevision, proposed.Name, proposed.Extension));
            Assert.Contains(file.FileId, preview.Diff.ChangedFiles);
            requestId = await service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default);
        }

        string approvedRevisionId;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            var reviewed = Assert.Single(detail.Proposed.Files);
            Assert.Equal((mapType.Id, "Map", "Survey Map", ".pdf"),
                (reviewed.FileTypeTagId, reviewed.FileTypeNameAtRevision, reviewed.Name, reviewed.Extension));
            approvedRevisionId = (await reviewer.ChangeRequests.ApproveAsync(requestId,
                detail.Request.CurrentProposalVersionId, null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files);
        Assert.Equal((mapType.Id, "Map", "Survey Map", ".pdf"),
            (acceptedFile.FileTypeTagId, acceptedFile.FileTypeNameAtRevision, acceptedFile.Name, acceptedFile.Extension));
    }

    [Fact]
    public async Task StagedFileSelectedMetadataIsPersistedAndMatchesApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileSelectedMetadataIsPersistedAndMatchesApproval));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "stgtype00a");
        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", System.Text.Encoding.UTF8.GetBytes("typed staged bytes"));
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await using (var seed = database.CreateDbContext("staged-file-type-seed", tenant.AccountId))
        {
            (await seed.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            (await seed.Files.SingleAsync(row => row.Id == file.FileId)).Extension = ".pdf";
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
                Proposal(tenant, "Temporary request state"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Survey", false, default);
            var stagedVersionId = await CurrentVersionAsync(contributor, requestId);

            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var cave = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId, FileTypeTagId = mapType.Id, Name = "Survey Map"
            }];
            versionId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(requestId,
                values, againstCurrent: false, expectedBaseRevisionId: tenant.RevisionId,
                expectedProposalVersionId: stagedVersionId, default);
        }

        await using (var inspect = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(inspect, tenant.AccountId);
            var stored = await inspect.CaveProposalVersions.SingleAsync(version => version.Id == versionId);
            var intent = Assert.Single(CaveProposalJson.Deserialize(stored.ProposalJson, stored.SchemaVersion).Files,
                candidate => candidate.FileId == file.FileId);
            Assert.Equal((mapType.Id, "Map", "Survey Map", ".pdf"),
                (intent.FileTypeTagId, intent.FileTypeName, intent.Name, intent.Extension));
            var historical = await IntegrationTestServices.For(inspect).CaveChangeRequests.GetVersionAsync(
                requestId, versionId, default);
            var presented = Assert.Single(historical.Proposed.Files);
            Assert.Equal((mapType.Id, "Map", "Survey Map", ".pdf"),
                (presented.FileTypeTagId, presented.FileTypeNameAtRevision, presented.Name, presented.Extension));
            Assert.Empty(historical.DiffFromBase.Scalars);
            Assert.Equal([file.FileId], historical.DiffFromBase.AddedFiles);
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approvedRevisionId = (await IntegrationTestServices.For(reviewer, blobs).CaveChangeRequests.ApproveAsync(
                requestId, versionId, null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files);
        Assert.Equal((mapType.Id, "Map", "Survey Map", ".pdf"),
            (acceptedFile.FileTypeTagId, acceptedFile.FileTypeNameAtRevision, acceptedFile.Name, acceptedFile.Extension));
    }

    [Fact]
    public async Task HistoricalStagedFilePresentationDoesNotDriftWhileLiveFileExists()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalStagedFilePresentationDoesNotDriftWhileLiveFileExists));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "drift0000a");
        var mapType = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.File, "Map", "filemap00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string versionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            (await contributor.TagTypes.SingleAsync(tag => tag.Id == file.FileTypeId)).Name = "Report";
            (await contributor.Files.SingleAsync(row => row.Id == file.FileId)).Extension = ".pdf";
            await contributor.SaveChangesAsync();

            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Temporary request state"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Version One", false, default);
            var stagedVersionId = await CurrentVersionAsync(contributor, requestId);

            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var cave = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                FileTypeTagId = file.FileTypeId,
                Name = "Version One"
            }];
            versionId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(
                requestId, values, againstCurrent: false, tenant.RevisionId, stagedVersionId, default);
        }

        await using (var mutate = database.CreateDbContext("live-file-mutation", tenant.AccountId))
        {
            var live = await mutate.Files.SingleAsync(row => row.Id == file.FileId);
            live.FileTypeTagId = mapType.Id;
            live.Extension = ".png";
            live.Name = "Today";
            await mutate.SaveChangesAsync();
        }

        await using var audit = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(audit, tenant.AccountId);
        var historical = await IntegrationTestServices.For(audit).CaveChangeRequests.GetVersionAsync(
            requestId, versionId, default);
        var presented = Assert.Single(historical.Proposed.Files);
        Assert.Equal((file.FileTypeId, "Report", "Version One", ".pdf"),
            (presented.FileTypeTagId, presented.FileTypeNameAtRevision, presented.Name, presented.Extension));
        Assert.Empty(historical.DiffFromBase.Scalars);
        Assert.Equal([file.FileId], historical.DiffFromBase.AddedFiles);
        Assert.Empty(historical.UnavailableStagedFileIds);
    }

    [Fact]
    public async Task PublishedFileProposalRejectsANameThatNewlyIncludesItsFixedExtension()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PublishedFileProposalRejectsANameThatNewlyIncludesItsFixedExtension));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, associateWithCave: true,
            fileId: "badname00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using (var seed = database.CreateDbContext("proposal-name-validation-baseline", tenant.AccountId))
        {
            var mutation = await new CaveMutationRepository(seed, seed.RequestUser,
                    new CavePublishedSnapshotRepository(seed, seed.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, _ => { });
            tenant = tenant with { RevisionId = mutation.RevisionId! };
        }

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.Files = [new EditFileMetadataVm
        {
            Id = file.FileId,
            Name = "seed-a.pdf",
            FileTypeTagId = file.FileTypeId
        }];

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.PreviewAsync(tenant.CaveId, values, context.ExpectedBaseRevisionId, default));
        Assert.Equal(400, failure.StatusCode);
        Assert.Contains("must not include its fixed extension", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HistoricalStagedFileProposalMetadataRemainsImmutableAcrossVersions()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalStagedFileProposalMetadataRemainsImmutableAcrossVersions));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "stagedmeta");
        const string mapTypeId = "maptype00a";
        const string liveTypeId = "livetype0a";
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Map", mapTypeId);
        await ReferenceTestData.AddTagAsync(database, tenant.AccountId, TagTypeKeyConstant.File, "Live type", liveTypeId);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        string requestId;
        string versionOneId;
        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            (await contributor.TagTypes.SingleAsync(tag => tag.Id == stagedFile.FileTypeId)).Name = "Report";
            var file = await contributor.Files.SingleAsync(candidate => candidate.Id == stagedFile.FileId);
            file.Extension = ".pdf";
            file.Name = "Survey";
            await contributor.SaveChangesAsync();

            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Temporary request state"), default);
            await requests.StageFileAsync(requestId, file.Id, file.FileTypeTagId, file.Name,
                reviewer: false, default);
            var stagedVersionId = await CurrentVersionAsync(contributor, requestId);

            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var cave = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tenant.CaveId);
            var versionOneValues = ValuesFromCave(cave!);
            versionOneValues.Files = [new EditFileMetadataVm
            {
                Id = stagedFile.FileId,
                FileTypeTagId = stagedFile.FileTypeId,
                Name = "Survey"
            }];
            versionOneId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(
                requestId, versionOneValues, againstCurrent: false, tenant.RevisionId, stagedVersionId, default);

            var versionTwoValues = ValuesFromCave(cave!);
            versionTwoValues.Files = [new EditFileMetadataVm
            {
                Id = stagedFile.FileId,
                FileTypeTagId = mapTypeId,
                Name = "Reviewed Survey"
            }];
            versionTwoId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(
                requestId, versionTwoValues, againstCurrent: false, tenant.RevisionId, versionOneId, default);
        }

        await using (var inspect = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(inspect, tenant.AccountId);
            var service = IntegrationTestServices.For(inspect).CaveChangeRequests;
            var activeRow = await inspect.CaveProposalVersions.SingleAsync(version => version.Id == versionTwoId);
            var activeIntent = Assert.Single(CaveProposalJson.Deserialize(activeRow.ProposalJson,
                activeRow.SchemaVersion).Files, intent => intent.FileId == stagedFile.FileId);
            Assert.Equal((mapTypeId, "Map", "Reviewed Survey", ".pdf"),
                (activeIntent.FileTypeTagId, activeIntent.FileTypeName, activeIntent.Name, activeIntent.Extension));
            var activePreview = await service.GetVersionAsync(requestId, versionTwoId, default);
            var activeFile = Assert.Single(activePreview.Proposed.Files, file => file.Id == stagedFile.FileId);
            Assert.Equal((mapTypeId, "Map", "Reviewed Survey", ".pdf"),
                (activeFile.FileTypeTagId, activeFile.FileTypeNameAtRevision, activeFile.Name, activeFile.Extension));
            Assert.Empty(activePreview.DiffFromBase.Scalars);
            Assert.Equal([stagedFile.FileId], activePreview.DiffFromBase.AddedFiles);

            var liveFile = await inspect.Files.SingleAsync(file => file.Id == stagedFile.FileId);
            liveFile.FileTypeTagId = liveTypeId;
            liveFile.Extension = ".txt";
            liveFile.Name = "Today's mutable metadata";
            await inspect.SaveChangesAsync();

            var historical = await service.GetVersionAsync(requestId, versionOneId, default);
            var historicalFile = Assert.Single(historical.Proposed.Files,
                file => file.Id == stagedFile.FileId);
            Assert.Equal((stagedFile.FileTypeId, "Report", "Survey", ".pdf"),
                (historicalFile.FileTypeTagId, historicalFile.FileTypeNameAtRevision,
                    historicalFile.Name, historicalFile.Extension));
            Assert.Empty(historical.DiffFromBase.Scalars);
            Assert.Equal([stagedFile.FileId], historical.DiffFromBase.AddedFiles);
            Assert.Empty(historical.UnavailableStagedFileIds);
        }
    }

    [Fact]
    public async Task ApprovalPublishesMetadataFromTheActiveStagedFileProposalVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalPublishesMetadataFromTheActiveStagedFileProposalVersion));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, null, null, null);
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
            file.Extension = ".pdf";
            file.Name = "Survey";
            await contributor.SaveChangesAsync();

            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Temporary request state"), default);
            await requests.StageFileAsync(requestId, file.Id, file.FileTypeTagId, file.Name,
                reviewer: false, default);
            var stagedVersionId = await CurrentVersionAsync(contributor, requestId);

            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var cave = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.Files = [new EditFileMetadataVm
            {
                Id = stagedFile.FileId,
                FileTypeTagId = mapTypeId,
                Name = "Reviewed Survey"
            }];
            activeVersionId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(
                requestId, values, againstCurrent: false, tenant.RevisionId, stagedVersionId, default);
        }

        await using (var mutate = database.CreateDbContext("live-file-mutation", tenant.AccountId))
        {
            var live = await mutate.Files.SingleAsync(file => file.Id == stagedFile.FileId);
            live.Name = "Drifted live name";
            live.Extension = ".png";
            await mutate.SaveChangesAsync();
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            var selected = await IntegrationTestServices.For(reviewer).CaveChangeRequests.GetVersionAsync(
                requestId, activeVersionId, default);
            Assert.Empty(selected.DiffFromBase.Scalars);
            Assert.Equal([stagedFile.FileId], selected.DiffFromBase.AddedFiles);
            approvedRevisionId = (await IntegrationTestServices.For(reviewer, blobs).CaveChangeRequests.ApproveAsync(
                requestId, activeVersionId, null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        var acceptedFile = Assert.Single(accepted.Files, file => file.Id == stagedFile.FileId);
        Assert.Equal((mapTypeId, "Map", "Reviewed Survey", ".pdf"),
            (acceptedFile.FileTypeTagId, acceptedFile.FileTypeNameAtRevision,
                acceptedFile.Name, acceptedFile.Extension));
        var liveFile = await verify.Files.SingleAsync(file => file.Id == stagedFile.FileId);
        Assert.Equal(("Reviewed Survey", ".pdf", mapTypeId),
            (liveFile.Name, liveFile.Extension, liveFile.FileTypeTagId));
    }

}
