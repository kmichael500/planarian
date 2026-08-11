using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Tags.Repositories;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveChangeRequestWorkflowIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ApplicationApprovalPublishesNormalizedCaveAndExactlyOneLinkedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApplicationApprovalPublishesNormalizedCaveAndExactlyOneLinkedRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            requestId = await new CaveChangeRequestRepository(contributor, contributor.RequestUser).CreateAsync(
                tenant.CaveId, tenant.RevisionId, PublishableProposal(tenant, locationTag.Id, "Approved Cave"),
                default);
        }

        CaveChangeRequestDecisionVm result;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            result = await CreateChangeRequestService(reviewer).ApproveAsync(requestId, "Looks correct", default);
        }

        Assert.Equal(CaveChangeRequestDecisionResult.Approved, result.Result);
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(result.PublishedRevisionId, request.ApprovedRevisionId);
        Assert.Equal("reviewer", request.ReviewerUserId);
        Assert.Equal("Looks correct", request.ReviewerNotes);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == result.PublishedRevisionId);
        Assert.Equal(CaveRevisionSource.UserSubmission, revision.Source);
        Assert.Equal(requestId, revision.ChangeRequestId);
        var published = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
        Assert.Equal("Approved Cave", published.Name);
        Assert.Single(published.Entrances);
        var normalized = await new CavePublishedSnapshotRepository(verify, verify.RequestUser)
            .BuildAsync(tenant.CaveId);
        Assert.Equal(CaveSnapshotJson.Serialize(normalized), CaveSnapshotJson.Serialize(published));
        Assert.Equal("Approved Cave", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == tenant.CaveId)).Name);
    }

    [Fact]
    public async Task StaleProposalCanBeExplicitlyRevisedAgainstCurrentAndApproved()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StaleProposalCanBeExplicitlyRevisedAgainstCurrentAndApproved));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionOneId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Contributor proposal v1"), default);
            versionOneId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
        }

        string revisionB;
        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            var published = await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cave => cave.Narrative = "Unrelated manager change");
            revisionB = published.RevisionId!;
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            var conflict = await CreateChangeRequestService(reviewer).ApproveAsync(requestId, null, default);
            Assert.Equal(CaveChangeRequestDecisionResult.Conflict, conflict.Result);
            Assert.Equal(revisionB, conflict.CurrentRevisionId);
        }

        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await AuthenticateAsync(contributor, tenant.AccountId);
            versionTwoId = await CreateChangeRequestService(contributor).AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "Contributor proposal v2", "Unrelated manager change"),
                againstCurrent: true, default);
            Assert.False((await CreateChangeRequestService(contributor).GetAsync(requestId, default)).Request.IsStale);
        }

        await using (var beforeApproval = database.CreateDbContext("verify", tenant.AccountId))
        {
            var versions = await beforeApproval.CaveProposalVersions.Where(row => row.ChangeRequestId == requestId)
                .OrderBy(row => row.CreatedOn).ToListAsync();
            Assert.Equal([versionOneId, versionTwoId], versions.Select(version => version.Id));
            Assert.Equal(tenant.RevisionId, versions[0].BaseRevisionId);
            Assert.Equal(revisionB, versions[1].BaseRevisionId);
            Assert.Equal(versionOneId, versions[1].PreviousProposalVersionId);
            Assert.Equal(versionTwoId, (await beforeApproval.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId);
        }

        CaveChangeRequestDecisionVm approved;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            approved = await CreateChangeRequestService(reviewer).ApproveAsync(requestId, "Re-reviewed", default);
        }

        Assert.Equal(CaveChangeRequestDecisionResult.Approved, approved.Result);
        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(3, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        var revisionC = await verify.CaveRevisions.SingleAsync(row => row.Id == approved.PublishedRevisionId);
        Assert.Equal(revisionB, revisionC.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.UserSubmission, revisionC.Source);
        Assert.Equal(requestId, revisionC.ChangeRequestId);
        Assert.Equal(revisionC.Id, (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
            .ApprovedRevisionId);
    }

    [Fact]
    public async Task ApplicationApprovalPublishesStagedFileInsideSingleRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApplicationApprovalPublishesStagedFileInsideSingleRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, tenant);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Proposal with staged file"), default);
            var staged = await contributor.Files.SingleAsync(row => row.Id == file.FileId);
            staged.ExpiresOn = DateTime.UtcNow.AddDays(-1);
            await contributor.SaveChangesAsync();
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Survey attachment",
                reviewer: false, default);
            await AuthenticateAsync(contributor, tenant.AccountId);
            await CreateChangeRequestService(contributor).AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "Proposal with staged file"),
                againstCurrent: false, default);
        }

        await using (var pending = database.CreateDbContext("verify", tenant.AccountId))
        {
            Assert.Null((await pending.Files.SingleAsync(row => row.Id == file.FileId)).CaveId);
            Assert.True(await pending.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == file.FileId));
            var currentVersionId = (await pending.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
            var proposal = CaveProposalJson.Deserialize((await pending.CaveProposalVersions.SingleAsync(row =>
                row.Id == currentVersionId)).ProposalJson, 1);
            Assert.Contains(proposal.Files, candidate => candidate.FileId == file.FileId &&
                candidate.Disposition == ProposalFileDisposition.PublishStaged);
            var original = CaveSnapshotJson.Deserialize((await pending.CaveRevisions.SingleAsync(row =>
                row.Id == tenant.RevisionId)).SnapshotJson, 1);
            Assert.DoesNotContain(original.Files, candidate => candidate.Id == file.FileId);
        }

        CaveChangeRequestDecisionVm result;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            result = await CreateChangeRequestService(reviewer).ApproveAsync(requestId, null, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var publishedFile = await verify.Files.SingleAsync(row => row.Id == file.FileId);
        Assert.Equal(tenant.CaveId, publishedFile.CaveId);
        Assert.Null(publishedFile.ExpiresOn);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == file.FileId));
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == result.PublishedRevisionId);
        Assert.Contains(CaveSnapshotJson.Deserialize(revision.SnapshotJson, 1).Files,
            candidate => candidate.Id == file.FileId);
    }

    [Fact]
    public async Task PendingStagedFileIsExcludedFromExpirationUntilRequestIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PendingStagedFileIsExcludedFromExpirationUntilRequestIsRejected));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        var stagedFile = await TestDataBuilder.AddFileAsync(database, tenant, fileId: "staged000a");
        const string unrelatedFileId = "expired00a";
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Pending files"), default);
            contributor.Files.Add(new Planarian.Model.Database.Entities.RidgeWalker.File
            {
                Id = unrelatedFileId,
                AccountId = tenant.AccountId,
                FileTypeTagId = stagedFile.FileTypeId,
                FileName = "expired.pdf",
                BlobKey = "expired",
                BlobContainer = "test"
            });
            await contributor.SaveChangesAsync();
            var files = await contributor.Files.Where(row => row.Id == stagedFile.FileId ||
                row.Id == unrelatedFileId).ToListAsync();
            foreach (var file in files) file.ExpiresOn = DateTime.UtcNow.AddDays(-1);
            await contributor.SaveChangesAsync();
            await requests.StageFileAsync(requestId, stagedFile.FileId, stagedFile.FileTypeId, "Staged",
                reviewer: false, default);

            var expired = (await new FileRepository(contributor, contributor.RequestUser).GetExpiredFiles()).ToList();
            Assert.DoesNotContain(expired, file => file.Id == stagedFile.FileId);
            Assert.Contains(expired, file => file.Id == unrelatedFileId);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
            await new CaveChangeRequestRepository(reviewer, reviewer.RequestUser)
                .RejectAsync(requestId, "Rejected", default);

        await using var afterRejection = database.CreateDbContext("verify", tenant.AccountId);
        var nowExpired = (await new FileRepository(afterRejection, afterRejection.RequestUser)
            .GetExpiredFiles()).ToList();
        Assert.Contains(nowExpired, file => file.Id == stagedFile.FileId);
    }

    [Fact]
    public async Task ApprovalFailureBeforeCommitRollsBackCaveRequestRevisionAndStagedFile()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalFailureBeforeCommitRollsBackCaveRequestRevisionAndStagedFile));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await TestDataBuilder.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await TestDataBuilder.AddFileAsync(database, tenant);
        await GrantViewAsync(database, tenant, "contributor");
        await GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Must roll back"), default);
            await requests.StageFileAsync(requestId, file.FileId, file.FileTypeId, "Rollback attachment",
                reviewer: false, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await AuthenticateAsync(reviewer, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Must roll back");
            values.Files = [new EditFileMetadataVm
            {
                Id = file.FileId,
                FileTypeTagId = file.FileTypeId,
                DisplayName = "Rollback attachment"
            }];
            await Assert.ThrowsAsync<InvalidOperationException>(() => CreateCaveService(reviewer)
                .ApproveChangeRequestAsync(values, tenant.RevisionId, requestId, [file.FileId],
                    (_, _) => throw new InvalidOperationException("Simulated approval failure"), default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal("Cave A", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(row => row.Id == tenant.CaveId)).Name);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId)).Status);
        Assert.Equal(1, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        Assert.Null((await verify.Files.SingleAsync(row => row.Id == file.FileId)).CaveId);
        Assert.True(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == file.FileId));
    }

    [Fact]
    public async Task ProposalVersionsAreImmutableAndApprovalPublishesExactlyOneLinkedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionsAreImmutableAndApprovalPublishesExactlyOneLinkedRevision));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string firstVersionId;
        string secondVersionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "First proposal"), default);
            firstVersionId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
            secondVersionId = await repository.AddVersionAsync(requestId, tenant.RevisionId,
                Proposal(tenant, "Accepted proposal"),
                reviewer: false, default);
        }

        string publishedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await using var transaction = await reviewer.Database.BeginTransactionAsync();
            var mutations = new CaveMutationRepository(reviewer, reviewer.RequestUser,
                new CavePublishedSnapshotRepository(reviewer, reviewer.RequestUser));
            var requests = new CaveChangeRequestRepository(reviewer, reviewer.RequestUser);
            var preparation = await mutations.PrepareExistingAsync(tenant.CaveId, tenant.RevisionId);
            (await reviewer.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId)).Name = "Accepted proposal";
            await reviewer.SaveChangesAsync();
            var result = await mutations.PublishPreparedAsync(preparation, CaveRevisionSource.UserSubmission,
                CaveRevisionOperation.Update, requestId);
            await requests.MarkApprovedAsync(requestId, result, "Reviewed", default);
            publishedRevisionId = result.RevisionId!;
            await transaction.CommitAsync();
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        var versions = await verify.CaveProposalVersions.Where(row => row.ChangeRequestId == requestId)
            .OrderBy(row => row.CreatedOn).ToListAsync();
        Assert.Equal([firstVersionId, secondVersionId], versions.Select(row => row.Id));
        Assert.Null(versions[0].PreviousProposalVersionId);
        Assert.Equal(firstVersionId, versions[1].PreviousProposalVersionId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(publishedRevisionId, request.ApprovedRevisionId);
        Assert.Equal("Reviewed", request.ReviewerNotes);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == publishedRevisionId);
        Assert.Equal(requestId, revision.ChangeRequestId);
        Assert.Equal(CaveRevisionSource.UserSubmission, revision.Source);
    }

    [Fact]
    public async Task RejectionDoesNotPublishAndStaleBaseCannotBePreparedForApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RejectionDoesNotPublishAndStaleBaseCannotBePreparedForApproval));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        string rejectedId;
        string staleId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            rejectedId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Rejected"), default);
            staleId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Stale"), default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await new CaveChangeRequestRepository(reviewer, reviewer.RequestUser)
                .RejectAsync(rejectedId, "Not enough evidence", default);
        }

        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Name = "Newer state");
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await using var transaction = await reviewer.Database.BeginTransactionAsync();
            var mutations = new CaveMutationRepository(reviewer, reviewer.RequestUser,
                new CavePublishedSnapshotRepository(reviewer, reviewer.RequestUser));
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                mutations.PrepareExistingAsync(tenant.CaveId, tenant.RevisionId));
            await transaction.RollbackAsync();
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(row => row.CaveId == tenant.CaveId));
        Assert.Equal(CaveChangeRequestStatus.Rejected,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == rejectedId)).Status);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(row => row.Id == staleId)).Status);
    }

    [Fact]
    public async Task StagedFileIsVersionedAndOnlyAssociatedWithCaveDuringPublication()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StagedFileIsVersionedAndOnlyAssociatedWithCaveDuringPublication));
        var tenant = await TestDataBuilder.CreatePublishedCaveAsync(database, 'a');
        await GrantViewAsync(database, tenant, "contributor");
        var stagedFile = await TestDataBuilder.AddFileAsync(database, tenant);
        string requestId;
        var fileId = stagedFile.FileId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Proposal with file"), default);
            var file = await contributor.Files.SingleAsync(row => row.Id == fileId);
            file.DisplayName = "Survey";
            file.ExpiresOn = DateTime.UtcNow.AddDays(10);
            await contributor.SaveChangesAsync();
            await repository.StageFileAsync(requestId, fileId, stagedFile.FileTypeId, file.DisplayName,
                reviewer: false, default);
        }

        await using (var beforePublication = database.CreateDbContext("verify", tenant.AccountId))
        {
            var staged = await beforePublication.CaveChangeRequestStagedFiles.SingleAsync(row =>
                row.ChangeRequestId == requestId && row.FileId == fileId);
            Assert.Equal(tenant.AccountId, staged.AccountId);
            Assert.Null((await beforePublication.Files.SingleAsync(row => row.Id == fileId)).CaveId);
            var versions = await beforePublication.CaveProposalVersions
                .Where(row => row.ChangeRequestId == requestId).OrderBy(row => row.CreatedOn).ToListAsync();
            Assert.Equal(2, versions.Count);
            var proposal = CaveProposalJson.Deserialize(versions[^1].ProposalJson, versions[^1].SchemaVersion);
            Assert.Contains(proposal.Files, intent => intent.FileId == fileId &&
                intent.Disposition == ProposalFileDisposition.PublishStaged);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            var caves = new Planarian.Modules.Caves.Repositories.CaveRepository(reviewer, reviewer.RequestUser);
            var attached = await caves.AttachStagedFilesAsync(requestId, tenant.CaveId, [fileId]);
            await reviewer.SaveChangesAsync();
            Assert.Single(attached);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var published = await verify.Files.SingleAsync(row => row.Id == fileId);
        Assert.Equal(tenant.CaveId, published.CaveId);
        Assert.Null(published.ExpiresOn);
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(row => row.FileId == fileId));
    }

    private static CaveProposalSnapshotV1 Proposal(PublishedCaveTestData cave, string name) => new()
    {
        AccountId = cave.AccountId,
        CaveId = cave.CaveId,
        Name = name,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumberIntent = CountyNumberIntent.Manual,
        RequestedCountyNumber = cave.CountyNumber
    };

    private static CaveProposalSnapshotV1 PublishableProposal(PublishedCaveTestData cave,
        string locationQualityTagId, string name, string? narrative = null) => new()
    {
        AccountId = cave.AccountId,
        CaveId = cave.CaveId,
        Name = name,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumberIntent = CountyNumberIntent.Manual,
        RequestedCountyNumber = cave.CountyNumber,
        Narrative = narrative,
        Entrances = [new CaveProposalEntranceV1
        {
            EntranceId = string.Empty,
            Name = "Main Entrance",
            IsPrimary = true,
            Latitude = 35,
            Longitude = -86,
            Elevation = 500,
            LocationQualityTagId = locationQualityTagId
        }]
    };

    private static AddCaveVm PublishableValues(PublishedCaveTestData cave, string locationQualityTagId,
        string name, string? narrative = null) => new()
    {
        Id = cave.CaveId,
        Name = name,
        AlternateNames = [],
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        CountyNumber = cave.CountyNumber,
        IsCountyNumberManuallySet = true,
        Narrative = narrative,
        Entrances = [new AddEntranceVm
        {
            Name = "Main Entrance",
            IsPrimary = true,
            Latitude = 35,
            Longitude = -86,
            ElevationFeet = 500,
            LocationQualityTagId = locationQualityTagId
        }]
    };

    private static CaveChangeRequestService CreateChangeRequestService(Planarian.Model.Database.PlanarianDbContext db)
    {
        var user = db.RequestUser;
        var caves = new CaveRepository(db, user);
        return new CaveChangeRequestService(new CaveChangeRequestRepository(db, user), caves, CreateCaveService(db),
            new CaveRevisionQueryRepository(db, user), null!, user);
    }

    private static CaveService CreateCaveService(Planarian.Model.Database.PlanarianDbContext db)
    {
        var user = db.RequestUser;
        var snapshots = new CavePublishedSnapshotRepository(db, user);
        var coordinator = new CaveMutationCoordinator(new CaveMutationRepository(db, user, snapshots));
        return new CaveService(new CaveRepository(db, user), user, null!, new TagRepository(db, user), null!, null!,
            coordinator);
    }

    private static async Task GrantViewAsync(PostgresTestDatabase database, PublishedCaveTestData cave, string userId)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
        await EnsureAccountUserAsync(db, cave.AccountId);
        const string permissionId = "vIeWPz9a00";
        if (!await db.Permissions.AnyAsync(row => row.Id == permissionId))
        {
            db.Permissions.Add(new Permission
            {
                Id = permissionId, Key = "View", Name = "View", Description = "View Caves",
                PermissionType = "Cave"
            });
            await db.SaveChangesAsync();
        }
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }

    private static async Task GrantManagerAsync(PostgresTestDatabase database, PublishedCaveTestData cave,
        string userId)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
        await EnsureAccountUserAsync(db, cave.AccountId);
        const string permissionId = "mAnageR000";
        if (!await db.Permissions.AnyAsync(row => row.Id == permissionId))
        {
            db.Permissions.Add(new Permission
            {
                Id = permissionId, Key = "Manager", Name = "Manager", Description = "Manage Caves",
                PermissionType = "Cave"
            });
            await db.SaveChangesAsync();
        }
        db.CavePermissions.Add(new CavePermission
        {
            UserId = db.RequestUser.Id,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            PermissionId = permissionId
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureAccountUserAsync(Planarian.Model.Database.PlanarianDbContext db,
        string accountId)
    {
        if (await db.AccountUsers.AnyAsync(row => row.AccountId == accountId && row.UserId == db.RequestUser.Id))
            return;
        db.AccountUsers.Add(new AccountUser
        {
            AccountId = accountId,
            UserId = db.RequestUser.Id,
            InvitationAcceptedOn = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static Task AuthenticateAsync(Planarian.Model.Database.PlanarianDbContext db, string accountId) =>
        db.RequestUser.Initialize(accountId, db.RequestUser.Id);
}
