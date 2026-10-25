using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveBulkRevisionSemanticParityIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task BulkPublicationUsesPublishedRevisionSnapshotWhenReferenceMetadataDrifted()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(BulkPublicationUsesPublishedRevisionSnapshotWhenReferenceMetadataDrifted));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var reporter = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.People, "Alice Reporter", "people000a");

        await using (var attach = database.CreateDbContext("attach", cave.AccountId))
        {
            attach.CaveReportedByNameTags.Add(new CaveReportedByNameTag
            {
                Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = reporter.Id
            });
            await attach.SaveChangesAsync();
        }

        var publishedRevisionId = await PublishCurrentSnapshotAsync(database, cave);

        await using (var rename = database.CreateDbContext("rename", cave.AccountId))
        {
            (await rename.TagTypes.SingleAsync(tag => tag.Id == reporter.Id)).Name = "Alice Renamed";
            await rename.SaveChangesAsync();
        }

        await using (var bulk = database.CreateDbContext("bulk", cave.AccountId))
        {
            var snapshots = new CavePublishedSnapshotRepository(bulk, bulk.RequestUser);
            var live = await snapshots.BuildAsync(cave.CaveId);
            Assert.Contains(live.Tags, tag => tag.TagTypeId == reporter.Id && tag.NameAtRevision == "Alice Renamed");

            await using var transaction = await bulk.Database.BeginTransactionAsync();
            await new CaveBulkRevisionRepository(bulk, bulk.RequestUser).PublishAsync(
                new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal) { [cave.CaveId] = live },
                new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal) { [cave.CaveId] = live },
                new Dictionary<string, string?>(StringComparer.Ordinal) { [cave.CaveId] = publishedRevisionId },
                new Dictionary<string, CaveRevisionOperation>(StringComparer.Ordinal)
                    { [cave.CaveId] = CaveRevisionOperation.Update },
                CaveRevisionSource.ManagerEdit);
            await transaction.CommitAsync();
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var currentRevisionId = await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId)
            .SingleAsync();
        Assert.NotEqual(publishedRevisionId, currentRevisionId);

        var previous = await verify.CaveRevisions.SingleAsync(revision => revision.Id == publishedRevisionId);
        var current = await verify.CaveRevisions.SingleAsync(revision => revision.Id == currentRevisionId);
        Assert.Equal(publishedRevisionId, current.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, current.Source);

        var diff = new CaveRevisionDiffService().Compare(
            CaveSnapshotJson.Deserialize(previous.SnapshotJson, previous.SnapshotSchemaVersion),
            CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion));
        Assert.Contains(diff.ReferenceMetadataChanges, change =>
            change.StableId == reporter.Id &&
            change.PreviousValue == "Alice Reporter" &&
            change.CurrentValue == "Alice Renamed");
    }

    [Fact]
    public async Task BulkPublicationUsesSemanticEqualityRatherThanSerializedCollectionOrder()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(BulkPublicationUsesSemanticEqualityRatherThanSerializedCollectionOrder));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var first = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.People, "Alice", "people000a");
        var second = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.People, "Bob", "people000b");
        string orderedDifferentlyRevisionId;

        await using (var seed = database.CreateDbContext("seed", cave.AccountId))
        {
            seed.CaveReportedByNameTags.AddRange(
                new CaveReportedByNameTag
                    { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = first.Id },
                new CaveReportedByNameTag
                    { Id = IdGenerator.Generate(), CaveId = cave.CaveId, TagTypeId = second.Id });
            await seed.SaveChangesAsync();

            var live = await new CavePublishedSnapshotRepository(seed, seed.RequestUser).BuildAsync(cave.CaveId);
            var reordered = live with { Tags = live.Tags.Reverse().ToList() };
            Assert.NotEqual(CaveSnapshotJson.Serialize(live), CaveSnapshotJson.Serialize(reordered));
            Assert.True(new CaveRevisionDiffService().IsSemanticEqual(reordered, live));

            orderedDifferentlyRevisionId = IdGenerator.Generate();
            seed.CaveRevisions.Add(new CaveRevision
            {
                Id = orderedDifferentlyRevisionId,
                AccountId = cave.AccountId,
                CaveId = cave.CaveId,
                PreviousRevisionId = cave.RevisionId,
                Source = CaveRevisionSource.ManagerEdit,
                Operation = CaveRevisionOperation.Update,
                SnapshotSchemaVersion = 1,
                SnapshotJson = CaveSnapshotJson.Serialize(reordered)
            });
            (await seed.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId)).CurrentRevisionId =
                orderedDifferentlyRevisionId;
            await seed.SaveChangesAsync();
        }

        await using (var bulk = database.CreateDbContext("bulk", cave.AccountId))
        {
            var live = await new CavePublishedSnapshotRepository(bulk, bulk.RequestUser).BuildAsync(cave.CaveId);
            await using var transaction = await bulk.Database.BeginTransactionAsync();
            await new CaveBulkRevisionRepository(bulk, bulk.RequestUser).PublishAsync(
                new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal) { [cave.CaveId] = live },
                new Dictionary<string, CavePublishedSnapshotV1>(StringComparer.Ordinal) { [cave.CaveId] = live },
                new Dictionary<string, string?>(StringComparer.Ordinal)
                    { [cave.CaveId] = orderedDifferentlyRevisionId },
                new Dictionary<string, CaveRevisionOperation>(StringComparer.Ordinal)
                    { [cave.CaveId] = CaveRevisionOperation.Update },
                CaveRevisionSource.ManagerEdit);
            await transaction.CommitAsync();
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(orderedDifferentlyRevisionId, await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId)
            .SingleAsync());
        Assert.Empty(await verify.CaveRevisions
            .Where(revision => revision.PreviousRevisionId == orderedDifferentlyRevisionId)
            .ToListAsync());
    }

    private static async Task<string> PublishCurrentSnapshotAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave)
    {
        await using var db = database.CreateDbContext("publish-current", cave.AccountId);
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(cave.CaveId);
        var current = await db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        var revision = new CaveRevision
        {
            Id = IdGenerator.Generate(),
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            PreviousRevisionId = current.CurrentRevisionId,
            Source = CaveRevisionSource.ManagerEdit,
            Operation = CaveRevisionOperation.Update,
            SnapshotSchemaVersion = 1,
            SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
        };
        db.CaveRevisions.Add(revision);
        current.CurrentRevisionId = revision.Id;
        await db.SaveChangesAsync();
        return revision.Id;
    }
}
