using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Tags.Repositories;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class TagMergeRevisionIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Theory]
    [InlineData(CaveRevisionSource.UserSubmission)]
    [InlineData(CaveRevisionSource.SystemBaseline)]
    [InlineData(CaveRevisionSource.System)]
    public async Task BulkPublisherRejectsSourcesWithoutSupportedProvenance(CaveRevisionSource source)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(BulkPublisherRejectsSourcesWithoutSupportedProvenance)}_{source}");
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await Assert.ThrowsAsync<ArgumentException>(() => new CaveBulkRevisionRepository(db, db.RequestUser)
            .PublishAsync(new Dictionary<string, CavePublishedSnapshotV1>(),
                new Dictionary<string, CavePublishedSnapshotV1>(),
                new Dictionary<string, string?>(),
                new Dictionary<string, CaveRevisionOperation>(), source));
    }

    [Fact]
    public async Task StableIdMergePublishesManagerEditAndPreservesHistoricalSnapshot()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(StableIdMergePublishesManagerEditAndPreservesHistoricalSnapshot));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Source Cricket", "biology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Destination Cricket", "biology00b");
        var previousRevisionId = await AttachBiologyAndPublishAsync(database, cave, source.Id);
        DateTime biologyModifiedBefore;
        await using (var before = database.CreateDbContext("before-audit", cave.AccountId))
            biologyModifiedBefore = (await before.BiologyTags.SingleAsync()).ModifiedOn ?? DateTime.MinValue;

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
            await Repository(db).ExecuteAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(destination.Id, (await verify.BiologyTags.SingleAsync()).TagTypeId);
        var currentId = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
        Assert.NotEqual(previousRevisionId, currentId);
        var current = await verify.CaveRevisions.SingleAsync(revision => revision.Id == currentId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, current.Source);
        Assert.Equal(CaveRevisionOperation.Update, current.Operation);
        Assert.Equal(previousRevisionId, current.PreviousRevisionId);
        var previousSnapshot = CaveSnapshotJson.Deserialize((await verify.CaveRevisions
            .SingleAsync(revision => revision.Id == previousRevisionId)).SnapshotJson, 1);
        var currentSnapshot = CaveSnapshotJson.Deserialize(current.SnapshotJson, 1);
        Assert.Contains(previousSnapshot.Tags, tag => tag.TagTypeId == source.Id &&
            tag.NameAtRevision == "Source Cricket");
        Assert.Contains(currentSnapshot.Tags, tag => tag.TagTypeId == destination.Id);
        var rewritten = await verify.BiologyTags.SingleAsync();
        Assert.Equal("manager", rewritten.ModifiedByUserId);
        Assert.True(rewritten.ModifiedOn > biologyModifiedBefore);
        Assert.Equal("manager", current.CreatedByUserId);
    }

    [Fact]
    public async Task SameSourceOnMultipleCavesPublishesOneRevisionPerAffectedCaveOnly()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(SameSourceOnMultipleCavesPublishesOneRevisionPerAffectedCaveOnly));
        var first = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var secondCave = await CaveTestDataFactory.AddCaveAsync(database, first, "cave00000b", "Second", 2);
        var second = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, secondCave, "revision0b");
        var unaffectedCave = await CaveTestDataFactory.AddCaveAsync(database, first, "cave00000c", "Unaffected", 3);
        var unaffected = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, unaffectedCave, "revision0c");
        var source = await ReferenceTestData.AddTagAsync(database, first.AccountId,
            TagTypeKeyConstant.Biology, "Shared Source", "biology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, first.AccountId,
            TagTypeKeyConstant.Biology, "Shared Destination", "biology00b");
        var firstBefore = await AttachBiologyAndPublishAsync(database, first, source.Id);
        var secondBefore = await AttachBiologyAndPublishAsync(database, second, source.Id);

        await using (var db = database.CreateDbContext("manager", first.AccountId))
            await Repository(db).ExecuteAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", first.AccountId);
        var pointers = await verify.Caves.IgnoreQueryFilters()
            .Where(cave => new[] { first.CaveId, second.CaveId, unaffected.CaveId }.Contains(cave.Id))
            .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId);
        Assert.NotEqual(firstBefore, pointers[first.CaveId]);
        Assert.NotEqual(secondBefore, pointers[second.CaveId]);
        Assert.Equal(unaffected.RevisionId, pointers[unaffected.CaveId]);
        Assert.Equal(2, await verify.CaveRevisions.CountAsync(revision =>
            revision.Source == CaveRevisionSource.ManagerEdit &&
            (revision.PreviousRevisionId == firstBefore || revision.PreviousRevisionId == secondBefore)));
    }

    [Fact]
    public async Task CrossKeyMergeIsRejectedBeforeRelationshipOrRevisionMutation()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CrossKeyMergeIsRejectedBeforeRelationshipOrRevisionMutation));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Biology Source", "biology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.People, "People Destination", "people000a");
        var before = await AttachBiologyAndPublishAsync(database, cave, source.Id);

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
            await Assert.ThrowsAsync<ApiException>(() => Repository(db).ExecuteAsync([source.Id], destination.Id));

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(source.Id, (await verify.BiologyTags.SingleAsync()).TagTypeId);
        Assert.Equal(before, await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync());
        Assert.Empty(await verify.CaveRevisions.Where(revision => revision.PreviousRevisionId == before).ToListAsync());
    }

    [Fact]
    public async Task UnsupportedSameKeyMergeIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(UnsupportedSameKeyMergeIsRejected));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Trip, "Trip Source", "triptag00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Trip, "Trip Destination", "triptag00b");

        await using var db = database.CreateDbContext("manager", cave.AccountId);
        await Assert.ThrowsAsync<ApiException>(() => Repository(db).ExecuteAsync([source.Id], destination.Id));
    }

    [Fact]
    public async Task LateRevisionPublicationFailureRollsBackRelationshipAndPointer()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LateRevisionPublicationFailureRollsBackRelationshipAndPointer));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Rollback Source", "biology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Rollback Destination", "biology00b");
        var before = await AttachBiologyAndPublishAsync(database, cave, source.Id);
        var fault = new FailAtRevisionPublicationInterceptor();

        await using (var db = database.CreateDbContext("manager", cave.AccountId, fault))
            await Assert.ThrowsAsync<DbUpdateException>(
                () => Repository(db).ExecuteAsync([source.Id], destination.Id));

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(source.Id, (await verify.BiologyTags.SingleAsync()).TagTypeId);
        Assert.Equal(before, await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync());
        Assert.Empty(await verify.CaveRevisions.Where(revision => revision.PreviousRevisionId == before).ToListAsync());
    }

    [Fact]
    public async Task AffectingMergeMakesPendingProposalStaleWithoutChangingItsBaseVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(AffectingMergeMakesPendingProposalStaleWithoutChangingItsBaseVersion));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Stale Source", "biology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Stale Destination", "biology00b");
        var proposalBase = await AttachBiologyAndPublishAsync(database, cave, source.Id);
        cave = cave with { RevisionId = proposalBase };
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");
        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId,
                CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Pending after merge"),
                proposalBase, default);
            versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(contributor.Db, requestId);
        }

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
            await Repository(db).ExecuteAsync([source.Id], destination.Id);

        await using (var reviewer = await CaveTestActor.CreateAsync(database, cave.AccountId, "reviewer"))
        {
            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            Assert.True(detail.Request.IsStale);
            await Assert.ThrowsAsync<CaveRevisionConflictException>(
                () => reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default));
        }
        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(proposalBase, await verify.CaveProposalVersions.Where(version => version.Id == versionId)
            .Select(version => version.BaseRevisionId).SingleAsync());
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);
    }

    [Fact]
    public async Task EntranceOwnedMergePublishesRevisionForOwningCave()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(EntranceOwnedMergePublishesRevisionForOwningCave));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Open", "status000a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Restricted", "status000b");
        await EntranceTestData.AddEntranceAsync(database, cave, "entrance0a",
            locationQualityTagId: quality.Id, entranceStatusTagId: source.Id);
        var before = await PublishCurrentSnapshotAsync(database, cave);

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
            await Repository(db).ExecuteAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(destination.Id, (await verify.EntranceStatusTags.SingleAsync()).TagTypeId);
        Assert.NotEqual(before, await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync());
    }

    [Fact]
    public async Task FileOwnedMergePublishesRevisionContainingDestinationStableId()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(FileOwnedMergePublishesRevisionContainingDestinationStableId));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Source File Type", "filetype0a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Destination File Type", "filetype0b");
        await using (var seed = database.CreateDbContext("file", cave.AccountId))
        {
            seed.Files.Add(new File
            {
                Id = "cavefile0a", AccountId = cave.AccountId, CaveId = cave.CaveId,
                FileName = "map.pdf", DisplayName = "map", FileTypeTagId = source.Id
            });
            await seed.SaveChangesAsync();
        }
        var before = await PublishCurrentSnapshotAsync(database, cave);

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
            await Repository(db).ExecuteAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(destination.Id, (await verify.Files.SingleAsync()).FileTypeTagId);
        Assert.Equal("manager", (await verify.Files.SingleAsync()).ModifiedByUserId);
        var revisionId = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
        Assert.NotEqual(before, revisionId);
        var snapshot = CaveSnapshotJson.Deserialize((await verify.CaveRevisions
            .SingleAsync(revision => revision.Id == revisionId)).SnapshotJson, 1);
        Assert.Equal(destination.Id, Assert.Single(snapshot.Files).FileTypeTagId);
    }

    [Fact]
    public async Task LocationQualityMergeStampsEntranceAuditActor()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(LocationQualityMergeStampsEntranceAuditActor));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Estimated", "locqual00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Surveyed", "locqual00b");
        await EntranceTestData.AddEntranceAsync(database, cave, "entrance0a", locationQualityTagId: source.Id);
        await PublishCurrentSnapshotAsync(database, cave);

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
            await Repository(db).ExecuteAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var entrance = await verify.Entrances.SingleAsync();
        Assert.Equal(destination.Id, entrance.LocationQualityTagId);
        Assert.Equal("manager", entrance.ModifiedByUserId);
        Assert.NotNull(entrance.ModifiedOn);
    }

    [Fact]
    public async Task LegacyCaveMergeCreatesBaselineThenManagerEdit()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(LegacyCaveMergeCreatesBaselineThenManagerEdit));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Legacy Source", "biology00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Legacy Destination", "biology00b");
        await using (var seed = database.CreateDbContext("legacy", cave.AccountId))
        {
            seed.BiologyTags.Add(new BiologyTag
                { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = source.Id });
            (await seed.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId)).CurrentRevisionId = null;
            await seed.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext("manager", cave.AccountId))
            await Repository(db).ExecuteAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var pointer = await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync();
        var managerEdit = await verify.CaveRevisions.SingleAsync(revision => revision.Id == pointer);
        var baseline = await verify.CaveRevisions.SingleAsync(revision => revision.Id == managerEdit.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.SystemBaseline, baseline.Source);
        Assert.Contains(CaveSnapshotJson.Deserialize(baseline.SnapshotJson, 1).Tags,
            tag => tag.TagTypeId == source.Id);
        Assert.Equal(CaveRevisionSource.ManagerEdit, managerEdit.Source);
        Assert.Contains(CaveSnapshotJson.Deserialize(managerEdit.SnapshotJson, 1).Tags,
            tag => tag.TagTypeId == destination.Id);
    }

    private static TagTypeMergeExecutionRepository Repository(Planarian.Model.Database.PlanarianDbContext db)
    {
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);
        return new TagTypeMergeExecutionRepository(db, db.RequestUser,
            new TagReferenceLockRepository(db, db.RequestUser), snapshots,
            new CaveBulkRevisionRepository(db, db.RequestUser));
    }

    private static async Task<string> AttachBiologyAndPublishAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, string tagId)
    {
        await using var db = database.CreateDbContext("attach", cave.AccountId);
        db.BiologyTags.Add(new BiologyTag
            { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = tagId });
        await db.SaveChangesAsync();
        return await PublishCurrentSnapshotAsync(database, cave, db);
    }

    private static async Task<string> PublishCurrentSnapshotAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, Planarian.Model.Database.PlanarianDbContext? existingDb = null)
    {
        await using var ownedDb = existingDb is null ? database.CreateDbContext("publish-current", cave.AccountId) : null;
        var db = existingDb ?? ownedDb!;
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(cave.CaveId);
        var current = await db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        var revision = new CaveRevision
        {
            Id = IdGenerator.Generate(), AccountId = cave.AccountId, CaveId = cave.CaveId,
            PreviousRevisionId = current.CurrentRevisionId, Source = CaveRevisionSource.ManagerEdit,
            Operation = CaveRevisionOperation.Update, SnapshotSchemaVersion = 1,
            SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
        };
        db.CaveRevisions.Add(revision);
        current.CurrentRevisionId = revision.Id;
        await db.SaveChangesAsync();
        return revision.Id;
    }
}
