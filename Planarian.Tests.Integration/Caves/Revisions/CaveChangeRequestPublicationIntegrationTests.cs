using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
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

public sealed class CaveChangeRequestPublicationIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task AgainstCurrentRereviewRetainsServerAllocatedProposalEntranceIdThroughApproval()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AgainstCurrentRereviewRetainsServerAllocatedProposalEntranceIdThroughApproval));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionOne;
        string proposalEntranceId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                PublishableValues(tenant, quality.Id, "Initial proposal"), tenant.RevisionId, default);
            versionOne = await CurrentVersionAsync(contributor.Db, requestId);
            var version = await contributor.Db.CaveProposalVersions.SingleAsync(row => row.Id == versionOne);
            proposalEntranceId = Assert.Single(CaveProposalJson.Deserialize(version.ProposalJson, 1).Entrances)
                .EntranceId;
        }

        string currentRevisionId;
        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            currentRevisionId = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                cave => cave.Narrative = "Independent publication")).RevisionId!;
        }

        string versionTwo;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var rereviewed = PublishableValues(tenant, quality.Id, "Rereviewed proposal");
            rereviewed.Narrative = "Independent publication";
            rereviewed.Entrances.Single().Id = proposalEntranceId;
            versionTwo = await contributor.ChangeRequests.AddVersionAsync(requestId, rereviewed,
                againstCurrent: true, expectedBaseRevisionId: currentRevisionId,
                expectedProposalVersionId: versionOne, default);
            var persisted = await contributor.Db.CaveProposalVersions.SingleAsync(row => row.Id == versionTwo);
            Assert.Equal(proposalEntranceId,
                Assert.Single(CaveProposalJson.Deserialize(persisted.ProposalJson, 1).Entrances).EntranceId);
        }

        string acceptedRevisionId;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            acceptedRevisionId = (await reviewer.ChangeRequests.ApproveAsync(requestId, versionTwo, null, default))
                .PublishedRevisionId!;

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(proposalEntranceId, Assert.Single(await verify.Entrances.Where(entrance =>
            entrance.CaveId == tenant.CaveId).ToListAsync()).Id);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == acceptedRevisionId)).SnapshotJson, 1);
        Assert.Equal(proposalEntranceId, Assert.Single(accepted.Entrances).Id);
    }

    [Fact]
    public async Task ServerAllocatedProposalEntranceIdSurvivesPublication()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ServerAllocatedProposalEntranceIdSurvivesPublication));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string proposalEntranceId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var values = PublishableValues(tenant, locationTag.Id, "Entrance identity");
            values.CartographerNameTagIds = ["Shared New Person"];
            values.ReportedByNameTagIds = ["Shared New Person"];
            values.Entrances.Single().ReportedByNameTagIds = ["Shared New Person"];
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                values, tenant.RevisionId, default);
            var version = await contributor.Db.CaveProposalVersions.SingleAsync(row =>
                row.ChangeRequestId == requestId);
            proposalEntranceId = Assert.Single(CaveProposalJson.Deserialize(version.ProposalJson, 1).Entrances)
                .EntranceId;
            Assert.False(string.IsNullOrWhiteSpace(proposalEntranceId));
        }

        string revisionId;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            revisionId = (await reviewer.ChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer.Db, requestId), null, default)).PublishedRevisionId!;

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(proposalEntranceId, Assert.Single(await verify.Entrances.Where(entrance =>
            entrance.CaveId == tenant.CaveId).ToListAsync()).Id);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == revisionId)).SnapshotJson, 1);
        Assert.Equal(proposalEntranceId, Assert.Single(accepted.Entrances).Id);
        var person = Assert.Single(await verify.TagTypes.Where(tag => tag.AccountId == tenant.AccountId &&
            tag.Key == TagTypeKeyConstant.People && tag.Name == "Shared New Person").ToListAsync());
        Assert.Contains(accepted.Tags, tag => tag.Role == SnapshotTagRole.Cartographer &&
            tag.TagTypeId == person.Id);
        Assert.Contains(accepted.Tags, tag => tag.Role == SnapshotTagRole.CaveReportedBy &&
            tag.TagTypeId == person.Id);
        Assert.Contains(accepted.Entrances.Single().Tags, tag => tag.Role == SnapshotTagRole.EntranceReportedBy &&
            tag.TagTypeId == person.Id);
    }

    [Fact]
    public async Task ApprovalReturnsThePublishedRevisionWithoutPostCommitReload()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovalReturnsThePublishedRevisionWithoutPostCommitReload));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            requestId = await new CaveChangeRequestRepository(contributor, contributor.RequestUser).CreateAsync(
                tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Published revision response"), default);
        }

        CaveChangeRequestDecisionVm decision;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            decision = await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default);
        }

        Assert.Equal(CaveChangeRequestDecisionResult.Approved, decision.Result);
        Assert.False(string.IsNullOrWhiteSpace(decision.PublishedRevisionId));
        Assert.Equal(decision.PublishedRevisionId, decision.CurrentRevisionId);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var request = await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, request.Status);
        Assert.Equal(decision.PublishedRevisionId, request.ApprovedRevisionId);
        var revision = await verify.CaveRevisions.SingleAsync(row => row.ChangeRequestId == requestId);
        Assert.Equal(decision.PublishedRevisionId, revision.Id);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(decision.PublishedRevisionId, cave.CurrentRevisionId);
    }

    [Fact]
    public async Task ApplicationApprovalPublishesNormalizedCaveAndExactlyOneLinkedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApplicationApprovalPublishesNormalizedCaveAndExactlyOneLinkedRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            result = await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "Looks correct", default);
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
    public async Task EntranceOtherTagsSurviveProposalApprovalAndUnrelatedDirectEdit()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EntranceOtherTagsSurviveProposalApprovalAndUnrelatedDirectEdit));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var otherTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.CaveOther, "Sensitive entrance value", "entrothera");
        const string entranceId = "entrance0a";
        await EntranceTestData.AddEntranceAsync(database, tenant, entranceId,
            locationQualityTagId: locationTag.Id);
        await using (var seed = database.CreateDbContext("other-tag-seed", tenant.AccountId))
        {
            seed.EntranceOtherTag.Add(new EntranceOtherTag
                { EntranceId = entranceId, TagTypeId = otherTag.Id });
            await seed.SaveChangesAsync();
        }
        await using (var baseline = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutation = await new CaveMutationRepository(baseline, baseline.RequestUser,
                    new CavePublishedSnapshotRepository(baseline, baseline.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, cave => cave.Narrative = "Baseline with entrance tags");
            tenant = tenant with { RevisionId = mutation.RevisionId! };
        }
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var read = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tenant.CaveId);
            Assert.Equal([otherTag.Id], read!.Entrances.Single().EntranceOtherTagIds);
            var values = ValuesFromCave(read);
            values.Name = "Name-only proposal";
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            var preview = await service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default);
            Assert.DoesNotContain(preview.Diff.RemovedTags, tag =>
                tag.Role == SnapshotTagRole.EntranceOther && tag.TagTypeId == otherTag.Id);
            requestId = await service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default);
        }

        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
            var read = await new CaveRepository(manager, manager.RequestUser).GetCave(tenant.CaveId);
            var values = ValuesFromCave(read!);
            values.Narrative = "Unrelated direct edit";
            await IntegrationTestServices.For(manager).Caves.AddCave(values, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.True(await verify.EntranceOtherTag.AnyAsync(tag =>
            tag.EntranceId == entranceId && tag.TagTypeId == otherTag.Id));
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.ChangeRequestId == requestId)).SnapshotJson, 1);
        Assert.Contains(accepted.Entrances.Single(entrance => entrance.Id == entranceId).Tags,
            tag => tag.Role == SnapshotTagRole.EntranceOther && tag.TagTypeId == otherTag.Id);
        var currentRevisionId = (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.Id == tenant.CaveId)).CurrentRevisionId;
        var current = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == currentRevisionId)).SnapshotJson, 1);
        Assert.Contains(current.Entrances.Single(entrance => entrance.Id == entranceId).Tags,
            tag => tag.Role == SnapshotTagRole.EntranceOther && tag.TagTypeId == otherTag.Id);
    }

    [Fact]
    public async Task PendingRequestBlocksHardDeleteUntilItIsResolved()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PendingRequestBlocksHardDeleteUntilItIsResolved));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string versionOneId;
        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Pending V1"), default);
            versionOneId = await CurrentVersionAsync(contributor, requestId);
            versionTwoId = await requests.AddVersionAsync(requestId, tenant.RevisionId, versionOneId,
                Proposal(tenant, "Rejected V2"), reviewer: false, againstCurrent: false, default);
        }

        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                IntegrationTestServices.For(manager).Caves.DeleteCave(tenant.CaveId, default));
            Assert.Contains("Pending proposed changes", failure.Message);
        }
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            await IntegrationTestServices.For(reviewer).CaveChangeRequests.RejectAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "Resolved before deletion", default);
        }
        await using (var manager = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(manager, tenant.AccountId);
            await IntegrationTestServices.For(manager).Caves.DeleteCave(tenant.CaveId, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
        Assert.True(await verify.CaveChangeRequests.AnyAsync(request => request.Id == requestId));
        Assert.Equal(2, await verify.CaveProposalVersions.CountAsync(version =>
            version.ChangeRequestId == requestId));

        await using var contributorHistory = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(contributorHistory, tenant.AccountId);
        var history = IntegrationTestServices.For(contributorHistory).CaveChangeRequests;
        Assert.DoesNotContain(await history.ListMineAsync(default), request => request.Id == requestId);
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => history.GetAsync(requestId, default));
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            history.GetVersionAsync(requestId, versionOneId, default));

        await using var reviewQueue = database.CreateDbContext("reviewer", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(reviewQueue, tenant.AccountId);
        Assert.DoesNotContain(await IntegrationTestServices.For(reviewQueue).CaveChangeRequests.ListForReviewAsync(default),
            request => request.Id == requestId);
    }

    [Fact]
    public async Task AutomaticCountyMovePublishesAllocatedNumberAndRevisionRecordsIt()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AutomaticCountyMovePublishesAllocatedNumberAndRevisionRecordsIt));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        const string newCountyId = "county999a";
        await using (var seed = database.CreateDbContext("county-seed", tenant.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = newCountyId, AccountId = tenant.AccountId, StateId = tenant.StateId,
                DisplayId = "A99", Name = "New County"
            });
            await seed.SaveChangesAsync();
        }
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var proposal = PublishableProposal(tenant, locationTag.Id, "Moved Cave") with
            {
                CountyId = newCountyId,
                CountyNumberIntent = CountyNumberIntent.AutomaticNext,
                RequestedCountyNumber = null
            };
            requestId = await new CaveChangeRequestRepository(contributor, contributor.RequestUser)
                .CreateAsync(tenant.CaveId, tenant.RevisionId, proposal, default);
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var detail = await IntegrationTestServices.For(contributor).CaveChangeRequests.GetAsync(requestId, default);
            Assert.Equal(CountyNumberIntent.AutomaticNext, detail.CountyNumberIntent);
            Assert.Null(detail.RequestedCountyNumber);
        }

        string revisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            revisionId = (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                    await CurrentVersionAsync(reviewer, requestId), null, default))
                .PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(newCountyId, cave.CountyId);
        Assert.Equal(1, cave.CountyNumber);
        var snapshot = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(row => row.Id == revisionId))
            .SnapshotJson, 1);
        Assert.Equal(newCountyId, snapshot.County.Id);
        Assert.Equal(1, snapshot.CountyNumber);
    }

    public static IEnumerable<object?[]> MeasurementPreservationCases()
    {
        yield return [null, null, null, null];
        yield return [0d, 0d, 0d, 0];
        yield return [null, 0d, 27d, 3];
    }

    [Theory]
    [MemberData(nameof(MeasurementPreservationCases))]
    public async Task UnrelatedProposalEditPreservesNullableMeasurementSemantics(
        double? length, double? depth, double? maxPitDepth, int? numberOfPits)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(UnrelatedProposalEditPreservesNullableMeasurementSemantics)}_{length}_{depth}_{maxPitDepth}_{numberOfPits}");
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a',
            length, depth, maxPitDepth, numberOfPits);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var context = await IntegrationTestServices.For(contributor).CaveChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.Name = "Unrelated name edit";
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(tenant.CaveId, values,
                context.ExpectedBaseRevisionId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(length, cave.LengthFeet);
        Assert.Equal(depth, cave.DepthFeet);
        Assert.Equal(maxPitDepth, cave.MaxPitDepthFeet);
        Assert.Equal(numberOfPits, cave.NumberOfPits);
        var revision = await verify.CaveRevisions.SingleAsync(row => row.Id == cave.CurrentRevisionId);
        var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, revision.SnapshotSchemaVersion);
        Assert.Equal(length, snapshot.LengthFeet);
        Assert.Equal(depth, snapshot.DepthFeet);
        Assert.Equal(maxPitDepth, snapshot.MaxPitDepthFeet);
        Assert.Equal(numberOfPits, snapshot.NumberOfPits);
    }

    [Fact]
    public async Task ProposalCanIntentionallyTransitionMeasurementsBetweenNullZeroAndPositive()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalCanIntentionallyTransitionMeasurementsBetweenNullZeroAndPositive));
        var (tenant, _) = await CreateMeasuredPublishedCaveAsync(database, 'a', null, 25, 0, null);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var context = await IntegrationTestServices.For(contributor).CaveChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
            var values = ValuesFromCave(context.Cave);
            values.LengthFeet = 10;
            values.DepthFeet = null;
            values.MaxPitDepthFeet = null;
            values.NumberOfPits = 0;
            requestId = await IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(tenant.CaveId, values,
                context.ExpectedBaseRevisionId, default);
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var cave = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == tenant.CaveId);
        Assert.Equal(10, cave.LengthFeet);
        Assert.Null(cave.DepthFeet);
        Assert.Null(cave.MaxPitDepthFeet);
        Assert.Equal(0, cave.NumberOfPits);
    }

    [Fact]
    public async Task ImportedUnknownMeasurementsDoNotBecomeSyntheticZeroProposalChanges()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ImportedUnknownMeasurementsDoNotBecomeSyntheticZeroProposalChanges));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        string importedCaveId;
        string importedRevisionId;
        await using (var importer = database.CreateDbContext("importer", tenant.AccountId))
        {
            var harness = new CaveImportTestHarness(importer, importer.RequestUser);
            var csv = ImportDryRunIntegrationTests.CaveHeader + "\n" +
                      "Imported Unknowns,County A,A01,20,AA,,,,,,,,,,,,,,,false,,\n";
            var plan = await harness.PlanCsvAsync(csv, syncExisting: false);
            importedCaveId = Assert.Single(plan.Caves).Id;
            await harness.ExecuteAsync(plan, "unknown-measurements.csv");
            importer.ChangeTracker.Clear();
            importedRevisionId = (await importer.Caves.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == importedCaveId)).CurrentRevisionId!;
        }
        var imported = tenant with
        {
            CaveId = importedCaveId,
            CaveName = "Imported Unknowns",
            CountyNumber = 20,
            RevisionId = importedRevisionId
        };
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await using (var validImport = database.CreateDbContext("valid-import", tenant.AccountId))
        {
            var entrance = new Entrance
            {
                Id = "importent1", CaveId = importedCaveId, IsPrimary = true,
                LocationQualityTagId = locationTag.Id, Location = new Point(-86, 35, 500) { SRID = 4326 }
            };
            validImport.Entrances.Add(entrance);
            var revision = await validImport.CaveRevisions.SingleAsync(row => row.Id == importedRevisionId);
            var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, 1) with
            {
                Entrances = [new CaveEntranceSnapshotV1
                {
                    Id = entrance.Id, IsPrimary = true, Latitude = 35, Longitude = -86, Elevation = 500,
                    LocationQualityTagId = locationTag.Id, LocationQualityNameAtRevision = locationTag.Name
                }]
            };
            revision.SnapshotJson = CaveSnapshotJson.Serialize(snapshot);
            await validImport.SaveChangesAsync();
        }
        await CavePermissions.GrantViewAsync(database, imported, "contributor");

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
        var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
        var context = await service.GetAuthoringContextAsync(importedCaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.Name = "Imported Unknowns Renamed";

        var preview = await service.PreviewAsync(importedCaveId, values, importedRevisionId, default);
        var requestId = await service.CreateAsync(importedCaveId, values, importedRevisionId, default);

        Assert.Equal(["Name"], preview.Diff.Scalars.Select(change => change.Path));
        var stored = await contributor.CaveProposalVersions.SingleAsync(row => row.ChangeRequestId == requestId);
        var proposal = CaveProposalJson.Deserialize(stored.ProposalJson, stored.SchemaVersion);
        Assert.Null(proposal.LengthFeet);
        Assert.Null(proposal.DepthFeet);
        Assert.Null(proposal.MaxPitDepthFeet);
        Assert.Null(proposal.NumberOfPits);
    }
}
