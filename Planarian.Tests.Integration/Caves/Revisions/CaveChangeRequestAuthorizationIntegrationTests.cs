using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Import.Planning;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestAuthorizationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ProposalOwnerLosesAllRequestAndHistoryAccessWhenCaveViewIsRevoked()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalOwnerLosesAllRequestAndHistoryAccessWhenCaveViewIsRevoked));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        string requestId;
        string versionId;
        await using (var owner = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            requestId = await owner.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Visible proposal"), tenant.RevisionId, default);
            await new CaveChangeRequestRepository(owner.Db, owner.Db.RequestUser).StageFileAsync(
                requestId, file.FileId, file.FileTypeId, "Evidence", false, default);
            versionId = await CurrentVersionAsync(owner.Db, requestId);
        }

        await CavePermissions.RevokeAllAsync(database, tenant, "contributor");
        await using (var revoked = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var service = revoked.ChangeRequests;
            Assert.Empty(await service.ListMineAsync(default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => service.GetAsync(requestId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.GetVersionAsync(requestId, versionId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => service.PreviewVersionAsync(
                requestId, PublishableValues(tenant, quality.Id, "Revision"), false,
                tenant.RevisionId, versionId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => service.AddVersionAsync(
                requestId, PublishableValues(tenant, quality.Id, "Revision"), false,
                tenant.RevisionId, versionId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => revoked.Services.StageRequestFileForTestAsync(
                requestId, new MemoryStream([1, 2, 3]), "blocked.txt", null, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.OpenStagedFileAsync(requestId, file.FileId, default));
            var revisions = new CaveRevisionService(new CaveRepository(revoked.Db, revoked.Db.RequestUser),
                new CaveRevisionQueryRepository(revoked.Db, revoked.Db.RequestUser));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                revisions.ListAsync(tenant.CaveId, default));
        }

        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await using var restored = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        Assert.Equal(requestId, (await restored.ChangeRequests.GetAsync(requestId, default)).Request.Id);
        Assert.Contains((await restored.ChangeRequests.ListMineAsync(default)), row => row.Id == requestId);
    }

    [Fact]
    public async Task SameAccountGuessedCaveWithoutViewPermissionCannotReceiveProposal()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(SameAccountGuessedCaveWithoutViewPermissionCannotReceiveProposal));
        var visible = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var hiddenCave = await CaveTestDataFactory.AddCaveAsync(database, visible, "hidden000a", "Hidden Cave", 2);
        var hidden = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, hiddenCave, "revisionha");
        var locationTag = await ReferenceTestData.AddTagAsync(database, visible.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, visible, "contributor");

        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, visible.AccountId, "contributor"))
        {
            var values = PublishableValues(hidden, locationTag.Id, "Guessed hidden Cave");

            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                contributor.ChangeRequests.CreateAsync(hidden.CaveId, values,
                    hidden.RevisionId, default));
            Assert.Contains("Cave", failure.Message);
        }

        await using var verify = database.CreateDbContext("verify", visible.AccountId);
        Assert.False(await verify.CaveChangeRequests.AnyAsync(row => row.CaveId == hidden.CaveId));
        Assert.False(await verify.CaveProposalVersions.AnyAsync(row => row.CaveId == hidden.CaveId));
        Assert.False(await verify.CaveChangeRequestStagedFiles.AnyAsync(staged =>
            verify.CaveChangeRequests.Any(request =>
                request.Id == staged.ChangeRequestId && request.CaveId == hidden.CaveId)));
    }

    [Fact]
    public async Task UnauthorizedAndCrossTenantIdentifiersCannotAccessOrDecideRequests()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(UnauthorizedAndCrossTenantIdentifiersCannotAccessOrDecideRequests));
        var tenantA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var tenantB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var locationTagA = await ReferenceTestData.AddTagAsync(database, tenantA.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade A", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenantA, "contributor");
        await CavePermissions.GrantViewAsync(database, tenantA, "viewer");

        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, tenantA.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenantA.CaveId,
                PublishableValues(tenantA, locationTagA.Id, "Protected proposal"), tenantA.RevisionId, default);
            versionId = await CurrentVersionAsync(contributor.Db, requestId);
        }

        await using (var viewer = await CaveTestActor.CreateAsync(database, tenantA.AccountId, "viewer"))
        {
            var service = viewer.ChangeRequests;
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.ApproveAsync(requestId, versionId, null, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.RejectAsync(requestId, versionId, null, default));
        }

        await using (var accountB = await CaveTestActor.CreateAsync(database, tenantB.AccountId, "user-b"))
        {
            var service = accountB.ChangeRequests;
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => service.GetAsync(requestId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.GetVersionAsync(requestId, versionId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.AddVersionAsync(requestId, new AddCaveVm { Id = tenantA.CaveId }, false,
                    tenantA.RevisionId, versionId, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.ApproveAsync(requestId, versionId, null, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.RejectAsync(requestId, versionId, null, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.OpenStagedFileAsync(requestId, "guessedfile", default));
        }

        await using var verify = database.CreateDbContext("verify", tenantA.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Pending, request.Status);
        Assert.Null(request.ReviewerUserId);
        Assert.Single(await verify.CaveRevisions.Where(row => row.CaveId == tenantA.CaveId).ToListAsync());
    }

    [Fact]
    public async Task GuessedInvisibleCaveAndMismatchedStagedFileAreNotAccessible()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(GuessedInvisibleCaveAndMismatchedStagedFileAreNotAccessible));
        var visible = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var invisible = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var locationTag = await ReferenceTestData.AddTagAsync(database, visible.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, visible);
        await CavePermissions.GrantViewAsync(database, visible, "contributor");

        await using var contributor = await CaveTestActor.CreateAsync(database, visible.AccountId, "contributor");
        var service = contributor.ChangeRequests;
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => service.CreateAsync(
            invisible.CaveId, PublishableValues(invisible, locationTag.Id, "Guessed cave"),
            invisible.RevisionId, default));

        var requestA = await service.CreateAsync(visible.CaveId,
            PublishableValues(visible, locationTag.Id, "Request A"), visible.RevisionId, default);
        var requestB = await service.CreateAsync(visible.CaveId,
            PublishableValues(visible, locationTag.Id, "Request B"), visible.RevisionId, default);
        await new CaveChangeRequestRepository(contributor.Db, contributor.Db.RequestUser).StageFileAsync(
            requestB, file.FileId, file.FileTypeId, "Request B file", false, default);

        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            service.OpenStagedFileAsync(requestA, file.FileId, default));
        contributor.Db.ChangeTracker.Clear();
        Assert.Equal(2, await contributor.Db.CaveChangeRequests.CountAsync());
        Assert.Equal(3, await contributor.Db.CaveProposalVersions.CountAsync());

        await using (var crossAccount = await CaveTestActor.CreateAsync(
                         database, invisible.AccountId, "cross-account"))
        {
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                crossAccount.ChangeRequests.OpenStagedFileAsync(requestB, file.FileId, default));
        }

        await using var verifyInvisible = database.CreateDbContext("verify", invisible.AccountId);
        Assert.Empty(await verifyInvisible.CaveChangeRequests.ToListAsync());
        Assert.Empty(await verifyInvisible.CaveProposalVersions.ToListAsync());
    }
}
