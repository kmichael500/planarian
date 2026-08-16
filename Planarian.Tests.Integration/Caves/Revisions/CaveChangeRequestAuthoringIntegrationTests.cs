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

public sealed class CaveChangeRequestAuthoringIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task CanonicallyDuplicateEntranceAndFileIdsAreRejectedBeforeProposalPersistence()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CanonicallyDuplicateEntranceAndFileIdsAreRejectedBeforeProposalPersistence));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");

        var duplicateEntrances = PublishableValues(tenant, quality.Id, "Duplicate Entrances");
        duplicateEntrances.Entrances.Single().Id = "entrance01";
        duplicateEntrances.Entrances = duplicateEntrances.Entrances.Append(new AddEntranceVm
        {
            Id = " entrance01 ", IsPrimary = false, LocationQualityTagId = quality.Id,
            Latitude = 35, Longitude = -86, ElevationFeet = 500
        }).ToList();
        var entranceFailure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, duplicateEntrances, tenant.RevisionId, default));
        Assert.Equal(400, entranceFailure.StatusCode);
        Assert.Equal("Entrance IDs must be unique within a Cave mutation.", entranceFailure.Message);

        var duplicateFiles = PublishableValues(tenant, quality.Id, "Duplicate Files");
        duplicateFiles.Files =
        [
            new EditFileMetadataVm { Id = "file0001" },
            new EditFileMetadataVm { Id = " file0001 " }
        ];
        var fileFailure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, duplicateFiles, tenant.RevisionId, default));
        Assert.Equal(400, fileFailure.StatusCode);
        Assert.Equal("File IDs must be unique within a Cave mutation.", fileFailure.Message);

        Assert.Empty(await contributor.Db.CaveChangeRequests.ToListAsync());
        Assert.Empty(await contributor.Db.CaveProposalVersions.ToListAsync());
    }

    [Fact]
    public async Task ProposalTagBoundaryAcceptsScopedTypedTagsAndRejectsForeignWrongKeyAndFreeFormValues()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalTagBoundaryAcceptsScopedTypedTagsAndRejectsForeignWrongKeyAndFreeFormValues));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var foreignTenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var ownedBiology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Biology, "Bat", "biology00a");
        var defaultBiology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biologydef", isDefault: true);
        var foreignBiology = await ReferenceTestData.AddTagAsync(database, foreignTenant.AccountId,
            TagTypeKeyConstant.Biology, "Foreign", "biology00b");
        var archeology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Archeology, "Burial", "archaeo00a");
        var person = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Existing Person", "people000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var valid = PublishableValues(tenant, quality.Id, "Valid typed references");
        valid.BiologyTagIds = [ownedBiology.Id, defaultBiology.Id];
        valid.ReportedByNameTagIds = [person.Id];
        var requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId, valid,
            tenant.RevisionId, default);
        Assert.False(string.IsNullOrWhiteSpace(requestId));

        foreach (var invalidBiology in new[] { foreignBiology.Id, archeology.Id, person.Id, "Cricket" })
        {
            var invalid = PublishableValues(tenant, quality.Id, $"Reject {invalidBiology}");
            invalid.BiologyTagIds = [invalidBiology];
            await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                contributor.ChangeRequests.CreateAsync(tenant.CaveId, invalid, tenant.RevisionId, default));
        }

        var freeFormArcheology = PublishableValues(tenant, quality.Id, "Reject free-form archeology");
        freeFormArcheology.ArcheologyTagIds = ["Uncatalogued site"];
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, freeFormArcheology, tenant.RevisionId, default));

        var wrongPeopleKey = PublishableValues(tenant, quality.Id, "Reject wrong People key");
        wrongPeopleKey.CartographerNameTagIds = [ownedBiology.Id];
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, wrongPeopleKey, tenant.RevisionId, default));

        var wrongQuality = PublishableValues(tenant, ownedBiology.Id, "Reject wrong quality key");
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, wrongQuality, tenant.RevisionId, default));

        var wrongFileType = PublishableValues(tenant, quality.Id, "Reject wrong file key");
        wrongFileType.Files = [new EditFileMetadataVm { Id = "file000001", FileTypeTagId = ownedBiology.Id }];
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, wrongFileType, tenant.RevisionId, default));
    }

    [Fact]
    public async Task DirectMutationAlsoEnforcesTypedTagsAndStateCountyPairing()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DirectMutationAlsoEnforcesTypedTagsAndStateCountyPairing));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var other = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Biology, "Bat", "biology00a");
        var archeology = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.Archeology, "Burial", "archaeo00a");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager");
        var valid = PublishableValues(tenant, quality.Id, "Direct valid");
        valid.BiologyTagIds = [biology.Id];
        Assert.Equal(tenant.CaveId, await manager.Services.Caves.AddCave(valid, default));

        var wrongKey = PublishableValues(tenant, quality.Id, "Direct wrong key");
        wrongKey.BiologyTagIds = [archeology.Id];
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            manager.Services.Caves.AddCave(wrongKey, default));

        var mismatched = PublishableValues(tenant, quality.Id, "Direct mismatched location");
        mismatched.StateId = other.StateId;
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            manager.Services.Caves.AddCave(mismatched, default));

        var proposalMismatch = PublishableValues(tenant, quality.Id, "Proposal mismatched location");
        proposalMismatch.StateId = other.StateId;
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var currentRevisionId = (await manager.Db.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.Id == tenant.CaveId)).CurrentRevisionId!;
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, proposalMismatch, currentRevisionId, default));
    }

    [Fact]
    public async Task FreeFormPeopleArePersistedAsIntentsWithoutPollutingTheTagCatalog()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FreeFormPeopleArePersistedAsIntentsWithoutPollutingTheTagCatalog));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var values = PublishableValues(tenant, locationTag.Id, "People proposal");
        values.CartographerNameTagIds = ["  New Person  "];
        var requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId, values,
            tenant.RevisionId, default);
        var version = await contributor.Db.CaveProposalVersions.SingleAsync(row =>
            row.ChangeRequestId == requestId);
        var proposal = CaveProposalJson.Deserialize(version.ProposalJson, 1);

        Assert.Equal(new ProposalPeopleTagIntent(SnapshotTagRole.Cartographer, "New Person"),
            Assert.Single(proposal.NewPeopleTagIntents));
        Assert.DoesNotContain(proposal.Tags, tag => tag.Role == SnapshotTagRole.Cartographer);
        Assert.False(await contributor.Db.TagTypes.AnyAsync(tag => tag.AccountId == tenant.AccountId &&
            tag.Key == TagTypeKeyConstant.People && tag.Name == "New Person"));
    }

    [Fact]
    public async Task InitialProposalRejectsClientManufacturedEntranceId()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialProposalRejectsClientManufacturedEntranceId));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var values = PublishableValues(tenant, locationTag.Id, "Invalid Entrance identity");
        values.Entrances.Single().Id = "madeup0001";

        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default));
        Assert.False(await contributor.Db.CaveChangeRequests.AnyAsync());
    }

    [Fact]
    public async Task RevisionPreservesServerEntranceIdAndRejectsANewlyManufacturedId()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RevisionPreservesServerEntranceIdAndRejectsANewlyManufacturedId));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
            PublishableValues(tenant, quality.Id, "Version one"), tenant.RevisionId, default);
        var versionOne = await CurrentVersionAsync(contributor.Db, requestId);
        var allocatedId = CaveProposalJson.Deserialize((await contributor.Db.CaveProposalVersions
            .SingleAsync(version => version.Id == versionOne)).ProposalJson, 1).Entrances.Single().EntranceId;

        var preserved = PublishableValues(tenant, quality.Id, "Version two");
        preserved.Entrances.Single().Id = allocatedId;
        var versionTwo = await contributor.ChangeRequests.AddVersionAsync(requestId, preserved, false,
            tenant.RevisionId, versionOne, default);
        Assert.Equal(allocatedId, CaveProposalJson.Deserialize((await contributor.Db.CaveProposalVersions
            .SingleAsync(version => version.Id == versionTwo)).ProposalJson, 1).Entrances.Single().EntranceId);

        var manufactured = PublishableValues(tenant, quality.Id, "Invalid version three");
        manufactured.Entrances.Single().Id = "madeup0002";
        await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.AddVersionAsync(requestId, manufactured, false,
                tenant.RevisionId, versionTwo, default));
    }

    [Fact]
    public async Task InitialSemanticNoOpIsRejectedWithoutPersistingWorkflowState()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialSemanticNoOpIsRejectedWithoutPersistingWorkflowState));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await MakeBaselineStructurallyValidAsync(database, tenant, locationTag.Id);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using (var contributor = await CaveTestActor.CreateAsync(
                         database, tenant.AccountId, "contributor"))
        {
            var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
            var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
                contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                    ValuesFromCave(context.Cave), context.ExpectedBaseRevisionId, default));
            Assert.Contains("does not contain any changes", failure.Message);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Empty(await verify.CaveChangeRequests.ToListAsync());
        Assert.Empty(await verify.CaveProposalVersions.ToListAsync());
        Assert.Empty(await verify.CaveChangeRequestStagedFiles.ToListAsync());
        Assert.Single(await verify.CaveRevisions.Where(row => row.CaveId == tenant.CaveId).ToListAsync());
    }

    [Fact]
    public async Task InitialSemanticNoOpPreviewIsMarkedAsNotMeaningful()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialSemanticNoOpPreviewIsMarkedAsNotMeaningful));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await MakeBaselineStructurallyValidAsync(database, tenant, locationTag.Id);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
        var preview = await contributor.ChangeRequests.PreviewAsync(tenant.CaveId,
            ValuesFromCave(context.Cave), context.ExpectedBaseRevisionId, default);

        Assert.False(preview.HasMeaningfulChanges);
        Assert.Empty(await contributor.Db.CaveChangeRequests.ToListAsync());
        Assert.Empty(await contributor.Db.CaveProposalVersions.ToListAsync());
    }

    [Fact]
    public async Task RevisedSemanticNoOpIsRejectedWithoutSupersedingTheValidVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RevisedSemanticNoOpIsRejectedWithoutSupersedingTheValidVersion));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await MakeBaselineStructurallyValidAsync(database, tenant, locationTag.Id);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var service = contributor.ChangeRequests;
        var requestId = await service.CreateAsync(tenant.CaveId,
            PublishableValues(tenant, locationTag.Id, "Valid proposal"), tenant.RevisionId, default);
        var validVersionId = await CurrentVersionAsync(contributor.Db, requestId);
        var context = await service.GetAuthoringContextAsync(tenant.CaveId, default);

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            service.AddVersionAsync(requestId, ValuesFromCave(context.Cave), false,
                tenant.RevisionId, validVersionId, default));

        Assert.Contains("does not contain any changes", failure.Message);
        contributor.Db.ChangeTracker.Clear();
        Assert.Equal(validVersionId, await CurrentVersionAsync(contributor.Db, requestId));
        Assert.Single(await contributor.Db.CaveProposalVersions.Where(row => row.ChangeRequestId == requestId)
            .ToListAsync());
    }

    [Fact]
    public async Task IdenticalRevisionPreviewIsMarkedAsNotMeaningfulBeforeSave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(IdenticalRevisionPreviewIsMarkedAsNotMeaningfulBeforeSave));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await MakeBaselineStructurallyValidAsync(database, tenant, locationTag.Id);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.Name = "Changed once";
        var requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId, values,
            context.ExpectedBaseRevisionId, default);
        var currentVersionId = await CurrentVersionAsync(contributor.Db, requestId);

        var noOpPreview = await contributor.ChangeRequests.PreviewVersionAsync(requestId, values, false,
            context.ExpectedBaseRevisionId, currentVersionId, default);
        Assert.False(noOpPreview.HasMeaningfulChanges);

        values.Narrative = "Actually changed";
        var changedPreview = await contributor.ChangeRequests.PreviewVersionAsync(requestId, values, false,
            context.ExpectedBaseRevisionId, currentVersionId, default);
        Assert.True(changedPreview.HasMeaningfulChanges);

        contributor.Db.ChangeTracker.Clear();
        Assert.Equal(currentVersionId, await CurrentVersionAsync(contributor.Db, requestId));
        Assert.Single(await contributor.Db.CaveProposalVersions
            .Where(row => row.ChangeRequestId == requestId).ToListAsync());
    }

    [Fact]
    public async Task IdenticalRevisionIsRejectedWithoutCreatingDuplicateProposalVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(IdenticalRevisionIsRejectedWithoutCreatingDuplicateProposalVersion));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await MakeBaselineStructurallyValidAsync(database, tenant, locationTag.Id);
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");
        var context = await contributor.ChangeRequests.GetAuthoringContextAsync(tenant.CaveId, default);
        var values = ValuesFromCave(context.Cave);
        values.Name = "Changed once";
        var requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId, values,
            context.ExpectedBaseRevisionId, default);
        var currentVersionId = await CurrentVersionAsync(contributor.Db, requestId);

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.AddVersionAsync(requestId, values, false,
                context.ExpectedBaseRevisionId, currentVersionId, default));

        Assert.Contains("does not contain any changes", failure.Message);
        contributor.Db.ChangeTracker.Clear();
        Assert.Equal(currentVersionId, await CurrentVersionAsync(contributor.Db, requestId));
        Assert.Single(await contributor.Db.CaveProposalVersions
            .Where(row => row.ChangeRequestId == requestId).ToListAsync());
    }

    [Fact]
    public async Task AuthoringContextForMissingCaveReturnsNotFound()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AuthoringContextForMissingCaveReturnsNotFound));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await using var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor");

        var failure = await Assert.ThrowsAsync<Planarian.Library.Exceptions.ApiException>(() =>
            contributor.ChangeRequests.GetAuthoringContextAsync("missing00a", default));

        Assert.Contains("Cave", failure.Message);
    }

    [Fact]
    public async Task InitialAuthoringRejectsCaveChangedBeforePreviewOrAfterSuccessfulPreview()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InitialAuthoringRejectsCaveChangedBeforePreviewOrAfterSuccessfulPreview));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        var values = PublishableValues(tenant, locationTag.Id, "Expected B");

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            await IntegrationTestServices.For(contributor).CaveChangeRequests.PreviewAsync(tenant.CaveId, values,
                tenant.RevisionId, default);
        }

        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Narrative = "C");
        }

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                service.PreviewAsync(tenant.CaveId, values, tenant.RevisionId, default));
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                service.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Empty(await verify.CaveChangeRequests.ToListAsync());
    }

    [Fact]
    public async Task StaleRereviewRejectsCaveChangedBetweenPreviewAndSave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StaleRereviewRejectsCaveChangedBetweenPreviewAndSave));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string versionOneId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "V1"), default);
            versionOneId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
        }

        string revisionB;
        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            revisionB = (await mutations.PublishExistingAsync(tenant.CaveId, tenant.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Narrative = "B"))
                .RevisionId!;
        }

        var values = PublishableValues(tenant, locationTag.Id, "V2", "B");
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            await IntegrationTestServices.For(contributor).CaveChangeRequests.PreviewVersionAsync(requestId, values, true,
                revisionB, versionOneId, default);
        }

        await using (var manager = database.CreateDbContext("manager", tenant.AccountId))
        {
            var mutations = new CaveMutationRepository(manager, manager.RequestUser,
                new CavePublishedSnapshotRepository(manager, manager.RequestUser));
            await mutations.PublishExistingAsync(tenant.CaveId, revisionB, CaveRevisionSource.ManagerEdit,
                CaveRevisionOperation.Update, cave => cave.Narrative = "C");
        }

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() => service.PreviewVersionAsync(requestId,
                values, true, revisionB, versionOneId, default));
            await Assert.ThrowsAsync<CaveRevisionConflictException>(() => service.AddVersionAsync(requestId,
                values, true, revisionB, versionOneId, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Single(await verify.CaveProposalVersions.Where(row => row.ChangeRequestId == requestId).ToListAsync());
    }

    [Fact]
    public async Task ConcurrentProposalEditorCannotAppendFromSupersededVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ConcurrentProposalEditorCannotAppendFromSupersededVersion));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        string requestId;
        string versionOneId;
        await using (var seed = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var repository = new CaveChangeRequestRepository(seed, seed.RequestUser);
            requestId = await repository.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "V1"), default);
            versionOneId = (await seed.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
        }

        string versionTwoId;
        await using (var actorA = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(actorA, tenant.AccountId);
            versionTwoId = await IntegrationTestServices.For(actorA).CaveChangeRequests.AddVersionAsync(requestId,
                PublishableValues(tenant, locationTag.Id, "A created V2"), false,
                tenant.RevisionId, versionOneId, default);
        }
        await using (var actorB = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(actorB, tenant.AccountId);
            await Assert.ThrowsAsync<CaveProposalVersionConflictException>(() =>
                IntegrationTestServices.For(actorB).CaveChangeRequests.AddVersionAsync(requestId,
                    PublishableValues(tenant, locationTag.Id, "B stale values"), false,
                    tenant.RevisionId, versionOneId, default));
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(2, await verify.CaveProposalVersions.CountAsync(row => row.ChangeRequestId == requestId));
        Assert.Equal(versionTwoId, (await verify.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
            .CurrentProposalVersionId);
    }

    [Fact]
    public async Task ApprovingOneOfTwoPendingRequestsMakesTheOtherStaleUntilExplicitlyRevised()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ApprovingOneOfTwoPendingRequestsMakesTheOtherStaleUntilExplicitlyRevised));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var locationTag = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;
        string competingRequestId;
        string versionOneId;

        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            var requests = new CaveChangeRequestRepository(contributor, contributor.RequestUser);
            competingRequestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Competing proposal",
                    "Published by request A"), default);
            requestId = await requests.CreateAsync(tenant.CaveId, tenant.RevisionId,
                PublishableProposal(tenant, locationTag.Id, "Contributor proposal v1"), default);
            versionOneId = (await contributor.CaveChangeRequests.SingleAsync(row => row.Id == requestId))
                .CurrentProposalVersionId!;
        }

        string revisionB;
        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            revisionB = (await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(
                competingRequestId, await CurrentVersionAsync(reviewer, competingRequestId), null, default))
                .PublishedRevisionId!;
        }

        await using (var reviewer = database.CreateDbContext("reviewer", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            var expectedVersionId = await CurrentVersionAsync(reviewer, requestId);
            var conflict = await Assert.ThrowsAsync<CaveRevisionConflictException>(() =>
                IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                    expectedVersionId, null, default));
            Assert.Equal(revisionB, conflict.ActualRevisionId);
            var staleDetail = await IntegrationTestServices.For(reviewer).CaveChangeRequests.GetAsync(
                requestId, default);
            Assert.Equal(CaveChangeRequestStatus.Pending, staleDetail.Request.Status);
            Assert.True(staleDetail.Request.IsStale);
        }

        string versionTwoId;
        await using (var contributor = database.CreateDbContext("contributor", tenant.AccountId))
        {
            await CavePermissions.AuthenticateAsync(contributor, tenant.AccountId);
            var service = IntegrationTestServices.For(contributor).CaveChangeRequests;
            var revisedValues = PublishableValues(tenant, locationTag.Id, "Contributor proposal v2",
                "Published by request A");
            revisedValues.Entrances.Single().Id = (await service.GetAuthoringContextAsync(tenant.CaveId,
                default)).Cave.Entrances.Single().Id;
            versionTwoId = await service.AddVersionAsync(requestId, revisedValues,
                againstCurrent: true, expectedBaseRevisionId: revisionB,
                expectedProposalVersionId: versionOneId, default);
            Assert.False((await service.GetAsync(requestId, default)).Request.IsStale);
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
            await CavePermissions.AuthenticateAsync(reviewer, tenant.AccountId);
            approved = await IntegrationTestServices.For(reviewer).CaveChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer, requestId), "Re-reviewed", default);
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

    private static async Task MakeBaselineStructurallyValidAsync(PostgresTestDatabase database,
        PublishedCaveTestData tenant, string locationQualityTagId)
    {
        await using var db = database.CreateDbContext("valid-baseline", tenant.AccountId);
        var entrance = new Entrance
        {
            Id = $"entry0000{tenant.AccountId[^1]}", CaveId = tenant.CaveId, IsPrimary = true,
            LocationQualityTagId = locationQualityTagId, Location = new Point(-86, 35, 500) { SRID = 4326 }
        };
        db.Entrances.Add(entrance);
        var revision = await db.CaveRevisions.SingleAsync(row => row.Id == tenant.RevisionId);
        var snapshot = CaveSnapshotJson.Deserialize(revision.SnapshotJson, 1) with
        {
            Entrances = [new CaveEntranceSnapshotV1
            {
                Id = entrance.Id, IsPrimary = true, Latitude = 35, Longitude = -86, Elevation = 500,
                LocationQualityTagId = locationQualityTagId, LocationQualityNameAtRevision = "Survey Grade"
            }]
        };
        revision.SnapshotJson = CaveSnapshotJson.Serialize(snapshot);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ProposalVersionsAreImmutableAndApprovalPublishesExactlyOneLinkedRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ProposalVersionsAreImmutableAndApprovalPublishesExactlyOneLinkedRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
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
            secondVersionId = await repository.AddVersionAsync(requestId, tenant.RevisionId, firstVersionId,
                Proposal(tenant, "Accepted proposal"),
                reviewer: false, againstCurrent: false, default);
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
            await requests.MarkApprovedAsync(requestId, secondVersionId, result, "Reviewed", default);
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
}
