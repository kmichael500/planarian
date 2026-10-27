using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Tags.Repositories;
using Xunit;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class TagMergeKitchenSinkIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    public static TheoryData<TagRelationshipPath, string> CollectionPaths => new()
    {
        { TagRelationshipPath.Geology, TagTypeKeyConstant.Geology },
        { TagRelationshipPath.EntranceStatus, TagTypeKeyConstant.EntranceStatus },
        { TagRelationshipPath.FieldIndication, TagTypeKeyConstant.FieldIndication },
        { TagRelationshipPath.EntranceHydrology, TagTypeKeyConstant.EntranceHydrology },
        { TagRelationshipPath.Cartographer, TagTypeKeyConstant.People },
        { TagRelationshipPath.CaveReportedBy, TagTypeKeyConstant.People },
        { TagRelationshipPath.EntranceReportedBy, TagTypeKeyConstant.People },
        { TagRelationshipPath.Biology, TagTypeKeyConstant.Biology },
        { TagRelationshipPath.Archeology, TagTypeKeyConstant.Archeology },
        { TagRelationshipPath.MapStatus, TagTypeKeyConstant.MapStatus },
        { TagRelationshipPath.CaveOther, TagTypeKeyConstant.CaveOther },
        { TagRelationshipPath.EntranceOther, TagTypeKeyConstant.CaveOther },
        { TagRelationshipPath.GeologicAge, TagTypeKeyConstant.GeologicAge },
        { TagRelationshipPath.PhysiographicProvince, TagTypeKeyConstant.PhysiographicProvince }
    };

    [Theory]
    [MemberData(nameof(CollectionPaths))]
    public async Task CollectionMergePreservesDestinationOnlyParentAndCollapsesDuplicateWithoutDeletingDestination(
        TagRelationshipPath path, string key)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(CollectionMergePreservesDestinationOnlyParentAndCollapsesDuplicateWithoutDeletingDestination)}_{path}");
        var first = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var secondCave = await CaveTestDataFactory.AddCaveAsync(database, first, "cave00000b", "Destination only", 2);
        var second = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, secondCave, "revision0b");
        var source = await ReferenceTestData.AddTagAsync(database, first.AccountId, key, "Source", "source000a");
        var destination = await ReferenceTestData.AddTagAsync(database, first.AccountId, key, "Destination", "dest00000a");

        var firstEntranceId = NeedsEntrance(path)
            ? await AddEntranceAsync(database, first, "entrance0a")
            : null;
        var secondEntranceId = NeedsEntrance(path)
            ? await AddEntranceAsync(database, second, "entrance0b")
            : null;

        var sourceRowId = "sourceRelA";
        var firstDestinationRowId = "destRel00A";
        var secondDestinationRowId = "destRel00B";
        await AddCollectionReferenceAsync(database, first, firstEntranceId, path, source.Id, sourceRowId);
        await AddCollectionReferenceAsync(database, first, firstEntranceId, path, destination.Id, firstDestinationRowId);
        await AddCollectionReferenceAsync(database, second, secondEntranceId, path, destination.Id, secondDestinationRowId);
        var firstBefore = await PublishCurrentSnapshotAsync(database, first);
        var secondBefore = await PublishCurrentSnapshotAsync(database, second);

        await using (var admin = database.CreateDbContext("manager", first.AccountId))
            await Repository(admin).MergeAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", first.AccountId);
        var firstRows = await CollectionRowsAsync(verify, first.CaveId, path);
        var secondRows = await CollectionRowsAsync(verify, second.CaveId, path);

        Assert.DoesNotContain(firstRows, row => row.TagTypeId == source.Id);
        var survivingFirst = Assert.Single(firstRows.Where(row => row.TagTypeId == destination.Id));
        Assert.Equal(firstDestinationRowId, survivingFirst.Id);
        var survivingSecond = Assert.Single(secondRows.Where(row => row.TagTypeId == destination.Id));
        Assert.Equal(secondDestinationRowId, survivingSecond.Id);

        var firstCurrent = await CurrentRevisionAsync(verify, first.CaveId);
        var secondCurrent = await CurrentRevisionAsync(verify, second.CaveId);
        Assert.NotEqual(firstBefore, firstCurrent);
        Assert.Equal(secondBefore, secondCurrent);
    }

    [Fact]
    public async Task FileTypeMergePreservesEveryFileRowAndOnlyRewritesSourceFileType()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FileTypeMergePreservesEveryFileRowAndOnlyRewritesSourceFileType));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Source file", "filetag00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Destination file", "filetag00b");

        await using (var seed = database.CreateDbContext("file-seed", cave.AccountId))
        {
            seed.Files.AddRange(
                new File { Id = "file00000a", AccountId = cave.AccountId, CaveId = cave.CaveId,
                    Name = "source-file", Extension = ".pdf", FileTypeTagId = source.Id },
                new File { Id = "file00000b", AccountId = cave.AccountId, CaveId = cave.CaveId,
                    Name = "destination-file", Extension = ".jpg", FileTypeTagId = destination.Id });
            await seed.SaveChangesAsync();
        }
        await PublishCurrentSnapshotAsync(database, cave);

        await using (var admin = database.CreateDbContext("manager", cave.AccountId))
            await Repository(admin).MergeAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var files = await verify.Files.OrderBy(file => file.Id).ToListAsync();
        Assert.Equal(2, files.Count);
        Assert.Equal(["file00000a", "file00000b"], files.Select(file => file.Id));
        Assert.All(files, file => Assert.Equal(destination.Id, file.FileTypeTagId));
        Assert.Equal("source-file", files[0].Name);
        Assert.Equal("destination-file", files[1].Name);
    }

    [Fact]
    public async Task LocationQualityMergePreservesEveryEntranceRowAndOnlyRewritesReference()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(LocationQualityMergePreservesEveryEntranceRowAndOnlyRewritesReference));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var source = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Estimated", "quality00a");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Surveyed", "quality00b");
        await EntranceTestData.AddEntranceAsync(database, cave, "entrance0a", locationQualityTagId: source.Id);
        await EntranceTestData.AddEntranceAsync(database, cave, "entrance0b", isPrimary: false,
            locationQualityTagId: destination.Id);
        await PublishCurrentSnapshotAsync(database, cave);

        await using (var admin = database.CreateDbContext("manager", cave.AccountId))
            await Repository(admin).MergeAsync([source.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var entrances = await verify.Entrances.OrderBy(entrance => entrance.Id).ToListAsync();
        Assert.Equal(2, entrances.Count);
        Assert.Equal(["entrance0a", "entrance0b"], entrances.Select(entrance => entrance.Id));
        Assert.All(entrances, entrance => Assert.Equal(destination.Id, entrance.LocationQualityTagId));
    }

    [Fact]
    public async Task MultiSourceMergeNormalizesInputAndPreservesUnrelatedDestinationRelationships()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MultiSourceMergeNormalizesInputAndPreservesUnrelatedDestinationRelationships));
        var first = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var secondCave = await CaveTestDataFactory.AddCaveAsync(database, first, "cave00000b", "Second", 2);
        var second = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, secondCave, "revision0b");
        var thirdCave = await CaveTestDataFactory.AddCaveAsync(database, first, "cave00000c", "Destination only", 3);
        var third = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, thirdCave, "revision0c");
        var sourceA = await ReferenceTestData.AddTagAsync(database, first.AccountId,
            TagTypeKeyConstant.Biology, "Source A", "biology00a");
        var sourceB = await ReferenceTestData.AddTagAsync(database, first.AccountId,
            TagTypeKeyConstant.Biology, "Source B", "biology00b");
        var destination = await ReferenceTestData.AddTagAsync(database, first.AccountId,
            TagTypeKeyConstant.Biology, "Destination", "biology00d");

        await using (var seed = database.CreateDbContext("seed", first.AccountId))
        {
            seed.BiologyTags.AddRange(
                new BiologyTag { Id = "bioRel000a", CaveId = first.CaveId, TagTypeId = sourceA.Id },
                new BiologyTag { Id = "bioRel000b", CaveId = second.CaveId, TagTypeId = sourceB.Id },
                new BiologyTag { Id = "bioRel000d", CaveId = second.CaveId, TagTypeId = destination.Id },
                new BiologyTag { Id = "bioRel000e", CaveId = third.CaveId, TagTypeId = destination.Id });
            await seed.SaveChangesAsync();
        }
        var firstBefore = await PublishCurrentSnapshotAsync(database, first);
        var secondBefore = await PublishCurrentSnapshotAsync(database, second);
        var thirdBefore = await PublishCurrentSnapshotAsync(database, third);

        await using (var admin = database.CreateDbContext("manager", first.AccountId))
            await Repository(admin).MergeAsync(
                [sourceA.Id, sourceB.Id, sourceA.Id, destination.Id, " ", sourceB.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", first.AccountId);
        var rows = await verify.BiologyTags.OrderBy(tag => tag.CaveId).ThenBy(tag => tag.Id).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.All(rows, row => Assert.Equal(destination.Id, row.TagTypeId));
        Assert.Contains(rows, row => row.CaveId == first.CaveId && row.Id == "bioRel000a");
        Assert.Contains(rows, row => row.CaveId == second.CaveId && row.Id == "bioRel000d");
        Assert.Contains(rows, row => row.CaveId == third.CaveId && row.Id == "bioRel000e");
        Assert.NotEqual(firstBefore, await CurrentRevisionAsync(verify, first.CaveId));
        Assert.NotEqual(secondBefore, await CurrentRevisionAsync(verify, second.CaveId));
        Assert.Equal(thirdBefore, await CurrentRevisionAsync(verify, third.CaveId));
        Assert.True(await verify.TagTypes.AnyAsync(tag => tag.Id == sourceA.Id));
        Assert.True(await verify.TagTypes.AnyAsync(tag => tag.Id == sourceB.Id));
    }

    [Fact]
    public async Task MultipleSourcesOnSameParentWithoutDestinationCollapseToSingleDestination()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(MultipleSourcesOnSameParentWithoutDestinationCollapseToSingleDestination));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var sourceA = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Source A", "biology00a");
        var sourceB = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Source B", "biology00b");
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Destination", "biology00d");
        await using (var seed = database.CreateDbContext("seed", cave.AccountId))
        {
            seed.BiologyTags.AddRange(
                new BiologyTag { Id = "sourceRelA", CaveId = cave.CaveId, TagTypeId = sourceA.Id },
                new BiologyTag { Id = "sourceRelB", CaveId = cave.CaveId, TagTypeId = sourceB.Id });
            await seed.SaveChangesAsync();
        }
        var before = await PublishCurrentSnapshotAsync(database, cave);

        await using (var admin = database.CreateDbContext("manager", cave.AccountId))
            await Repository(admin).MergeAsync([sourceA.Id, sourceB.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var row = Assert.Single(await verify.BiologyTags.Where(tag => tag.CaveId == cave.CaveId).ToListAsync());
        Assert.Contains(row.Id, new[] { "sourceRelA", "sourceRelB" });
        Assert.Equal(destination.Id, row.TagTypeId);
        var current = await CurrentRevisionAsync(verify, cave.CaveId);
        Assert.NotEqual(before, current);
        var revision = await verify.CaveRevisions.SingleAsync(item => item.Id == current);
        Assert.Equal(before, revision.PreviousRevisionId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DefaultTagMergeMutatesOnlyCurrentAccountRelationships(bool defaultIsSource)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(DefaultTagMergeMutatesOnlyCurrentAccountRelationships)}_{defaultIsSource}");
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var global = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            TagTypeKeyConstant.Biology, "Global", "global0000a", isDefault: true);
        var owned = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            TagTypeKeyConstant.Biology, "Owned", "owned00000a");
        var source = defaultIsSource ? global : owned;
        var destination = defaultIsSource ? owned : global;

        await using (var seed = database.CreateDbContext("seed", accountA.AccountId))
        {
            seed.BiologyTags.Add(new BiologyTag
                { Id = "accountARel", CaveId = accountA.CaveId, TagTypeId = source.Id });
            await seed.SaveChangesAsync();
        }
        await using (var seedB = database.CreateDbContext("seed-b", accountB.AccountId))
        {
            seedB.BiologyTags.Add(new BiologyTag
                { Id = "accountBRel", CaveId = accountB.CaveId, TagTypeId = global.Id });
            await seedB.SaveChangesAsync();
        }
        await PublishCurrentSnapshotAsync(database, accountA);
        var accountBBefore = await PublishCurrentSnapshotAsync(database, accountB);

        await using (var admin = database.CreateDbContext("manager", accountA.AccountId))
            await Repository(admin).MergeAsync([source.Id], destination.Id);

        await using var verifyA = database.CreateDbContext("verify-a", accountA.AccountId);
        Assert.Equal(destination.Id,
            await verifyA.BiologyTags.Where(tag => tag.CaveId == accountA.CaveId)
                .Select(tag => tag.TagTypeId).SingleAsync());
        await using var verifyB = database.CreateDbContext("verify-b", accountB.AccountId);
        Assert.Equal(global.Id,
            await verifyB.BiologyTags.Where(tag => tag.CaveId == accountB.CaveId)
                .Select(tag => tag.TagTypeId).SingleAsync());
        Assert.Equal(accountBBefore, await CurrentRevisionAsync(verifyB, accountB.CaveId));
    }

    [Theory]
    [InlineData(TagTypeKeyConstant.File)]
    [InlineData(TagTypeKeyConstant.LocationQuality)]
    public async Task ScalarDefaultSourceMergeDoesNotTouchOtherAccount(string key)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(ScalarDefaultSourceMergeDoesNotTouchOtherAccount)}_{key}");
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var global = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            key, "Global Source", "global0000a", isDefault: true);
        var destination = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            key, "Owned Destination", "owned00000a");

        if (key == TagTypeKeyConstant.File)
        {
            await using var seedA = database.CreateDbContext("seed-a", accountA.AccountId);
            seedA.Files.Add(new File
            {
                Id = "file00000a", AccountId = accountA.AccountId, CaveId = accountA.CaveId,
                Name = "Account A", Extension = ".pdf", FileTypeTagId = global.Id
            });
            await seedA.SaveChangesAsync();

            await using var seedB = database.CreateDbContext("seed-b", accountB.AccountId);
            seedB.Files.Add(new File
            {
                Id = "file00000b", AccountId = accountB.AccountId, CaveId = accountB.CaveId,
                Name = "Account B", Extension = ".pdf", FileTypeTagId = global.Id
            });
            await seedB.SaveChangesAsync();
        }
        else
        {
            await EntranceTestData.AddEntranceAsync(database, accountA, "entrance0a",
                locationQualityTagId: global.Id);
            await EntranceTestData.AddEntranceAsync(database, accountB, "entrance0b",
                locationQualityTagId: global.Id);
        }

        await PublishCurrentSnapshotAsync(database, accountA);
        var accountBBefore = await PublishCurrentSnapshotAsync(database, accountB);

        await using (var admin = database.CreateDbContext("manager", accountA.AccountId))
            await Repository(admin).MergeAsync([global.Id], destination.Id);

        await using var verifyA = database.CreateDbContext("verify-a", accountA.AccountId);
        await using var verifyB = database.CreateDbContext("verify-b", accountB.AccountId);
        if (key == TagTypeKeyConstant.File)
        {
            Assert.Equal(destination.Id,
                await verifyA.Files.Where(file => file.CaveId == accountA.CaveId)
                    .Select(file => file.FileTypeTagId).SingleAsync());
            Assert.Equal(global.Id,
                await verifyB.Files.Where(file => file.CaveId == accountB.CaveId)
                    .Select(file => file.FileTypeTagId).SingleAsync());
        }
        else
        {
            Assert.Equal(destination.Id,
                await verifyA.Entrances.Where(entrance => entrance.CaveId == accountA.CaveId)
                    .Select(entrance => entrance.LocationQualityTagId).SingleAsync());
            Assert.Equal(global.Id,
                await verifyB.Entrances.Where(entrance => entrance.CaveId == accountB.CaveId)
                    .Select(entrance => entrance.LocationQualityTagId).SingleAsync());
        }
        Assert.Equal(accountBBefore, await CurrentRevisionAsync(verifyB, accountB.CaveId));
    }

    [Fact]
    public async Task DestinationOnlyOrBlankSourceListIsNoOp()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(DestinationOnlyOrBlankSourceListIsNoOp));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var destination = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Destination", "biology00d");
        await using (var seed = database.CreateDbContext("seed", cave.AccountId))
        {
            seed.BiologyTags.Add(new BiologyTag
                { Id = "destRel00a", CaveId = cave.CaveId, TagTypeId = destination.Id });
            await seed.SaveChangesAsync();
        }
        var before = await PublishCurrentSnapshotAsync(database, cave);

        await using (var admin = database.CreateDbContext("manager", cave.AccountId))
            await Repository(admin).MergeAsync([destination.Id, " ", destination.Id], destination.Id);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var row = Assert.Single(await verify.BiologyTags.Where(tag => tag.CaveId == cave.CaveId).ToListAsync());
        Assert.Equal("destRel00a", row.Id);
        Assert.Equal(destination.Id, row.TagTypeId);
        Assert.Equal(before, await CurrentRevisionAsync(verify, cave.CaveId));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForeignAccountTagCannotParticipateInMerge(bool foreignIsDestination)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(ForeignAccountTagCannotParticipateInMerge)}_{foreignIsDestination}");
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var owned = await ReferenceTestData.AddTagAsync(database, accountA.AccountId,
            TagTypeKeyConstant.Biology, "Owned", "owned00000a");
        var foreign = await ReferenceTestData.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.Biology, "Foreign", "foreign000a");
        var source = foreignIsDestination ? owned : foreign;
        var destination = foreignIsDestination ? foreign : owned;

        await using (var seed = database.CreateDbContext("seed", accountA.AccountId))
        {
            seed.BiologyTags.Add(new BiologyTag
                { Id = "ownedRel00a", CaveId = accountA.CaveId, TagTypeId = owned.Id });
            await seed.SaveChangesAsync();
        }
        var before = await PublishCurrentSnapshotAsync(database, accountA);

        await using (var admin = database.CreateDbContext("manager", accountA.AccountId))
        {
            var failure = await Assert.ThrowsAsync<ApiException>(() =>
                Repository(admin).MergeAsync([source.Id], destination.Id));
            Assert.Equal(400, failure.StatusCode);
        }

        await using var verify = database.CreateDbContext("verify", accountA.AccountId);
        Assert.Equal(owned.Id, await verify.BiologyTags.Select(tag => tag.TagTypeId).SingleAsync());
        Assert.Equal(before, await CurrentRevisionAsync(verify, accountA.CaveId));
    }

    private static TagTypeAdministrationRepository Repository(Planarian.Model.Database.PlanarianDbContext db)
    {
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);
        return new TagTypeAdministrationRepository(db, db.RequestUser,
            new TagReferenceLockRepository(db, db.RequestUser), snapshots,
            new CaveBulkRevisionRepository(db, db.RequestUser));
    }

    private static bool NeedsEntrance(TagRelationshipPath path) => path is
        TagRelationshipPath.EntranceStatus or TagRelationshipPath.FieldIndication or
        TagRelationshipPath.EntranceHydrology or TagRelationshipPath.EntranceReportedBy or
        TagRelationshipPath.EntranceOther;

    private static async Task<string> AddEntranceAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave, string entranceId)
    {
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, $"Quality {entranceId}");
        return await EntranceTestData.AddEntranceAsync(database, cave, entranceId, locationQualityTagId: quality.Id);
    }

    private static async Task AddCollectionReferenceAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, string? entranceId, TagRelationshipPath path, string tagId, string rowId)
    {
        await using var db = database.CreateDbContext("relationship-seed", cave.AccountId);
        switch (path)
        {
            case TagRelationshipPath.Geology:
                db.GeologyTags.Add(new GeologyTag { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            case TagRelationshipPath.EntranceStatus:
                db.EntranceStatusTags.Add(new EntranceStatusTag
                    { Id = rowId, EntranceId = entranceId!, TagTypeId = tagId });
                break;
            case TagRelationshipPath.FieldIndication:
                db.FieldIndicationTags.Add(new FieldIndicationTag
                    { Id = rowId, EntranceId = entranceId!, TagTypeId = tagId });
                break;
            case TagRelationshipPath.EntranceHydrology:
                db.EntranceHydrologyTags.Add(new EntranceHydrologyTag
                    { Id = rowId, EntranceId = entranceId!, TagTypeId = tagId });
                break;
            case TagRelationshipPath.Cartographer:
                db.CartographerNameTags.Add(new CartographerNameTag
                    { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            case TagRelationshipPath.CaveReportedBy:
                db.CaveReportedByNameTags.Add(new CaveReportedByNameTag
                    { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            case TagRelationshipPath.EntranceReportedBy:
                db.EntranceReportedByNameTags.Add(new EntranceReportedByNameTag
                    { Id = rowId, EntranceId = entranceId!, TagTypeId = tagId });
                break;
            case TagRelationshipPath.Biology:
                db.BiologyTags.Add(new BiologyTag { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            case TagRelationshipPath.Archeology:
                db.ArcheologyTags.Add(new ArcheologyTag { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            case TagRelationshipPath.MapStatus:
                db.MapStatusTags.Add(new MapStatusTag { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            case TagRelationshipPath.CaveOther:
                db.CaveOtherTags.Add(new CaveOtherTag { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            case TagRelationshipPath.EntranceOther:
                db.EntranceOtherTag.Add(new EntranceOtherTag
                    { Id = rowId, EntranceId = entranceId!, TagTypeId = tagId });
                break;
            case TagRelationshipPath.GeologicAge:
                db.GeologicAgeTags.Add(new GeologicAgeTag { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            case TagRelationshipPath.PhysiographicProvince:
                db.PhysiographicProvinceTags.Add(new PhysiographicProvinceTag
                    { Id = rowId, CaveId = cave.CaveId, TagTypeId = tagId });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(path), path, null);
        }
        await db.SaveChangesAsync();
    }

    private static async Task<List<RelationshipRow>> CollectionRowsAsync(
        Planarian.Model.Database.PlanarianDbContext db, string caveId, TagRelationshipPath path) =>
        path switch
        {
            TagRelationshipPath.Geology => await db.GeologyTags.Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.EntranceStatus => await db.EntranceStatusTags
                .Where(tag => tag.Entrance!.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.FieldIndication => await db.FieldIndicationTags
                .Where(tag => tag.Entrance!.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.EntranceHydrology => await db.EntranceHydrologyTags
                .Where(tag => tag.Entrance!.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.Cartographer => await db.CartographerNameTags.Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.CaveReportedBy => await db.CaveReportedByNameTags.Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.EntranceReportedBy => await db.EntranceReportedByNameTags
                .Where(tag => tag.Entrance!.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.Biology => await db.BiologyTags.Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.Archeology => await db.ArcheologyTags.Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.MapStatus => await db.MapStatusTags.Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.CaveOther => await db.CaveOtherTags.Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.EntranceOther => await db.EntranceOtherTag
                .Where(tag => tag.Entrance!.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.GeologicAge => await db.GeologicAgeTags.Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            TagRelationshipPath.PhysiographicProvince => await db.PhysiographicProvinceTags
                .Where(tag => tag.CaveId == caveId)
                .Select(tag => new RelationshipRow(tag.Id, tag.TagTypeId)).ToListAsync(),
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

    private static async Task<string> PublishCurrentSnapshotAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave)
    {
        await using var db = database.CreateDbContext("publish-current", cave.AccountId);
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

    private static Task<string?> CurrentRevisionAsync(
        Planarian.Model.Database.PlanarianDbContext db, string caveId) =>
        db.Caves.IgnoreQueryFilters().Where(cave => cave.Id == caveId)
            .Select(cave => cave.CurrentRevisionId).SingleAsync();

    private sealed record RelationshipRow(string Id, string TagTypeId);

    public enum TagRelationshipPath
    {
        Geology,
        EntranceStatus,
        FieldIndication,
        EntranceHydrology,
        Cartographer,
        CaveReportedBy,
        EntranceReportedBy,
        Biology,
        Archeology,
        MapStatus,
        CaveOther,
        EntranceOther,
        GeologicAge,
        PhysiographicProvince
    }
}
