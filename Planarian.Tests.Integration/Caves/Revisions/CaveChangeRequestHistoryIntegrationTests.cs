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

public sealed class CaveChangeRequestHistoryIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task HistoricalProposalVersionCanBeInspectedOnlyWithinItsReadableRequest()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalProposalVersionCanBeInspectedOnlyWithinItsReadableRequest));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string otherRequestId;
        string versionOneId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Original contributor proposal"), default);
            otherRequestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                Proposal(tenant, "Other request"), default);
            versionOneId = await CurrentVersionAsync(contributor, requestId);
            await requests.AddVersionAsync(requestId, tenant.RevisionId, versionOneId,
                Proposal(tenant, "Reviewer version"), false, false, default);
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            var historical = await service.GetVersionAsync(requestId, versionOneId, default);
            Assert.Equal("Original contributor proposal", historical.Proposed.Name);
            Assert.Contains(historical.DiffFromBase.Scalars, change => change.Path == "Name");
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                service.GetVersionAsync(otherRequestId, versionOneId, default));
        }
    }

    [Fact]
    public async Task ProposalVersionHistoryReportsTagEntranceTagAndFileReversalsFromLinkedPredecessor()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionHistoryReportsTagEntranceTagAndFileReversalsFromLinkedPredecessor));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var biology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        var hydrology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.EntranceHydrology, "Wet", "hydrologya");
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var file = await FileTestDataFactory.AddFileAsync(database, tenant, fileId: "versionf0a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        CaveProposalSnapshotV1 Version(string name, bool includeReversibleItems) => Proposal(tenant, name) with
        {
            Tags = includeReversibleItems
                ? [new SnapshotTagReference(SnapshotTagRole.Biology, biology.Id, biology.Name)] : [],
            Entrances = [new CaveProposalEntranceV1
            {
                EntranceId = "proposalena", Name = "Proposed entrance", IsPrimary = true,
                Latitude = 35, Longitude = -86, Elevation = 500,
                LocationQualityTagId = location.Id,
                Tags = includeReversibleItems
                    ? [new SnapshotTagReference(SnapshotTagRole.EntranceHydrology, hydrology.Id, hydrology.Name)] : []
            }],
            Files = includeReversibleItems
                ? [new ProposalFileIntent(file.FileId, ProposalFileDisposition.PublishStaged,
                    file.FileTypeId, "Version file", "Version file.pdf", "Document")] : []
        };

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
        var requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
            Version("Proposed name", true), default);
        var versionOneId = await CurrentVersionAsync(contributor, requestId);
        var versionTwoId = await repository.AddVersionAsync(requestId, tenant.RevisionId, versionOneId,
            Version("Revised proposed name", false), false, false, default);
        var versionThreeId = await repository.AddVersionAsync(requestId, tenant.RevisionId, versionTwoId,
            Version("Final proposed name", true), false, false, default);

        var versions = await contributor.CaveProposalVersions
            .Where(version => version.ChangeRequestId == requestId).ToListAsync();
        foreach (var version in versions) version.CreatedOn = new DateTime(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
        await contributor.SaveChangesAsync();
        await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
        var service = IntegrationTestServices.For(contributor).CaveChangeRequests;

        var request = await service.GetAsync(requestId, default);
        Assert.Equal([versionOneId, versionTwoId, versionThreeId], request.Versions.Select(version => version.Id));

        var removed = await service.GetVersionAsync(requestId, versionTwoId, default);
        Assert.DoesNotContain(removed.DiffFromBase.AddedTags.Concat(removed.DiffFromBase.RemovedTags),
            tag => tag.Role == SnapshotTagRole.Biology);
        Assert.Contains(removed.DiffFromPreviousVersion!.RemovedTags,
            tag => tag.Role == SnapshotTagRole.Biology && tag.TagTypeId == biology.Id);
        var removedEntrance = Assert.Single(removed.DiffFromPreviousVersion.EntranceChanges,
            change => change.EntranceId == "proposalena");
        Assert.Contains(removedEntrance.RemovedTags,
            tag => tag.Role == SnapshotTagRole.EntranceHydrology && tag.TagTypeId == hydrology.Id);
        Assert.Contains(file.FileId, removed.DiffFromPreviousVersion.RemovedFiles);

        var restored = await service.GetVersionAsync(requestId, versionThreeId, default);
        Assert.Contains(restored.DiffFromPreviousVersion!.AddedTags,
            tag => tag.Role == SnapshotTagRole.Biology && tag.TagTypeId == biology.Id);
        var restoredEntrance = Assert.Single(restored.DiffFromPreviousVersion.EntranceChanges,
            change => change.EntranceId == "proposalena");
        Assert.Contains(restoredEntrance.AddedTags,
            tag => tag.Role == SnapshotTagRole.EntranceHydrology && tag.TagTypeId == hydrology.Id);
        Assert.Contains(file.FileId, restored.DiffFromPreviousVersion.AddedFiles);

    }

    [Fact]
    public async Task SelfReferencingProposalHistoryIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(SelfReferencingProposalHistoryIsRejected));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await using var contributor = database.CreateDbContext("contributor", cave.AccountId);
        var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
        var requestId = await repository.CreateAsync(cave.CaveId, cave.RevisionId,
            Proposal(cave, "Proposal"), default);
        var versionId = await CurrentVersionAsync(contributor, requestId);
        (await contributor.CaveProposalVersions.SingleAsync(version => version.Id == versionId))
            .PreviousProposalVersionId = versionId;
        await contributor.SaveChangesAsync();
        await CavePermissions.AuthenticateAsync(contributor, cave.AccountId);

        var service = IntegrationTestServices.For(contributor).CaveChangeRequests;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(requestId, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetVersionAsync(requestId, versionId, default));
    }

    [Fact]
    public async Task CyclicProposalHistoryIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(CyclicProposalHistoryIsRejected));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await using var contributor = database.CreateDbContext("contributor", cave.AccountId);
        var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
        var requestId = await repository.CreateAsync(cave.CaveId, cave.RevisionId,
            Proposal(cave, "First proposal"), default);
        var firstId = await CurrentVersionAsync(contributor, requestId);
        var secondId = await repository.AddVersionAsync(requestId, cave.RevisionId, firstId,
            Proposal(cave, "Second proposal"), false, false, default);
        (await contributor.CaveProposalVersions.SingleAsync(version => version.Id == firstId))
            .PreviousProposalVersionId = secondId;
        await contributor.SaveChangesAsync();
        await CavePermissions.AuthenticateAsync(contributor, cave.AccountId);

        var service = IntegrationTestServices.For(contributor).CaveChangeRequests;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(requestId, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetVersionAsync(requestId, firstId, default));
    }

    [Fact]
    public async Task RebasedProposalHistoryReportsCountyNumberRelativeToCurrentRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RebasedProposalHistoryReportsCountyNumberRelativeToCurrentRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        const string countyY = "county999a";
        await using (var seed = database.CreateDbContext("county-seed", tenant.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = countyY, AccountId = tenant.AccountId, StateId = tenant.StateId,
                DisplayId = "A99", Name = "New County"
            });
            await seed.SaveChangesAsync();
        }
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        string rebasedRequestId;
        string rebasedVersionOneId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Allocate first available in County Y");
            values.CountyId = countyY;
            values.CountyNumber = null;
            values.IsCountyNumberManuallySet = false;
            values.UseFirstAvailableCountyNumber = true;
            rebasedRequestId = await IntegrationTestServices.For(contributor).CaveChangeRequests.CreateAsync(tenant.CaveId,
                values, tenant.RevisionId, default);
            rebasedVersionOneId = await CurrentVersionAsync(contributor, rebasedRequestId);
        }

        string revisionB;
        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            revisionB = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave =>
                {
                    cave.CountyId = countyY;
                    cave.CountyNumber = 47;
                })).RevisionId!;
        }

        string rebasedVersionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var values = PublishableValues(tenant, locationTag.Id, "Rebased proposal in County Y");
            values.CountyId = countyY;
            values.CountyNumber = 47;
            values.IsCountyNumberManuallySet = false;
            values.UseFirstAvailableCountyNumber = false;
            rebasedVersionTwoId = await IntegrationTestServices.For(contributor).CaveChangeRequests.AddVersionAsync(rebasedRequestId,
                values, againstCurrent: true, expectedBaseRevisionId: revisionB,
                expectedProposalVersionId: rebasedVersionOneId, default);
        }

        await using var audit = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(audit, tenant.AccountId);
        var service = IntegrationTestServices.For(audit).CaveChangeRequests;

        var rebasedDetail = await service.GetVersionAsync(rebasedRequestId, rebasedVersionTwoId, default);
        Assert.Equal(CountyNumberIntent.AutomaticNext, rebasedDetail.CountyNumberIntent);
        Assert.Equal(47, rebasedDetail.Proposed.CountyNumber);
        Assert.Equal(CaveProposalCountyNumberMode.FirstAvailable,
            rebasedDetail.CountyNumberChange!.Previous.Mode);
        Assert.Equal(CaveProposalCountyNumberMode.PreserveExisting,
            rebasedDetail.CountyNumberChange.Current.Mode);
        Assert.Equal(47, rebasedDetail.CountyNumberChange.Current.Number);
        Assert.True(rebasedDetail.BaseRevisionChanged);
    }

    [Fact]
    public async Task HistoricalProposalVersionKeepsStateCountyAndLocationQualityLabelsCapturedAtCreation()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(HistoricalProposalVersionKeepsStateCountyAndLocationQualityLabelsCapturedAtCreation));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var location = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade A", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using var contributor = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
        var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
        var firstValues = PublishableValues(tenant, location.Id, "Proposal version one");
        var requestId = await service.CreateAsync(tenant.CaveId, firstValues, tenant.RevisionId, default);
        var versionOneId = await CurrentVersionAsync(contributor, requestId);

        await GlobalStateTestData.RenameAsync(database, tenant.StateId, "State B", "BB");
        await using (var rename = database.CreateDbContext("manager", tenant.AccountId))
        {
            var county = await rename.Counties.SingleAsync(row => row.Id == tenant.CountyId);
            county.Name = "County B";
            county.DisplayId = "BB02";
            (await rename.TagTypes.SingleAsync(tag => tag.Id == location.Id)).Name = "Survey Grade B";
            await rename.SaveChangesAsync();
        }

        var historical = await service.GetVersionAsync(requestId, versionOneId, default);
        Assert.Equal((tenant.StateName, tenant.StateAbbreviation),
            (historical.Proposed.State.NameAtRevision, historical.Proposed.State.AbbreviationAtRevision));
        Assert.Equal((tenant.CountyName, tenant.CountyDisplayId),
            (historical.Proposed.County.NameAtRevision, historical.Proposed.County.DisplayIdAtRevision));
        Assert.Equal("Survey Grade A", Assert.Single(historical.Proposed.Entrances)
            .LocationQualityNameAtRevision);

        var secondValues = PublishableValues(tenant, location.Id, "Proposal version two");
        var versionTwoId = await service.AddVersionAsync(requestId, secondValues, false,
            tenant.RevisionId, versionOneId, default);
        var later = await service.GetVersionAsync(requestId, versionTwoId, default);
        Assert.Equal(("State B", "BB"),
            (later.Proposed.State.NameAtRevision, later.Proposed.State.AbbreviationAtRevision));
        Assert.Equal(("County B", "BB02"),
            (later.Proposed.County.NameAtRevision, later.Proposed.County.DisplayIdAtRevision));
        Assert.Equal("Survey Grade B", Assert.Single(later.Proposed.Entrances)
            .LocationQualityNameAtRevision);
    }

    [Fact]
    public async Task RemovedCaveTagAppearsAsRemovedWhenApprovedRevisionIsLoadedFromHistory()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RemovedCaveTagAppearsAsRemovedWhenApprovedRevisionIsLoadedFromHistory));
        var tagged = await CreateTaggedPublishedCaveAsync(database);
        await CavePermissions.GrantViewAsync(database, tagged.Cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, tagged.Cave, "reviewer");
        string requestId;

        await using (var contributor = database.CreateDbContext("contributor", tagged.Cave.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tagged.Cave.AccountId);
            var cave = await new CaveRepository(contributor, contributor.RequestUser).GetCave(tagged.Cave.CaveId);
            var values = ValuesFromCave(cave!);
            values.BiologyTagIds = [];
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            var preview = await service.PreviewAsync(tagged.Cave.CaveId, values, tagged.Cave.RevisionId, default);
            Assert.Contains(preview.Diff.RemovedTags,
                tag => tag.Role == SnapshotTagRole.Biology && tag.TagTypeId == tagged.BiologyTagId);
            requestId = await service.CreateAsync(tagged.Cave.CaveId, values, tagged.Cave.RevisionId, default);
            var detail = await service.GetAsync(requestId, default);
            Assert.Contains(detail.Diff.RemovedTags,
                tag => tag.Role == SnapshotTagRole.Biology && tag.TagTypeId == tagged.BiologyTagId);
            var stored = CaveProposalJson.Deserialize((await contributor.CaveProposalVersions.SingleAsync(version =>
                version.Id == detail.Request.CurrentProposalVersionId)).ProposalJson, 1);
            Assert.DoesNotContain(stored.Tags,
                tag => tag.Role == SnapshotTagRole.Biology && tag.TagTypeId == tagged.BiologyTagId);
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tagged.Cave.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tagged.Cave.AccountId);
            var requestService = IntegrationTestServices.For(reviewer).CaveChangeRequests;
            approvedRevisionId = (await requestService.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), null, default)).PublishedRevisionId!;
            var history = await new CaveRevisionService(new CaveRepository(reviewer, reviewer.RequestUser),
                new CaveRevisionQueryRepository(reviewer, reviewer.RequestUser))
                .CompareAsync(tagged.Cave.CaveId, approvedRevisionId, default);
            Assert.Contains(history.Diff!.RemovedTags,
                tag => tag.Role == SnapshotTagRole.Biology && tag.TagTypeId == tagged.BiologyTagId);
            Assert.DoesNotContain(history.Diff.AddedTags,
                tag => tag.Role == SnapshotTagRole.Biology && tag.TagTypeId == tagged.BiologyTagId);
        }

        await using var verify = database.CreateDbContext("verify", tagged.Cave.AccountId);
        Assert.False(await verify.BiologyTags.AnyAsync(tag => tag.CaveId == tagged.Cave.CaveId &&
            tag.TagTypeId == tagged.BiologyTagId));
        var approved = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == approvedRevisionId)).SnapshotJson, 1);
        Assert.DoesNotContain(approved.Tags, tag => tag.TagTypeId == tagged.BiologyTagId);
        var previous = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == tagged.Cave.RevisionId)).SnapshotJson, 1);
        Assert.Contains(previous.Tags, tag => tag.TagTypeId == tagged.BiologyTagId);
    }

    [Fact]
    public async Task RemovedEntranceTagAppearsAsRemovedWhenPublishedRevisionIsLoadedFromHistory()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RemovedEntranceTagAppearsAsRemovedWhenPublishedRevisionIsLoadedFromHistory));
        var tagged = await CreateTaggedPublishedCaveAsync(database);
        await CavePermissions.GrantManagerAsync(database, tagged.Cave, "reviewer");
        string revisionId;

        await using (var manager = database.CreateDbContext("reviewer", tagged.Cave.AccountId))
        {
            await CavePermissions.AuthenticateAsync(manager, tagged.Cave.AccountId);
            var cave = await new CaveRepository(manager, manager.RequestUser).GetCave(tagged.Cave.CaveId);
            var values = ValuesFromCave(cave!);
            Assert.Equal([tagged.HydrologyTagId], Assert.Single(values.Entrances).EntranceHydrologyTagIds);
            Assert.Single(values.BiologyTagIds);
            Assert.Single(values.Entrances);
            Assert.Single(values.Entrances.Single().EntranceHydrologyTagIds);
            values.Entrances.Single().EntranceHydrologyTagIds = [];
            await IntegrationTestServices.For(manager).Caves.AddCave(values, default);
            manager.ChangeTracker.Clear();
            Assert.False(await manager.EntranceHydrologyTags.AnyAsync(tag =>
                tag.EntranceId == tagged.EntranceId && tag.TagTypeId == tagged.HydrologyTagId));
            revisionId = (await manager.Caves.IgnoreQueryFilters().SingleAsync(caveRow =>
                caveRow.Id == tagged.Cave.CaveId)).CurrentRevisionId!;
            var history = await new CaveRevisionService(new CaveRepository(manager, manager.RequestUser),
                new CaveRevisionQueryRepository(manager, manager.RequestUser))
                .CompareAsync(tagged.Cave.CaveId, revisionId, default);
            Assert.Contains(tagged.EntranceId, history.Diff!.ChangedEntrances);
            var entrance = Assert.Single(history.Diff.EntranceChanges,
                change => change.EntranceId == tagged.EntranceId);
            Assert.Contains(entrance.RemovedTags,
                tag => tag.Role == SnapshotTagRole.EntranceHydrology && tag.TagTypeId == tagged.HydrologyTagId);
        }

        await using var verify = database.CreateDbContext("verify", tagged.Cave.AccountId);
        Assert.False(await verify.EntranceHydrologyTags.AnyAsync(tag => tag.EntranceId == tagged.EntranceId &&
            tag.TagTypeId == tagged.HydrologyTagId));
        var current = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == revisionId)).SnapshotJson, 1);
        Assert.DoesNotContain(current.Entrances.Single().Tags, tag => tag.TagTypeId == tagged.HydrologyTagId);
        var previous = CaveSnapshotJson.Deserialize((await verify.CaveRevisions.SingleAsync(revision =>
            revision.Id == tagged.Cave.RevisionId)).SnapshotJson, 1);
        Assert.Contains(previous.Entrances.Single().Tags, tag => tag.TagTypeId == tagged.HydrologyTagId);
    }

    [Fact]
    public async Task ApprovedRequestAndProposalHistoryRemainStoredButAreHiddenAfterLaterHardDelete()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovedRequestAndProposalHistoryRemainStoredButAreHiddenAfterLaterHardDelete));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string proposalVersionId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Approved historical Cave"), default);
            proposalVersionId = await CurrentVersionAsync(contributor, requestId);
        }

        string approvedRevisionId;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            var service = IntegrationTestServices.For(reviewer).CaveChangeRequests;
            approvedRevisionId = (await service.ApproveAsync(requestId, proposalVersionId,
                "Approved for publication", default)).PublishedRevisionId!;
            await IntegrationTestServices.For(reviewer).Caves.DeleteCave(tenant.CaveId, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.False(await verify.Caves.IgnoreQueryFilters().AnyAsync(cave => cave.Id == tenant.CaveId));
        var storedRequest = await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Approved, storedRequest.Status);
        Assert.Equal(approvedRevisionId, storedRequest.ApprovedRevisionId);
        Assert.True(await verify.CaveRevisions.AnyAsync(revision => revision.Id == approvedRevisionId &&
            revision.ChangeRequestId == requestId));

        await using var contributorHistory = database.CreateDbContext("contributor", tenant.AccountId);
        await CavePermissions.AuthenticateAsync(contributorHistory, tenant.AccountId);
        var history = IntegrationTestServices.For(contributorHistory).CaveChangeRequests;
        Assert.DoesNotContain(await history.ListMineAsync(default), request => request.Id == requestId);
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() => history.GetAsync(requestId, default));
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            history.GetVersionAsync(requestId, proposalVersionId, default));
    }
}
