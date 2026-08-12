using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
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
    public async Task SameAccountGuessedCaveWithoutViewPermissionCannotReceiveProposal()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(SameAccountGuessedCaveWithoutViewPermissionCannotReceiveProposal));
        var visible = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var hiddenCave = await CaveTestDataFactory.AddCaveAsync(database, visible, "hidden000a", "Hidden Cave", 2);
        var hidden = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, hiddenCave, "revisionha");
        await CavePermissions.GrantViewAsync(database, visible, "contributor");

        await using (var contributor = database.CreateDbContext("contributor", visible.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, visible.AccountId);
            var values = new AddCaveVm
            {
                Id = hidden.CaveId,
                Name = "Guessed hidden Cave",
                AlternateNames = [],
                StateId = hidden.StateId,
                CountyId = hidden.CountyId,
                CountyNumber = hidden.CountyNumber,
                IsCountyNumberManuallySet = true,
                Entrances = []
            };

            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(hidden.CaveId, values,
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
        await using (var contributor = database.CreateDbContext("contributor", tenantA.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenantA.AccountId);
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(tenantA.CaveId,
                PublishableValues(tenantA, locationTagA.Id, "Protected proposal"), tenantA.RevisionId, default);
            versionId = await CurrentVersionAsync(contributor, requestId);
        }

        await using (var viewer = database.CreateDbContext("viewer", tenantA.AccountId))
        {
            await CavePermissions.AuthenticateAsync(viewer, tenantA.AccountId);
            var service = IntegrationTestServices.For(viewer).CaveChangeRequests;
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.ApproveAsync(requestId, versionId, null, default));
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.RejectAsync(requestId, versionId, null, default));
        }

        await using (var accountB = database.CreateDbContext("user-b", tenantB.AccountId))
        {
            await CavePermissions.EnsureAccountUserAsync(accountB, tenantB.AccountId);
            await CavePermissions.AuthenticateAsync(accountB, tenantB.AccountId);
            var service = IntegrationTestServices.For(accountB).CaveChangeRequests;
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

        await using var contributor = database.CreateDbContext("contributor", visible.AccountId);
        await CavePermissions.AuthenticateAsync(contributor, visible.AccountId);
        var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => service.CreateAsync(
            invisible.CaveId, PublishableValues(invisible, locationTag.Id, "Guessed cave"),
            invisible.RevisionId, default));

        var requestA = await service.CreateAsync(visible.CaveId,
            PublishableValues(visible, locationTag.Id, "Request A"), visible.RevisionId, default);
        var requestB = await service.CreateAsync(visible.CaveId,
            PublishableValues(visible, locationTag.Id, "Request B"), visible.RevisionId, default);
        await new CaveChangeRequestRepository(contributor, contributor.RequestUser).StageFileAsync(
            requestB, file.FileId, file.FileTypeId, "Request B file", false, default);

        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            service.OpenStagedFileAsync(requestA, file.FileId, default));
        contributor.ChangeTracker.Clear();
        Assert.Equal(2, await contributor.CaveChangeRequests.CountAsync());
        Assert.Equal(3, await contributor.CaveProposalVersions.CountAsync());

        await using (var crossAccount = database.CreateDbContext("cross-account", invisible.AccountId))
        {
            await CavePermissions.EnsureAccountUserAsync(crossAccount, invisible.AccountId);
            await CavePermissions.AuthenticateAsync(crossAccount, invisible.AccountId);
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                IntegrationTestServices.For(crossAccount).CaveChangeRequests.OpenStagedFileAsync(requestB, file.FileId, default));
        }

        await using var verifyInvisible = database.CreateDbContext("verify", invisible.AccountId);
        Assert.Empty(await verifyInvisible.CaveChangeRequests.ToListAsync());
        Assert.Empty(await verifyInvisible.CaveProposalVersions.ToListAsync());
    }
}
