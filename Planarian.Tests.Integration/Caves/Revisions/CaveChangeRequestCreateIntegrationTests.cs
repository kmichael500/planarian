using System.Text;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Caves.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Users.Repositories;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Concurrency;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestCreateIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ViewContributorCanSubmitHiddenCountyCaveAndApprovalPublishesCorrectNumberAndViewGrant()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ViewContributorCanSubmitHiddenCountyCaveAndApprovalPublishesCorrectNumberAndViewGrant));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'n');
        await AccountTestDataFactory.AddAccountStateAsync(database, new AccountCountyTestData(
            source.AccountId, source.StateId, source.StateName, source.StateAbbreviation,
            source.CountyId, source.CountyName, source.CountyDisplayId));

        const string destinationCountyId = "county001n";
        await using (var seed = database.CreateDbContext("destination-seed", source.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = destinationCountyId,
                AccountId = source.AccountId,
                StateId = source.StateId,
                DisplayId = "NN02",
                Name = "Hidden County"
            });
            await seed.SaveChangesAsync();
            Assert.True(await seed.Counties.IgnoreQueryFilters().AnyAsync(county =>
                county.AccountId == source.AccountId && county.Id == destinationCountyId));
        }

        await CaveTestDataFactory.AddCaveAsync(database,
            new AccountCountyTestData(source.AccountId, source.StateId, source.StateName,
                source.StateAbbreviation, destinationCountyId, "Hidden County", "NN02"),
            "hidden001n", "Hidden One", 1);
        await CaveTestDataFactory.AddCaveAsync(database,
            new AccountCountyTestData(source.AccountId, source.StateId, source.StateName,
                source.StateAbbreviation, destinationCountyId, "Hidden County", "NN02"),
            "hidden055n", "Hidden Fifty Five", 55);

        var locationQuality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01n");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source.AccountId, destinationCountyId, "reviewer");

        string requestId;
        string reservedCaveId;
        string proposalVersionId;
        string contributorUserId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            contributorUserId = contributor.Db.RequestUser.Id;
            Assert.Empty(await contributor.Db.Caves.Where(cave => cave.CountyId == destinationCountyId).ToListAsync());

            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, destinationCountyId, locationQuality.Id), default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);

            Assert.True(detail.Request.IsCreation);
            Assert.False(detail.Request.CaveExists);
            Assert.Null(detail.Request.OriginalBaseRevisionId);
            Assert.Null(detail.Request.ProposalBaseRevisionId);
            Assert.Equal("Proposed Hidden Cave", detail.Request.CaveName);
            Assert.Equal(56, detail.LikelyCountyNumber);

            reservedCaveId = detail.Request.CaveId;
            proposalVersionId = detail.Request.CurrentProposalVersionId;
            Assert.False(await contributor.Db.Caves.IgnoreQueryFilters()
                .AnyAsync(cave => cave.Id == reservedCaveId));
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "reviewer"))
        {
            var queue = await reviewer.ChangeRequests.ListForReviewAsync(default);
            Assert.Contains(queue, request => request.Id == requestId && request.IsCreation);

            var decision = await reviewer.ChangeRequests.ApproveAsync(
                requestId, proposalVersionId, "Verified", default);
            Assert.Equal(CaveChangeRequestDecisionResult.Approved, decision.Result);
            Assert.NotNull(decision.PublishedRevisionId);
        }

        await using (var verify = database.CreateDbContext("verify", source.AccountId))
        {
            var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == reservedCaveId);
            Assert.Equal(destinationCountyId, cave.CountyId);
            Assert.Equal(56, cave.CountyNumber);
            Assert.NotNull(cave.CurrentRevisionId);

            var revision = await verify.CaveRevisions.SingleAsync(r => r.Id == cave.CurrentRevisionId);
            Assert.Equal(CaveRevisionSource.UserSubmission, revision.Source);
            Assert.Equal(CaveRevisionOperation.Create, revision.Operation);
            Assert.Equal(requestId, revision.ChangeRequestId);

            var request = await verify.CaveChangeRequests.SingleAsync(r => r.Id == requestId);
            Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
            Assert.Equal(revision.Id, request.ApprovedRevisionId);

            var viewPermissionId = await verify.Permissions
                .Where(permission => permission.Key == PermissionKey.View &&
                                     permission.PermissionType == PermissionType.Cave)
                .Select(permission => permission.Id)
                .SingleAsync();
            Assert.True(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(permission =>
                permission.AccountId == source.AccountId &&
                permission.UserId == contributorUserId &&
                permission.CaveId == reservedCaveId &&
                permission.PermissionId == viewPermissionId));
        }

        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            Assert.True(await contributor.Db.RequestUser.HasCavePermission(
                PermissionKey.View, reservedCaveId, null, null, false));
            Assert.NotNull(await contributor.Services.Caves.GetCave(reservedCaveId));
            Assert.Equal(requestId, (await contributor.ChangeRequests.GetAsync(requestId, default)).Request.Id);
        }

        await using (var revoke = database.CreateDbContext("permission-admin", source.AccountId))
        {
            var viewPermissionId = await revoke.Permissions
                .Where(permission => permission.Key == PermissionKey.View &&
                                     permission.PermissionType == PermissionType.Cave)
                .Select(permission => permission.Id)
                .SingleAsync();
            await revoke.CavePermissions.IgnoreQueryFilters().Where(permission =>
                permission.AccountId == source.AccountId &&
                permission.UserId == contributorUserId &&
                permission.CaveId == reservedCaveId &&
                permission.PermissionId == viewPermissionId).ExecuteDeleteAsync();
        }

        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            Assert.False(await contributor.Db.RequestUser.HasCavePermission(
                PermissionKey.View, reservedCaveId, null, null, false));
            Assert.DoesNotContain(await contributor.ChangeRequests.ListMineAsync(default),
                request => request.Id == requestId);
            await Assert.ThrowsAsync<ApiException>(() =>
                contributor.ChangeRequests.GetAsync(requestId, default));
        }
    }

    [Fact]
    public async Task ApprovedCreationHistoryIsStoredButHiddenAfterLaterHardDelete()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovedCreationHistoryIsStoredButHiddenAfterLaterHardDelete));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'h');
        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01h");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        string requestId;
        string proposalVersionId;
        string caveId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, quality.Id), default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            caveId = detail.Request.CaveId;
            proposalVersionId = detail.Request.CurrentProposalVersionId;
        }

        string approvedRevisionId;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer"))
        {
            approvedRevisionId = (await reviewer.ChangeRequests.ApproveAsync(
                requestId, proposalVersionId, null, default)).PublishedRevisionId!;
            await reviewer.Services.Caves.DeleteCave(caveId, default);
        }

        await using (var verify = database.CreateDbContext("verify", source.AccountId))
        {
            Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == caveId));
            var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
            Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
            Assert.Equal(approvedRevisionId, request.ApprovedRevisionId);
            Assert.True(await verify.CaveRevisions.AnyAsync(revision =>
                revision.Id == approvedRevisionId && revision.ChangeRequestId == requestId));
        }

        await using var contributorHistory = await CaveTestActor.CreateAsync(
            database, source.AccountId, "contributor");
        Assert.DoesNotContain(await contributorHistory.ChangeRequests.ListMineAsync(default),
            request => request.Id == requestId);
        await Assert.ThrowsAsync<ApiException>(() =>
            contributorHistory.ChangeRequests.GetAsync(requestId, default));
        await Assert.ThrowsAsync<ApiException>(() =>
            contributorHistory.ChangeRequests.GetVersionAsync(requestId, proposalVersionId, default));
    }

    [Fact]
    public async Task CountyManagerCanReviewFirstProposedCaveInEmptyCounty()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyManagerCanReviewFirstProposedCaveInEmptyCounty));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'e');
        const string emptyCountyId = "county001e";
        await using (var seed = database.CreateDbContext("empty-county-seed", source.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = emptyCountyId,
                AccountId = source.AccountId,
                StateId = source.StateId,
                DisplayId = "EE02",
                Name = "Empty County"
            });
            await seed.SaveChangesAsync();
            Assert.True(await seed.Counties.IgnoreQueryFilters().AnyAsync(county =>
                county.AccountId == source.AccountId && county.Id == emptyCountyId));
        }

        var locationQuality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01e");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source.AccountId, emptyCountyId, "reviewer");

        string requestId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, emptyCountyId, locationQuality.Id), default);

        await using var reviewer = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer");
        Assert.True(await reviewer.Db.RequestUser.HasCavePermission(
            PermissionKey.Manager, null, emptyCountyId, source.StateId, false));
        Assert.Contains(await reviewer.ChangeRequests.ListForReviewAsync(default),
            request => request.Id == requestId);
    }

    [Fact]
    public async Task DestinationManagerImmediatelyLosesCreationReviewAndDecisionAccessWhenRevoked()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DestinationManagerImmediatelyLosesCreationReviewAndDecisionAccessWhenRevoked));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'd');
        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01d");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, quality.Id), default);
            versionId = (await contributor.ChangeRequests.GetAsync(requestId, default))
                .Request.CurrentProposalVersionId;
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer"))
            Assert.Contains(await reviewer.ChangeRequests.ListForReviewAsync(default),
                request => request.Id == requestId);

        await CavePermissions.RevokeCountyManagerAsync(
            database, source.AccountId, source.CountyId, "reviewer");

        await using var revoked = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer");
        Assert.DoesNotContain(await revoked.ChangeRequests.ListForReviewAsync(default),
            request => request.Id == requestId);
        await Assert.ThrowsAsync<ApiException>(() =>
            revoked.ChangeRequests.GetAsync(requestId, default));
        await Assert.ThrowsAsync<ApiException>(() =>
            revoked.ChangeRequests.ApproveAsync(requestId, versionId, null, default));
        await Assert.ThrowsAsync<ApiException>(() =>
            revoked.ChangeRequests.RejectAsync(requestId, versionId, "No longer authorized.", default));
    }

    [Fact]
    public async Task ManualCountyNumberMustBePositiveForDirectAndProposalCreate()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ManualCountyNumberMustBePositiveForDirectAndProposalCreate));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'v');
        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01v");
        await CavePermissions.GrantCountyManagerAsync(database, source, "manager");

        await using var manager = await CaveTestActor.CreateAsync(database, source.AccountId, "manager");
        var invalid = NewCaveValues(source.StateId, source.CountyId, quality.Id);
        invalid.IsCountyNumberManuallySet = true;
        invalid.CountyNumber = 0;

        var direct = await Assert.ThrowsAsync<ApiException>(() =>
            manager.Services.Caves.AddCave(invalid, default));
        Assert.Equal(400, direct.StatusCode);

        var preview = await Assert.ThrowsAsync<ApiException>(() =>
            manager.ChangeRequests.PreviewNewAsync(invalid, default));
        Assert.Equal(400, preview.StatusCode);

        var create = await Assert.ThrowsAsync<ApiException>(() =>
            manager.ChangeRequests.CreateNewAsync(invalid, default));
        Assert.Equal(400, create.StatusCode);
    }

    [Fact]
    public async Task UserWithoutViewCapabilityCannotSubmitNewCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(UserWithoutViewCapabilityCannotSubmitNewCave));
        var tenant = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'v');
        var locationQuality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01v");

        await using var user = await CaveTestActor.CreateAsync(database, tenant.AccountId, "member");
        await Assert.ThrowsAsync<ApiException>(() => user.ChangeRequests.CreateNewAsync(
            NewCaveValues(tenant.StateId, tenant.CountyId, locationQuality.Id), default));

        Assert.Empty(await user.Db.CaveChangeRequests.ToListAsync());
        Assert.Empty(await user.Db.CaveProposalVersions.ToListAsync());
    }

    [Fact]
    public async Task NonDestinationManagerCanSubmitButCannotChooseCountyNumberOptionsOrReview()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NonDestinationManagerCanSubmitButCannotChooseCountyNumberOptionsOrReview));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'm');
        const string destinationCountyId = "county001m";
        await using (var seed = database.CreateDbContext("destination-seed", source.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = destinationCountyId,
                AccountId = source.AccountId,
                StateId = source.StateId,
                DisplayId = "MM02",
                Name = "Other County"
            });
            await seed.SaveChangesAsync();
            Assert.True(await seed.Counties.IgnoreQueryFilters().AnyAsync(county =>
                county.AccountId == source.AccountId && county.Id == destinationCountyId));
        }

        var locationQuality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01m");
        await CavePermissions.GrantManagerAsync(database, source, "manager");

        await using var actor = await CaveTestActor.CreateAsync(database, source.AccountId, "manager");
        var automatic = NewCaveValues(source.StateId, destinationCountyId, locationQuality.Id);
        var requestId = await actor.ChangeRequests.CreateNewAsync(automatic, default);
        Assert.NotNull(requestId);
        Assert.DoesNotContain(await actor.ChangeRequests.ListForReviewAsync(default),
            request => request.Id == requestId);

        var manual = NewCaveValues(source.StateId, destinationCountyId, locationQuality.Id);
        manual.IsCountyNumberManuallySet = true;
        manual.CountyNumber = 9;
        await Assert.ThrowsAsync<ApiException>(() => actor.ChangeRequests.PreviewNewAsync(manual, default));
        await Assert.ThrowsAsync<ApiException>(() => actor.ChangeRequests.CreateNewAsync(manual, default));

        var firstAvailable = NewCaveValues(source.StateId, destinationCountyId, locationQuality.Id);
        firstAvailable.UseFirstAvailableCountyNumber = true;
        await Assert.ThrowsAsync<ApiException>(() => actor.ChangeRequests.PreviewNewAsync(firstAvailable, default));
        await Assert.ThrowsAsync<ApiException>(() =>
            actor.ChangeRequests.CreateNewAsync(firstAvailable, default));
    }

    [Fact]
    public async Task NewCaveProposalSupportsChangesRequestedRevisionAndResubmissionWithoutPublishedBase()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NewCaveProposalSupportsChangesRequestedRevisionAndResubmissionWithoutPublishedBase));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'r');
        var locationQuality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01r");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        string requestId;
        string caveId;
        string firstVersionId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, locationQuality.Id), default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            caveId = detail.Request.CaveId;
            firstVersionId = detail.Request.CurrentProposalVersionId;
            Assert.Null(detail.Request.ProposalBaseRevisionId);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer"))
        {
            var result = await reviewer.ChangeRequests.RequestChangesAsync(
                requestId, firstVersionId, "Clarify the name.", default);
            Assert.Equal(CaveChangeRequestDecisionResult.ChangesRequested, result.Result);
        }

        string revisedVersionId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            var revised = NewCaveValues(source.StateId, source.CountyId, locationQuality.Id);
            revised.Id = caveId;
            revised.Name = "Revised Proposed Cave";
            revisedVersionId = await contributor.ChangeRequests.AddVersionAsync(
                requestId, revised, againstCurrent: false, expectedBaseRevisionId: null,
                expectedProposalVersionId: firstVersionId, default);
            var result = await contributor.ChangeRequests.ResubmitAsync(requestId, revisedVersionId, default);
            Assert.Equal(CaveChangeRequestDecisionResult.Resubmitted, result.Result);
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer"))
        {
            var decision = await reviewer.ChangeRequests.ApproveAsync(
                requestId, revisedVersionId, null, default);
            Assert.Equal(CaveChangeRequestDecisionResult.Approved, decision.Result);
        }

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == caveId);
        Assert.Equal("Revised Proposed Cave", cave.Name);
        var versions = await verify.CaveProposalVersions
            .Where(version => version.ChangeRequestId == requestId)
            .OrderBy(version => version.CreatedOn)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.All(versions, version => Assert.Null(version.BaseRevisionId));
        Assert.Equal(firstVersionId, versions[1].PreviousProposalVersionId);
    }

    [Fact]
    public async Task RejectingNewCaveProposalCreatesNeitherCaveRevisionNorViewGrant()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RejectingNewCaveProposalCreatesNeitherCaveRevisionNorViewGrant));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'j');
        var locationQuality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01j");
        var fileType = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.File, "Survey", "filetype0j");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        var blobs = new TestFileBlobStore();
        string requestId;
        string caveId;
        string proposalVersionId;
        string contributorUserId;
        string stagedFileId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor", blobs))
        {
            contributorUserId = contributor.Db.RequestUser.Id;
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("rejected creation file"));
            var staged = await contributor.Services.Files.StageAuthoringFile(content, "Survey reject.pdf", default);
            stagedFileId = staged.Id;
            var values = NewCaveValues(source.StateId, source.CountyId, locationQuality.Id);
            values.CartographerNameTagIds = ["Rejected Proposal Person"];
            values.Files =
            [
                new EditFileMetadataVm
                {
                    Id = stagedFileId,
                    FileTypeTagId = fileType.Id,
                    Name = "Rejected survey"
                }
            ];
            requestId = await contributor.ChangeRequests.CreateNewAsync(values, default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            caveId = detail.Request.CaveId;
            proposalVersionId = detail.Request.CurrentProposalVersionId;
        }
        Assert.NotEmpty(blobs.Keys);

        await using (var reviewer = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "reviewer", blobs))
        {
            var result = await reviewer.ChangeRequests.RejectAsync(
                requestId, proposalVersionId, "Duplicate report.", default);
            Assert.Equal(CaveChangeRequestDecisionResult.Rejected, result.Result);
        }

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == caveId));
        Assert.False(await verify.CaveRevisions.AnyAsync(revision =>
            revision.CaveId == caveId || revision.ChangeRequestId == requestId));
        Assert.False(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(permission =>
            permission.AccountId == source.AccountId &&
            permission.UserId == contributorUserId &&
            permission.CaveId == caveId));
        Assert.False(await verify.CaveChangeRequestStagedFiles.IgnoreQueryFilters().AnyAsync(link =>
            link.AccountId == source.AccountId &&
            link.ChangeRequestId == requestId &&
            link.FileId == stagedFileId));
        Assert.False(await verify.Files.IgnoreQueryFilters().AnyAsync(file =>
            file.AccountId == source.AccountId && file.Id == stagedFileId));
        Assert.False(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(tag =>
            tag.AccountId == source.AccountId &&
            tag.Key == TagTypeKeyConstant.People &&
            tag.Name == "Rejected Proposal Person"));
        Assert.Empty(blobs.Keys);
        Assert.Equal(CaveChangeRequestStatus.Rejected,
            (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);

        await using var contributorAfter = await CaveTestActor.CreateAsync(
            database, source.AccountId, "contributor");
        Assert.Equal(requestId,
            (await contributorAfter.ChangeRequests.GetAsync(requestId, default)).Request.Id);
    }

    [Fact]
    public async Task ReviewerCannotMoveNewCaveProposalOutsideTheirManagedDestination()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ReviewerCannotMoveNewCaveProposalOutsideTheirManagedDestination));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'q');
        const string otherCountyId = "county002q";
        await using (var seed = database.CreateDbContext("other-county-seed", source.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = otherCountyId,
                AccountId = source.AccountId,
                StateId = source.StateId,
                DisplayId = "QQ02",
                Name = "Other County"
            });
            await seed.SaveChangesAsync();
        }

        var locationQuality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01q");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        string requestId;
        string caveId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, locationQuality.Id), default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            caveId = detail.Request.CaveId;
            versionId = detail.Request.CurrentProposalVersionId;
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer"))
        {
            var moved = NewCaveValues(source.StateId, otherCountyId, locationQuality.Id);
            moved.Id = caveId;
            await Assert.ThrowsAsync<ApiException>(() => reviewer.ChangeRequests.PreviewVersionAsync(
                requestId, moved, againstCurrent: false, expectedBaseRevisionId: null,
                expectedProposalVersionId: versionId, default));
            await Assert.ThrowsAsync<ApiException>(() => reviewer.ChangeRequests.AddVersionAsync(
                requestId, moved, againstCurrent: false, expectedBaseRevisionId: null,
                expectedProposalVersionId: versionId, default));
        }

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(source.CountyId, request.ProposedCountyId);
        Assert.Equal(versionId, request.CurrentProposalVersionId);
        Assert.Single(await verify.CaveProposalVersions.Where(version =>
            version.ChangeRequestId == requestId).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentApprovalsOfSameNewCaveSerializeBeforeReservedCaveInsert()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentApprovalsOfSameNewCaveSerializeBeforeReservedCaveInsert));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'r');
        var locationQuality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01r");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        string requestId;
        string versionId;
        string caveId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, locationQuality.Id), default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            versionId = detail.Request.CurrentProposalVersionId;
            caveId = detail.Request.CaveId;
        }

        await using var blocker = database.CreateDbContext("county-blocker", source.AccountId);
        await using var blockerTransaction = await blocker.Database.BeginTransactionAsync();
        await new CountyReferenceLockRepository(blocker, blocker.RequestUser)
            .LockForMutationAsync([source.CountyId]);

        await using var reviewerOne = database.CreateDbContext("reviewer", source.AccountId);
        await using var reviewerTwo = database.CreateDbContext("reviewer", source.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerOne, source.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerTwo, source.AccountId);
        await reviewerOne.Database.OpenConnectionAsync();
        await reviewerTwo.Database.OpenConnectionAsync();
        var pidOne = await PostgresLockAssertions.BackendPidAsync(reviewerOne);
        var pidTwo = await PostgresLockAssertions.BackendPidAsync(reviewerTwo);

        var approveOne = IntegrationTestServices.For(reviewerOne).CaveChangeRequests
            .ApproveAsync(requestId, versionId, null, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, source.AccountId, pidOne, approveOne);

        var approveTwo = IntegrationTestServices.For(reviewerTwo).CaveChangeRequests
            .ApproveAsync(requestId, versionId, null, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, source.AccountId, pidTwo, approveTwo);

        await blockerTransaction.CommitAsync();
        var winner = await approveOne;
        var loser = await Assert.ThrowsAsync<ApiException>(() => approveTwo);
        Assert.Equal(400, loser.StatusCode);
        Assert.Equal("This request has already been reviewed.", loser.Message);

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        Assert.Single(await verify.Caves.IgnoreQueryFilters().Where(cave => cave.Id == caveId).ToListAsync());
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == caveId).ToListAsync());
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(winner.PublishedRevisionId, request.ApprovedRevisionId);
    }


    [Fact]
    public async Task NewCaveProposalApprovalMatchesDirectCreateForCompleteAuthoringContract()
    {
        await using var directDatabase = await fixture.CreateDatabaseAsync(
            nameof(NewCaveProposalApprovalMatchesDirectCreateForCompleteAuthoringContract) + "_direct");
        await using var proposalDatabase = await fixture.CreateDatabaseAsync(
            nameof(NewCaveProposalApprovalMatchesDirectCreateForCompleteAuthoringContract) + "_proposal");
        var directSource = await CaveTestDataFactory.CreatePublishedCaveAsync(directDatabase, 'k');
        var proposalSource = await CaveTestDataFactory.CreatePublishedCaveAsync(proposalDatabase, 'k');
        var directReferences = await CaveAuthoringFieldPersistenceIntegrationTests.CreateReferenceCatalogAsync(
            directDatabase, directSource);
        var proposalReferences = await CaveAuthoringFieldPersistenceIntegrationTests.CreateReferenceCatalogAsync(
            proposalDatabase, proposalSource);
        await CavePermissions.GrantCountyManagerAsync(
            directDatabase, directSource.AccountId, directReferences.TargetCountyId, "manager");
        await CavePermissions.GrantViewAsync(proposalDatabase, proposalSource, "contributor");
        await CavePermissions.GrantCountyManagerAsync(
            proposalDatabase, proposalSource.AccountId, proposalReferences.TargetCountyId, "reviewer");

        var directBlobs = new TestFileBlobStore();
        var proposalBlobs = new TestFileBlobStore();
        string directCaveId;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directSource.AccountId, "manager", directBlobs))
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("creation parity file bytes"));
            var staged = await manager.Services.Files.StageAuthoringFile(content, "source.pdf", default);
            var values = CaveAuthoringFieldPersistenceIntegrationTests.FullValues(
                directSource, directReferences, staged.Id);
            PrepareAutomaticCreate(values);
            AddSharedNewPerson(values);
            directCaveId = await manager.Services.Caves.AddCave(values, default);
        }

        string proposalCaveId;
        string requestId;
        string proposalVersionId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         proposalDatabase, proposalSource.AccountId, "contributor", proposalBlobs))
        {
            await using var content = new MemoryStream(Encoding.UTF8.GetBytes("creation parity file bytes"));
            var staged = await contributor.Services.Files.StageAuthoringFile(content, "source.pdf", default);
            var values = CaveAuthoringFieldPersistenceIntegrationTests.FullValues(
                proposalSource, proposalReferences, staged.Id);
            PrepareAutomaticCreate(values);
            AddSharedNewPerson(values);
            requestId = await contributor.ChangeRequests.CreateNewAsync(values, default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            proposalCaveId = detail.Request.CaveId;
            proposalVersionId = detail.Request.CurrentProposalVersionId;
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(
                         proposalDatabase, proposalSource.AccountId, "reviewer", proposalBlobs))
            await reviewer.ChangeRequests.ApproveAsync(requestId, proposalVersionId, "Kitchen sink", default);

        CavePublishedSnapshotV1 directSnapshot;
        CavePublishedSnapshotV1 proposalSnapshot;
        await using (var manager = await CaveTestActor.CreateAsync(
                         directDatabase, directSource.AccountId, "manager", directBlobs))
            directSnapshot = await new CavePublishedSnapshotRepository(manager.Db, manager.Db.RequestUser)
                .BuildAsync(directCaveId);
        await using (var reviewer = await CaveTestActor.CreateAsync(
                         proposalDatabase, proposalSource.AccountId, "reviewer", proposalBlobs))
            proposalSnapshot = await new CavePublishedSnapshotRepository(reviewer.Db, reviewer.Db.RequestUser)
                .BuildAsync(proposalCaveId);

        Assert.Equal(1, directSnapshot.CountyNumber);
        Assert.Equal(1, proposalSnapshot.CountyNumber);
        AssertCreationReferenceIntegrity(directSnapshot, directReferences);
        AssertCreationReferenceIntegrity(proposalSnapshot, proposalReferences);
        var canonicalDirect = CanonicalizeCreationSnapshot(directSnapshot);
        var canonicalProposal = CanonicalizeCreationSnapshot(proposalSnapshot);
        Assert.Equal(CaveSnapshotJson.Serialize(canonicalDirect), CaveSnapshotJson.Serialize(canonicalProposal));
        Assert.True(new CaveRevisionDiffService().IsSemanticEqual(canonicalDirect, canonicalProposal));

        await using var directVerify = directDatabase.CreateDbContext("verify-direct", directSource.AccountId);
        await using var proposalVerify = proposalDatabase.CreateDbContext("verify-proposal", proposalSource.AccountId);
        var directRevision = Assert.Single(await directVerify.CaveRevisions
            .Where(row => row.CaveId == directCaveId).ToListAsync());
        var proposalRevision = Assert.Single(await proposalVerify.CaveRevisions
            .Where(row => row.CaveId == proposalCaveId).ToListAsync());
        Assert.Equal((CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Create),
            (directRevision.Source, directRevision.Operation));
        Assert.Equal((CaveRevisionSource.UserSubmission, CaveRevisionOperation.Create),
            (proposalRevision.Source, proposalRevision.Operation));
        Assert.Equal(requestId, proposalRevision.ChangeRequestId);
        Assert.Single(await directVerify.TagTypes.Where(tag =>
            tag.AccountId == directSource.AccountId &&
            tag.Key == TagTypeKeyConstant.People &&
            tag.Name == "Shared New Person").ToListAsync());
        Assert.Single(await proposalVerify.TagTypes.Where(tag =>
            tag.AccountId == proposalSource.AccountId &&
            tag.Key == TagTypeKeyConstant.People &&
            tag.Name == "Shared New Person").ToListAsync());
    }

    [Fact]
    public async Task SubmitterRerouteMovesReviewQueueAcrossStatesAndPublishesAtNewDestination()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(SubmitterRerouteMovesReviewQueueAcrossStatesAndPublishesAtNewDestination));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'u');
        const string stateBId = "state002u";
        const string countyBId = "county002u";
        await GlobalStateTestData.AddAsync(database, stateBId, "Second State", "SS");
        await using (var seed = database.CreateDbContext("reroute-seed", source.AccountId))
        {
            seed.AccountStates.Add(new AccountState
            {
                Id = "acst002u",
                AccountId = source.AccountId,
                StateId = stateBId
            });
            seed.Counties.Add(new County
            {
                Id = countyBId,
                AccountId = source.AccountId,
                StateId = stateBId,
                DisplayId = "SS02",
                Name = "Second County"
            });
            await seed.SaveChangesAsync();
        }

        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01u");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source.AccountId, source.CountyId, "reviewera");
        await CavePermissions.GrantCountyManagerAsync(database, source.AccountId, countyBId, "reviewerb");

        string requestId;
        string caveId;
        string versionOne;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, quality.Id), default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            caveId = detail.Request.CaveId;
            versionOne = detail.Request.CurrentProposalVersionId;
        }

        await using (var reviewerA = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewera"))
            Assert.Contains(await reviewerA.ChangeRequests.ListForReviewAsync(default),
                request => request.Id == requestId);
        await using (var reviewerB = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewerb"))
            Assert.DoesNotContain(await reviewerB.ChangeRequests.ListForReviewAsync(default),
                request => request.Id == requestId);

        string versionTwo;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            var moved = NewCaveValues(stateBId, countyBId, quality.Id);
            moved.Id = caveId;
            moved.Name = "Rerouted Cave";
            versionTwo = await contributor.ChangeRequests.AddVersionAsync(
                requestId, moved, againstCurrent: false, expectedBaseRevisionId: null,
                expectedProposalVersionId: versionOne, default);
        }

        await using (var reviewerA = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewera"))
        {
            Assert.DoesNotContain(await reviewerA.ChangeRequests.ListForReviewAsync(default),
                request => request.Id == requestId);
            await Assert.ThrowsAsync<ApiException>(() => reviewerA.ChangeRequests.GetAsync(requestId, default));
        }
        await using (var reviewerB = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewerb"))
        {
            Assert.Contains(await reviewerB.ChangeRequests.ListForReviewAsync(default),
                request => request.Id == requestId);
            await reviewerB.ChangeRequests.ApproveAsync(requestId, versionTwo, null, default);
        }

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == caveId);
        Assert.Equal((stateBId, countyBId, "Rerouted Cave"), (cave.StateId, cave.CountyId, cave.Name));
    }

    [Fact]
    public async Task ConcurrentIndependentCreationApprovalsAllocateDistinctCountyNumbers()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentIndependentCreationApprovalsAllocateDistinctCountyNumbers));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'c');
        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01c");
        await CavePermissions.GrantViewAsync(database, source, "contrib1");
        await CavePermissions.GrantViewAsync(database, source, "contrib2");
        await CavePermissions.GrantCountyManagerAsync(database, source.AccountId, source.CountyId, "reviewer");

        async Task<(string RequestId, string VersionId, string CaveId)> Submit(string actor, string name)
        {
            await using var contributor = await CaveTestActor.CreateAsync(database, source.AccountId, actor);
            var values = NewCaveValues(source.StateId, source.CountyId, quality.Id);
            values.Name = name;
            var requestId = await contributor.ChangeRequests.CreateNewAsync(values, default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            Assert.Equal(2, detail.LikelyCountyNumber);
            return (requestId, detail.Request.CurrentProposalVersionId, detail.Request.CaveId);
        }

        var first = await Submit("contrib1", "Concurrent Cave One");
        var second = await Submit("contrib2", "Concurrent Cave Two");

        await using var blocker = database.CreateDbContext("county-blocker", source.AccountId);
        await using var blockerTransaction = await blocker.Database.BeginTransactionAsync();
        await new CountyReferenceLockRepository(blocker, blocker.RequestUser)
            .LockForMutationAsync([source.CountyId]);

        await using var reviewerOne = database.CreateDbContext("reviewer", source.AccountId);
        await using var reviewerTwo = database.CreateDbContext("reviewer", source.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerOne, source.AccountId);
        await CavePermissions.AuthenticateAsync(reviewerTwo, source.AccountId);
        await reviewerOne.Database.OpenConnectionAsync();
        await reviewerTwo.Database.OpenConnectionAsync();
        var pidOne = await PostgresLockAssertions.BackendPidAsync(reviewerOne);
        var pidTwo = await PostgresLockAssertions.BackendPidAsync(reviewerTwo);

        var approveOne = IntegrationTestServices.For(reviewerOne).CaveChangeRequests
            .ApproveAsync(first.RequestId, first.VersionId, null, default);
        var approveTwo = IntegrationTestServices.For(reviewerTwo).CaveChangeRequests
            .ApproveAsync(second.RequestId, second.VersionId, null, default);
        await PostgresLockAssertions.AssertBlockedAsync(database, source.AccountId, pidOne, approveOne);
        await PostgresLockAssertions.AssertBlockedAsync(database, source.AccountId, pidTwo, approveTwo);

        await blockerTransaction.CommitAsync();
        await Task.WhenAll(approveOne, approveTwo);

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        var created = await verify.Caves.IgnoreQueryFilters()
            .Where(cave => cave.Id == first.CaveId || cave.Id == second.CaveId)
            .OrderBy(cave => cave.CountyNumber).ToListAsync();
        Assert.True(created.Select(cave => cave.CountyNumber).SequenceEqual([2, 3]));
        foreach (var cave in created)
        {
            var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == cave.CurrentRevisionId);
            var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
            Assert.Equal(cave.CountyNumber, snapshot.CountyNumber);
        }
    }

    [Fact]
    public async Task RevokedViewCannotReviseCreationRequestButApprovalRestoresViewForCurrentMember()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RevokedViewCannotReviseCreationRequestButApprovalRestoresViewForCurrentMember));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'x');
        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01x");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        string requestId;
        string versionId;
        string caveId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, source.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, quality.Id), default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            versionId = detail.Request.CurrentProposalVersionId;
            caveId = detail.Request.CaveId;
        }

        await CavePermissions.RevokeAllAsync(database, source, "contributor");
        await using (var revoked = await CaveTestActor.CreateAsync(database, source.AccountId, "contributor"))
        {
            Assert.Empty(await revoked.ChangeRequests.ListMineAsync(default));
            await Assert.ThrowsAsync<ApiException>(() => revoked.ChangeRequests.GetAsync(requestId, default));
            var revised = NewCaveValues(source.StateId, source.CountyId, quality.Id);
            revised.Id = caveId;
            await Assert.ThrowsAsync<ApiException>(() => revoked.ChangeRequests.AddVersionAsync(
                requestId, revised, false, null, versionId, default));
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer"))
            await reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default);

        await using var restored = await CaveTestActor.CreateAsync(database, source.AccountId, "contributor");
        Assert.True(await restored.Db.RequestUser.HasCavePermission(
            PermissionKey.View, caveId, null, null, false));
        Assert.NotNull(await restored.Services.Caves.GetCave(caveId));
        Assert.Equal(requestId, (await restored.ChangeRequests.GetAsync(requestId, default)).Request.Id);
    }

    [Fact]
    public async Task MalformedCrossFamilyPermissionsDoNotGrantCreationReviewAccess()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MalformedCrossFamilyPermissionsDoNotGrantCreationReviewAccess));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'w');
        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01w");
        await CavePermissions.GrantViewAsync(database, source, "contributor");

        string requestId;
        string proposalVersionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, source.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, quality.Id), default);
            proposalVersionId = (await contributor.ChangeRequests.GetAsync(requestId, default))
                .Request.CurrentProposalVersionId;
        }

        const string reviewerId = "badreview1";
        const string userManagerPermissionId = "badmgr002";
        const string caveAdminPermissionId = "badadm002";
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = userManagerPermissionId,
            Key = PermissionKey.Manager,
            Name = "Wrong Type Manager",
            Description = "Regression-only User permission with a Cave Manager key.",
            PermissionType = PermissionType.User
        });
        await GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = caveAdminPermissionId,
            Key = PermissionKey.Admin,
            Name = "Wrong Type Admin",
            Description = "Regression-only Cave permission with an Account Admin key.",
            PermissionType = PermissionType.Cave
        });

        await using (var seed = database.CreateDbContext(reviewerId, source.AccountId))
        {
            await CavePermissions.EnsureAccountUserAsync(seed, source.AccountId);
            seed.CavePermissions.Add(new CavePermission
            {
                UserId = seed.RequestUser.Id,
                AccountId = source.AccountId,
                PermissionId = userManagerPermissionId
            });
            seed.UserPermissions.Add(new UserPermission
            {
                UserId = seed.RequestUser.Id,
                AccountId = source.AccountId,
                PermissionId = caveAdminPermissionId
            });
            await seed.SaveChangesAsync();
        }

        await using var malformedReviewer =
            await CaveTestActor.CreateAsync(database, source.AccountId, reviewerId);

        Assert.DoesNotContain(await malformedReviewer.ChangeRequests.ListForReviewAsync(default),
            request => request.Id == requestId);
        await Assert.ThrowsAsync<ApiException>(() =>
            malformedReviewer.ChangeRequests.GetAsync(requestId, default));
        await Assert.ThrowsAsync<ApiException>(() =>
            malformedReviewer.ChangeRequests.RequestChangesAsync(
                requestId, proposalVersionId, "Should be denied.", default));
    }

    [Fact]
    public async Task ApprovalPublishesButDoesNotGrantViewAfterSubmitterLeavesAccount()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalPublishesButDoesNotGrantViewAfterSubmitterLeavesAccount));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'y');
        var quality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01y");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        string requestId;
        string versionId;
        string caveId;
        string contributorUserId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, source.AccountId, "contributor"))
        {
            contributorUserId = contributor.Db.RequestUser.Id;
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, quality.Id), default);
            var detail = await contributor.ChangeRequests.GetAsync(requestId, default);
            versionId = detail.Request.CurrentProposalVersionId;
            caveId = detail.Request.CaveId;
        }

        await using (var revoke = database.CreateDbContext("permission-admin", source.AccountId))
        {
            await using var revokeTransaction = await revoke.Database.BeginTransactionAsync();
            Assert.True(await new UserRepository(revoke, revoke.RequestUser)
                .DeleteAccountUserAccess(contributorUserId, source.AccountId));
            await revokeTransaction.CommitAsync();
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, source.AccountId, "reviewer"))
            await reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default);

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        Assert.True(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == caveId));
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == caveId).ToListAsync());
        Assert.False(await verify.AccountUsers.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == source.AccountId && row.UserId == contributorUserId));
        Assert.False(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(permission =>
            permission.AccountId == source.AccountId &&
            permission.UserId == contributorUserId &&
            permission.CaveId == caveId));
    }

    [Fact]
    public async Task ConcurrentAccountRemovalAfterCreationGrantLeavesNoMembershipOrPermissions()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentAccountRemovalAfterCreationGrantLeavesNoMembershipOrPermissions));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'z');
        var sibling = await CaveTestDataFactory.AddCaveAsync(
            database, source, "cave00001z", "Granted Cave", 2);
        await CavePermissions.GrantViewAsync(database, source, "contributor");

        string contributorUserId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
            contributorUserId = contributor.Db.RequestUser.Id;

        await using var grantDb = database.CreateDbContext("grant-workflow", source.AccountId);
        await grantDb.Database.OpenConnectionAsync();
        await using var grantTransaction = await grantDb.Database.BeginTransactionAsync();
        var grantRepository = new CaveChangeRequestRepository(grantDb, grantDb.RequestUser);
        await grantRepository.GrantSubmitterCaveViewAsync(
            contributorUserId, sibling.CaveId, default);

        await using var revokeDb = database.CreateDbContext("permission-admin", source.AccountId);
        await revokeDb.Database.OpenConnectionAsync();
        await using var revokeTransaction = await revokeDb.Database.BeginTransactionAsync();
        var revokePid = await PostgresLockAssertions.BackendPidAsync(revokeDb);
        var revoke = new UserRepository(revokeDb, revokeDb.RequestUser)
            .DeleteAccountUserAccess(contributorUserId, source.AccountId);

        await PostgresLockAssertions.AssertBlockedAsync(
            database, source.AccountId, revokePid, revoke);

        await grantTransaction.CommitAsync();
        Assert.True(await revoke);
        await revokeTransaction.CommitAsync();

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        Assert.False(await verify.AccountUsers.IgnoreQueryFilters().AnyAsync(row =>
            row.AccountId == source.AccountId && row.UserId == contributorUserId));
        Assert.False(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(permission =>
            permission.AccountId == source.AccountId && permission.UserId == contributorUserId));
        Assert.False(await verify.UserPermissions.IgnoreQueryFilters().AnyAsync(permission =>
            permission.AccountId == source.AccountId && permission.UserId == contributorUserId));
    }

    [Fact]
    public async Task NewCaveApprovalTransactionRollsBackCaveRevisionAndViewGrantOnLateFailure()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NewCaveApprovalTransactionRollsBackCaveRevisionAndViewGrantOnLateFailure));
        var source = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 't');
        var locationQuality = await ReferenceTestData.AddTagAsync(database, source.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual01t");
        await CavePermissions.GrantViewAsync(database, source, "contributor");
        await CavePermissions.GrantCountyManagerAsync(database, source, "reviewer");

        string requestId;
        string caveId;
        string contributorUserId;
        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "contributor"))
        {
            contributorUserId = contributor.Db.RequestUser.Id;
            requestId = await contributor.ChangeRequests.CreateNewAsync(
                NewCaveValues(source.StateId, source.CountyId, locationQuality.Id), default);
            caveId = (await contributor.ChangeRequests.GetAsync(requestId, default)).Request.CaveId;
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(
                         database, source.AccountId, "reviewer"))
        {
            var request = await reviewer.Db.CaveChangeRequests.AsNoTracking()
                .SingleAsync(row => row.Id == requestId);
            var version = await reviewer.Db.CaveProposalVersions.AsNoTracking()
                .SingleAsync(row => row.Id == request.CurrentProposalVersionId);
            var proposal = CaveProposalJson.Deserialize(version.ProposalJson, version.SchemaVersion);
            var values = NewCaveValues(source.StateId, source.CountyId, locationQuality.Id);
            values.Id = caveId;
            values.Entrances.Single().Id = proposal.Entrances.Single().EntranceId;

            var requestRepository = new CaveChangeRequestRepository(reviewer.Db, reviewer.Db.RequestUser);
            var mutationCoordinator = new CaveMutationCoordinator(new CaveMutationRepository(
                reviewer.Db, reviewer.Db.RequestUser,
                new CavePublishedSnapshotRepository(reviewer.Db, reviewer.Db.RequestUser)));
            var peopleContext = new CavePeoplePublicationContext(
                new CavePeoplePublicationScope([], []),
                proposal.Entrances.ToDictionary(
                    entrance => entrance.EntranceId,
                    _ => new CavePeoplePublicationScope([], []),
                    StringComparer.Ordinal));

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                mutationCoordinator.ExecuteOwnedTransactionAsync(async token =>
                {
                    await reviewer.Services.Caves.ApproveNewCaveChangeRequestAsync(
                        values, caveId, requestId, [],
                        proposal.Entrances.Select(entrance => entrance.EntranceId)
                            .ToHashSet(StringComparer.Ordinal),
                        proposal.LinePlots.Select(linePlot => linePlot.Id)
                            .ToHashSet(StringComparer.Ordinal),
                        peopleContext, token);
                    await requestRepository.GrantSubmitterCaveViewAsync(
                        contributorUserId, caveId, token);
                    throw new InvalidOperationException("Simulated late approval failure.");
                }, default));
        }

        await using var verify = database.CreateDbContext("verify", source.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == caveId));
        Assert.False(await verify.CaveRevisions.AnyAsync(revision => revision.CaveId == caveId));
        Assert.False(await verify.CavePermissions.IgnoreQueryFilters().AnyAsync(permission =>
            permission.AccountId == source.AccountId &&
            permission.UserId == contributorUserId &&
            permission.CaveId == caveId));
        var persistedRequest = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Pending, persistedRequest.Status);
        Assert.Null(persistedRequest.ApprovedRevisionId);
    }


    private static void PrepareAutomaticCreate(AddCaveVm values)
    {
        values.Id = null;
        values.ExpectedRevisionId = null;
        values.IsCountyNumberManuallySet = false;
        values.UseFirstAvailableCountyNumber = false;
        values.CountyNumber = null;
    }

    private static void AddSharedNewPerson(AddCaveVm values)
    {
        values.CartographerNameTagIds = values.CartographerNameTagIds.Append("Shared New Person").ToList();
        values.ReportedByNameTagIds = values.ReportedByNameTagIds.Append("Shared New Person").ToList();
        var primary = values.Entrances.Single(entrance => entrance.IsPrimary);
        primary.ReportedByNameTagIds = primary.ReportedByNameTagIds.Append("Shared New Person").ToList();
    }

    private static void AssertCreationReferenceIntegrity(
        CavePublishedSnapshotV1 snapshot,
        CaveAuthoringFieldPersistenceIntegrationTests.ReferenceCatalog references)
    {
        Assert.Equal(references.TargetStateId, snapshot.State.Id);
        Assert.Equal(references.TargetCountyId, snapshot.County.Id);

        AssertTag(snapshot.Tags, SnapshotTagRole.Geology, references.Geology);
        AssertTag(snapshot.Tags, SnapshotTagRole.GeologicAge, references.GeologicAge);
        AssertTag(snapshot.Tags, SnapshotTagRole.MapStatus, references.MapStatus);
        AssertTag(snapshot.Tags, SnapshotTagRole.PhysiographicProvince, references.PhysiographicProvince);
        AssertTag(snapshot.Tags, SnapshotTagRole.Archeology, references.Archeology);
        AssertTag(snapshot.Tags, SnapshotTagRole.Biology, references.Biology);
        AssertTag(snapshot.Tags, SnapshotTagRole.CaveOther, references.CaveOther);
        AssertTag(snapshot.Tags, SnapshotTagRole.Cartographer, references.Cartographer);
        AssertTag(snapshot.Tags, SnapshotTagRole.CaveReportedBy, references.CaveReporter);

        var primary = snapshot.Entrances.Single(entrance => entrance.Name == "Main Entrance");
        var secondary = snapshot.Entrances.Single(entrance => entrance.Name == "Secondary Entrance");
        Assert.Equal(references.LocationQuality.Id, primary.LocationQualityTagId);
        Assert.Equal(references.LocationQuality.Id, secondary.LocationQualityTagId);
        AssertTag(primary.Tags, SnapshotTagRole.EntranceStatus, references.EntranceStatus);
        AssertTag(primary.Tags, SnapshotTagRole.EntranceHydrology, references.EntranceHydrology);
        AssertTag(primary.Tags, SnapshotTagRole.FieldIndication, references.FieldIndication);
        AssertTag(primary.Tags, SnapshotTagRole.EntranceReportedBy, references.EntranceReporter);
        AssertTag(primary.Tags, SnapshotTagRole.EntranceOther, references.EntranceOther);

        var file = Assert.Single(snapshot.Files);
        Assert.Equal(references.SurveyFile.Id, file.FileTypeTagId);

        var generatedPeople = snapshot.Tags
            .Concat(snapshot.Entrances.SelectMany(entrance => entrance.Tags))
            .Where(tag => tag.NameAtRevision == "Shared New Person")
            .ToList();
        Assert.Equal(3, generatedPeople.Count);
        Assert.Single(generatedPeople.Select(tag => tag.TagTypeId).Distinct(StringComparer.Ordinal));
        Assert.Contains(generatedPeople, tag => tag.Role == SnapshotTagRole.Cartographer);
        Assert.Contains(generatedPeople, tag => tag.Role == SnapshotTagRole.CaveReportedBy);
        Assert.Contains(generatedPeople, tag => tag.Role == SnapshotTagRole.EntranceReportedBy);

        static void AssertTag(
            IEnumerable<SnapshotTagReference> tags,
            SnapshotTagRole role,
            Planarian.Model.Database.Entities.TagType expected) =>
            Assert.Contains(tags, tag =>
                tag.Role == role &&
                tag.TagTypeId == expected.Id &&
                tag.NameAtRevision == expected.Name);
    }

    private static CavePublishedSnapshotV1 CanonicalizeCreationSnapshot(CavePublishedSnapshotV1 snapshot)
    {
        static SnapshotTagReference NormalizeTag(SnapshotTagReference tag) =>
            tag with { TagTypeId = $"<tag:{tag.Role}:{tag.NameAtRevision}>" };

        return snapshot with
        {
            AccountId = "<account>",
            CaveId = "<cave>",
            Tags = snapshot.Tags.Select(NormalizeTag)
                .OrderBy(tag => tag.Role).ThenBy(tag => tag.NameAtRevision, StringComparer.Ordinal).ToList(),
            Entrances = snapshot.Entrances.OrderBy(entrance => entrance.Name, StringComparer.Ordinal)
                .Select((entrance, index) => entrance with
                {
                    Id = $"<entrance-{index}>",
                    LocationQualityTagId = $"<location-quality:{entrance.LocationQualityNameAtRevision}>",
                    Tags = entrance.Tags.Select(NormalizeTag)
                        .OrderBy(tag => tag.Role).ThenBy(tag => tag.NameAtRevision, StringComparer.Ordinal).ToList()
                }).ToList(),
            Files = snapshot.Files.OrderBy(file => file.Name, StringComparer.Ordinal)
                .Select((file, index) => file with
                {
                    Id = $"<file-{index}>",
                    FileTypeTagId = $"<file-type:{file.FileTypeNameAtRevision}>"
                }).ToList(),
            LinePlots = snapshot.LinePlots.OrderBy(plot => plot.Name, StringComparer.Ordinal)
                .Select((plot, index) => plot with { Id = $"<line-{index}>" }).ToList()
        };
    }

    private static AddCaveVm NewCaveValues(string stateId, string countyId, string locationQualityTagId) => new()
    {
        Name = "Proposed Hidden Cave",
        StateId = stateId,
        CountyId = countyId,
        IsCountyNumberManuallySet = false,
        UseFirstAvailableCountyNumber = false,
        AlternateNames = [],
        Entrances =
        [
            new AddEntranceVm
            {
                IsPrimary = true,
                Name = "Main Entrance",
                Latitude = 35,
                Longitude = -86,
                ElevationFeet = 500,
                LocationQualityTagId = locationQualityTagId
            }
        ]
    };
}
