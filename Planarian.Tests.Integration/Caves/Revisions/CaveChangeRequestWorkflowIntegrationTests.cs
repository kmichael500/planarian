using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests;

public sealed class CaveChangeRequestWorkflowIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
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
            secondVersionId = await repository.AddVersionAsync(requestId, Proposal(tenant, "Accepted proposal"),
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
            await repository.StageFileAsync(requestId, fileId, stagedFile.FileTypeId, file.DisplayName, default);
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

    private static async Task GrantViewAsync(PostgresTestDatabase database, PublishedCaveTestData cave, string userId)
    {
        await using var db = database.CreateDbContext(userId, cave.AccountId);
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
}
