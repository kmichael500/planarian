using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Tags;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveChangeRequestPeopleTagIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task TypedExistingPeopleNameBindsIdentityInImmutableProposal()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TypedExistingPeopleNameBindsIdentityInImmutableProposal));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var person = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Élodie Person", "people000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var values = PublishableValues(tenant, quality.Id, "Identity-bound proposal");
            values.CartographerNameTagIds = ["élodie person"];
            await contributor.ChangeRequests.CreateAsync(tenant.CaveId, values, tenant.RevisionId, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var row = Assert.Single(await verify.CaveProposalVersions.ToListAsync());
        var proposal = CaveProposalJson.Deserialize(row.ProposalJson, row.SchemaVersion);
        var reference = Assert.Single(proposal.Tags.Where(tag => tag.Role == SnapshotTagRole.Cartographer));
        Assert.Equal(person.Id, reference.TagTypeId);
        Assert.Equal("Élodie Person", reference.NameAtRevision);
        Assert.DoesNotContain(proposal.NewPeopleTagIntents,
            intent => TagNameMatchPolicy.IdentityComparer.Equals(intent.Name, "élodie person"));
    }

    [Fact]
    public async Task RenameAfterSubmissionPreservesProposalIdentityAndPublishesCurrentLabel()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RenameAfterSubmissionPreservesProposalIdentityAndPublishesCurrentLabel));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var person = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Existing Person", "people000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var values = PublishableValues(tenant, quality.Id, "Rename-stable proposal");
            values.CartographerNameTagIds = ["Existing Person"];
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId, values,
                tenant.RevisionId, default);
        }
        await using (var rename = database.CreateDbContext("rename-person", tenant.AccountId))
        {
            (await rename.TagTypes.SingleAsync(tag => tag.Id == person.Id)).Name = "Renamed Person";
            await rename.SaveChangesAsync();
        }
        string revisionId;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            revisionId = (await reviewer.ChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer.Db, requestId), null, default)).PublishedRevisionId!;
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal(person.Id, Assert.Single(await verify.CartographerNameTags
            .Where(tag => tag.CaveId == tenant.CaveId).ToListAsync()).TagTypeId);
        Assert.False(await verify.TagTypes.AnyAsync(tag => tag.Key == TagTypeKeyConstant.People &&
            tag.Name == "Existing Person"));
        var storedVersion = await verify.CaveProposalVersions.SingleAsync();
        var proposalReference = Assert.Single(CaveProposalJson.Deserialize(storedVersion.ProposalJson,
            storedVersion.SchemaVersion).Tags.Where(tag => tag.Role == SnapshotTagRole.Cartographer));
        Assert.Equal(person.Id, proposalReference.TagTypeId);
        Assert.Equal("Existing Person", proposalReference.NameAtRevision);
        var accepted = await verify.CaveRevisions.SingleAsync(revision => revision.Id == revisionId);
        var acceptedReference = Assert.Single(CaveSnapshotJson.Deserialize(accepted.SnapshotJson,
            accepted.SnapshotSchemaVersion).Tags.Where(tag => tag.Role == SnapshotTagRole.Cartographer));
        Assert.Equal(person.Id, acceptedReference.TagTypeId);
        Assert.Equal("Renamed Person", acceptedReference.NameAtRevision);
    }

    [Fact]
    public async Task ExistingAssociationRetypedByNameIsRejectedAsSemanticNoOp()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ExistingAssociationRetypedByNameIsRejectedAsSemanticNoOp));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var person = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "David Parr", "people000a");
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await EntranceTestData.AddEntranceAsync(database, tenant, "entrance0a",
            locationQualityTagId: quality.Id);
        string revisionId;
        await using (var seed = database.CreateDbContext("seed-association", tenant.AccountId))
        {
            seed.CartographerNameTags.Add(new CartographerNameTag
                { Id = "cartlink0a", CaveId = tenant.CaveId, TagTypeId = person.Id });
            await seed.SaveChangesAsync();
            revisionId = (await new CaveMutationRepository(seed, seed.RequestUser,
                    new CavePublishedSnapshotRepository(seed, seed.RequestUser))
                .PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit,
                    CaveRevisionOperation.Update, _ => { })).RevisionId!;
        }
        tenant = tenant with { RevisionId = revisionId };
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var cave = await contributor.Services.Caves.GetCave(tenant.CaveId);
            var values = ValuesFromCave(cave!);
            values.CartographerNameTagIds = ["David Parr"];
            var exception = await Assert.ThrowsAsync<ApiException>(() => contributor.ChangeRequests.CreateAsync(
                tenant.CaveId, values, revisionId, default));
            Assert.Equal(400, exception.StatusCode);
            Assert.Equal("The proposal does not contain any changes.", exception.Message);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Empty(await verify.CaveChangeRequests.ToListAsync());
        Assert.Empty(await verify.CaveProposalVersions.ToListAsync());
        Assert.Single(await verify.TagTypes.Where(tag => tag.Key == TagTypeKeyConstant.People).ToListAsync());
    }

    [Fact]
    public async Task NewPeopleCaseVariantsCollapseToOneIntentAndOneTag()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NewPeopleCaseVariantsCollapseToOneIntentAndOneTag));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var values = PublishableValues(tenant, quality.Id, "Case-folded People proposal");
            values.CartographerNameTagIds = ["New Person", "new person"];
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId, values,
                tenant.RevisionId, default);
            var row = await contributor.Db.CaveProposalVersions.SingleAsync();
            var proposal = CaveProposalJson.Deserialize(row.ProposalJson, row.SchemaVersion);
            Assert.Equal("New Person", Assert.Single(proposal.NewPeopleTagIntents).Name);
        }
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            await reviewer.ChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer.Db, requestId), null, default);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var created = Assert.Single(await verify.TagTypes.Where(tag => tag.AccountId == tenant.AccountId &&
            tag.Key == TagTypeKeyConstant.People).ToListAsync());
        Assert.Equal("New Person", created.Name);
        Assert.Equal(created.Id, Assert.Single(await verify.CartographerNameTags
            .Where(tag => tag.CaveId == tenant.CaveId).ToListAsync()).TagTypeId);
    }

    [Fact]
    public async Task DeletedExistingReportedByReferenceCannotDegradeIntoPeopleCreationIntent()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DeletedExistingReportedByReferenceCannotDegradeIntoPeopleCreationIntent));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var person = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Deleted Reporter", "people000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var values = PublishableValues(tenant, quality.Id, "Must not publish");
            values.ReportedByNameTagIds = [person.Id];
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId, values,
                tenant.RevisionId, default);
        }
        await using (var remove = database.CreateDbContext("remove-person", tenant.AccountId))
        {
            remove.TagTypes.Remove(await remove.TagTypes.SingleAsync(tag => tag.Id == person.Id));
            await remove.SaveChangesAsync();
        }

        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
        {
            var versionId = await CurrentVersionAsync(reviewer.Db, requestId);
            var exception = await Assert.ThrowsAsync<ApiException>(() => reviewer.ChangeRequests.ApproveAsync(
                requestId, versionId, null, default));
            Assert.Equal(400, exception.StatusCode);
            Assert.Contains("recorded People tag", exception.Message);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal("Cave A", (await verify.Caves.IgnoreQueryFilters()
            .SingleAsync(cave => cave.Id == tenant.CaveId)).Name);
        var request = await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId);
        Assert.Equal(CaveChangeRequestStatus.Pending, request.Status);
        Assert.Null(request.ApprovedRevisionId);
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
        Assert.False(await verify.TagTypes.AnyAsync(tag => tag.Name == person.Id));
    }

    [Fact]
    public async Task InlinePeopleNamesReuseExistingIdentityAndOneNewIdentityAcrossRoles()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(InlinePeopleNamesReuseExistingIdentityAndOneNewIdentityAcrossRoles));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var existing = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Existing Person", "people000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var values = PublishableValues(tenant, quality.Id, "People resolution");
            values.CartographerNameTagIds = ["Existing Person", "Shared New Person"];
            values.ReportedByNameTagIds = ["existing person", "Shared New Person"];
            values.Entrances.Single().ReportedByNameTagIds = ["Shared New Person"];
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId, values,
                tenant.RevisionId, default);
        }
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            await reviewer.ChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer.Db, requestId), null, default);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var created = Assert.Single(await verify.TagTypes.Where(tag => tag.AccountId == tenant.AccountId &&
            tag.Key == TagTypeKeyConstant.People && tag.Name == "Shared New Person").ToListAsync());
        Assert.Contains(await verify.CartographerNameTags.Where(tag => tag.CaveId == tenant.CaveId).ToListAsync(),
            tag => tag.TagTypeId == existing.Id);
        Assert.Contains(await verify.CaveReportedByNameTags.Where(tag => tag.CaveId == tenant.CaveId).ToListAsync(),
            tag => tag.TagTypeId == existing.Id);
        Assert.Contains(await verify.CartographerNameTags.Where(tag => tag.CaveId == tenant.CaveId).ToListAsync(),
            tag => tag.TagTypeId == created.Id);
        Assert.Contains(await verify.CaveReportedByNameTags.Where(tag => tag.CaveId == tenant.CaveId).ToListAsync(),
            tag => tag.TagTypeId == created.Id);
        Assert.Contains(await verify.EntranceReportedByNameTags.Where(tag => tag.Entrance.CaveId == tenant.CaveId)
            .ToListAsync(), tag => tag.TagTypeId == created.Id);
    }

    [Fact]
    public async Task NewPeopleIntentMatchingExistingTagIdRemainsANameCreationIntent()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(NewPeopleIntentMatchingExistingTagIdRemainsANameCreationIntent));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var existing = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.People, "Different Existing Name", "people000a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");
        string requestId;

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var proposal = PublishableProposal(tenant, quality.Id, "Typed creation intent") with
            {
                NewPeopleTagIntents =
                [new ProposalPeopleTagIntent(SnapshotTagRole.Cartographer, existing.Id)]
            };
            requestId = await new CaveChangeRequestRepository(contributor.Db, contributor.Db.RequestUser)
                .CreateAsync(tenant.CaveId, tenant.RevisionId, proposal, default);
        }
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            await reviewer.ChangeRequests.ApproveAsync(requestId,
                await CurrentVersionAsync(reviewer.Db, requestId), null, default);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var created = Assert.Single(await verify.TagTypes.Where(tag => tag.AccountId == tenant.AccountId &&
            tag.Key == TagTypeKeyConstant.People && tag.Name == existing.Id).ToListAsync());
        var relation = Assert.Single(await verify.CartographerNameTags.Where(tag =>
            tag.CaveId == tenant.CaveId).ToListAsync());
        Assert.Equal(created.Id, relation.TagTypeId);
        Assert.NotEqual(existing.Id, relation.TagTypeId);
    }

    [Fact]
    public async Task PeopleNameLengthIsRejectedBeforeProposalOrDirectMutationPersistence()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PeopleNameLengthIsRejectedBeforeProposalOrDirectMutationPersistence));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            var accepted = PublishableValues(tenant, quality.Id, "Maximum People name");
            accepted.CartographerNameTagIds = [new string('a', PropertyLength.Name)];
            await contributor.ChangeRequests.CreateAsync(tenant.CaveId, accepted, tenant.RevisionId, default);

            var rejected = PublishableValues(tenant, quality.Id, "Oversized People name");
            rejected.CartographerNameTagIds = [new string('b', PropertyLength.Name + 1)];
            var exception = await Assert.ThrowsAsync<ApiException>(() => contributor.ChangeRequests.CreateAsync(
                tenant.CaveId, rejected, tenant.RevisionId, default));
            Assert.Equal(400, exception.StatusCode);
        }

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var rejected = PublishableValues(tenant, quality.Id, "Direct oversized People name");
            rejected.CartographerNameTagIds = [new string('c', PropertyLength.Name + 1)];
            var exception = await Assert.ThrowsAsync<ApiException>(() => manager.Services.Caves.AddCave(
                rejected, default));
            Assert.Equal(400, exception.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Single(await verify.CaveChangeRequests.ToListAsync());
        Assert.Single(await verify.CaveProposalVersions.ToListAsync());
        Assert.Single(await verify.CaveRevisions.Where(revision => revision.CaveId == tenant.CaveId).ToListAsync());
    }
}
