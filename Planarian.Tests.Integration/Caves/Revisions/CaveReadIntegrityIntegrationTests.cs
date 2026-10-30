using Microsoft.EntityFrameworkCore;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Caves.Services;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Map.Controllers;
using Planarian.Modules.Query.Constants;
using Planarian.Modules.Query.Models;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveReadIntegrityIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private const string GeoJson = """{"type":"FeatureCollection","features":[]}""";

    [Fact]
    public async Task CaveSearchMapFilesHistoryProposalsAndAuthoringReadsNeverWriteDomainRows()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CaveSearchMapFilesHistoryProposalsAndAuthoringReadsNeverWriteDomainRows));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var foreign = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "qual00000a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cave bats", "biology00a");
        await EntranceTestData.AddEntranceAsync(database, cave, "entrance0a", locationQualityTagId: quality.Id);
        var file = await FileTestDataFactory.AddFileAsync(database, cave, associateWithCave: true,
            fileId: "readfile0a");
        var foreignFile = await FileTestDataFactory.AddFileAsync(database, foreign, associateWithCave: true,
            fileId: "readfile0b");
        const string foreignPlotId = "foreignplB";
        await using (var foreignSeed = database.CreateDbContext("foreign-seed", foreign.AccountId))
        {
            foreignSeed.CaveGeoJsons.Add(new CaveGeoJson
            { Id = foreignPlotId, CaveId = foreign.CaveId, Name = "Foreign plot", GeoJson = GeoJson });
            await foreignSeed.SaveChangesAsync();
        }
        await CavePermissions.GrantManagerAsync(database, cave, "manager");
        // Same user belongs to both accounts: account A's search cannot use their B access.
        await CavePermissions.GrantViewAsync(database, foreign, "manager");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantViewAsync(database, foreign, "foreign-viewer");
        string revisionId;
        const string plotId = "readplot0a";
        await using (var seed = database.CreateDbContext("seed", cave.AccountId))
        {
            seed.CaveGeoJsons.Add(new CaveGeoJson
            { Id = plotId, CaveId = cave.CaveId, Name = "Line plot", GeoJson = GeoJson });
            seed.BiologyTags.Add(new BiologyTag
            { Id = "biolink00a", CaveId = cave.CaveId, TagTypeId = biology.Id });
            await seed.SaveChangesAsync();
            var mutations = new CaveMutationRepository(seed, seed.RequestUser,
                new CavePublishedSnapshotRepository(seed, seed.RequestUser));
            revisionId = (await mutations.PublishExistingAsync(cave.CaveId, cave.RevisionId,
                CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, _ => { })).RevisionId!;
        }

        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor"))
        {
            var proposed = PublishableValues(cave with { RevisionId = revisionId }, quality.Id,
                "Proposed Cave name");
            requestId = await contributor.ChangeRequests.CreateAsync(cave.CaveId, proposed, revisionId, default);
            versionId = await CurrentVersionAsync(contributor.Db, requestId);
        }

        var blobs = new TestFileBlobStore();
        blobs.Seed("test", "seed-a", [1, 2, 3, 4]);
        await using var manager = await CaveTestActor.CreateAsync(database, cave.AccountId, "manager", blobs);
        await using var contributorReads = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor", blobs);
        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var repository = new CaveRepository(manager.Db, manager.Db.RequestUser);
        var fileRepository = new FileRepository(manager.Db, manager.Db.RequestUser);
        var map = new MapRepository(manager.Db, manager.Db.RequestUser, repository);
        var revision = new CaveRevisionService(repository,
            new CaveRevisionQueryRepository(manager.Db, manager.Db.RequestUser));
        var filter = new FilterQuery();
        var advanced = new FilterQuery
        {
            Conditions = [new QueryCondition(nameof(CaveSearchParamsVm.Name), QueryOperator.Contains, "Cave")]
        };
        var reads = new (string Name, Func<Task> Read)[]
        {
            ("Cave detail", async () => Assert.NotNull(await manager.Services.Caves.GetCave(cave.CaveId))),
            ("Cave list", async () => AssertOnlyOwnCave(
                (await manager.Services.Caves.GetCaves(filter)).Results.Select(row => row.Id), cave.CaveId, foreign.CaveId)),
            ("Standard search", async () => AssertOnlyOwnCave(
                (await manager.Services.Caves.GetCavesSearch(filter)).Results.Select(row => row.Id), cave.CaveId, foreign.CaveId)),
            ("Advanced search", async () => AssertOnlyOwnCave(
                (await manager.Services.Caves.GetCavesSearch(advanced)).Results.Select(row => row.Id), cave.CaveId, foreign.CaveId)),
            ("Map entrance points", async () => Assert.NotEmpty(await map.GetMapData(36, 34, -85, -87, 12, default))),
            ("Map line plot IDs", async () => AssertOnlyOwnCave(
                await map.GetLinePlotIds(36, 34, -85, -87, 12, default), plotId, foreignPlotId)),
            ("Map line plot GeoJSON", async () => Assert.NotNull(await map.GetLinePlotGeoJson(plotId, default))),
            ("Published line plots", async () => Assert.Single(await repository.GetCaveLinePlotsAsync(cave.CaveId, default))),
            ("File metadata", async () => Assert.Equal("Document",
                (await fileRepository.GetFileVm(file.FileId))?.FileTypeKey)),
            ("File authorization metadata", async () => Assert.NotNull(
                await fileRepository.GetFileAuthorizationContext(file.FileId))),
            ("File view response setup", async () => Assert.NotNull(
                await manager.Services.Files.CreateFileResponse(file.FileId, false, default))),
            ("File download response setup", async () => Assert.NotNull(
                await manager.Services.Files.CreateFileResponse(file.FileId, true, default))),
            ("Revision listing", async () => Assert.Equal(2, (await revision.ListAsync(cave.CaveId, default)).Revisions.Count)),
            ("Individual revision", async () => Assert.NotNull(await revision.CompareAsync(cave.CaveId, revisionId, default))),
            ("Proposal authoring context", async () => Assert.Equal(revisionId,
                (await contributorReads.ChangeRequests.GetAuthoringContextAsync(cave.CaveId, default)).ExpectedBaseRevisionId)),
            ("Direct editing context", async () => Assert.Equal(revisionId,
                (await manager.Services.Caves.GetEditAuthoringContextAsync(cave.CaveId, default)).Cave.CurrentRevisionId)),
            ("Proposal listing", async () => Assert.Single(await contributorReads.ChangeRequests.ListMineAsync(default))),
            ("Proposal review listing", async () => Assert.Single(await manager.ChangeRequests.ListForReviewAsync(default))),
            ("Proposal detail", async () => Assert.NotNull(await contributorReads.ChangeRequests.GetAsync(requestId, default))),
            ("Proposal version detail", async () => Assert.NotNull(
                await contributorReads.ChangeRequests.GetVersionAsync(requestId, versionId, default))),
            ("County number lookup", async () => Assert.True(
                await manager.Services.Caves.GetNextCountyNumber(cave.CountyId, caveId: cave.CaveId) > 0)),
            ("County number in-use", async () => Assert.False(
                await manager.Services.Caves.IsCountyNumberInUse(cave.CountyId, cave.CountyNumber, cave.CaveId))),
            ("Tag lookup", async () => Assert.NotEmpty(await repository.GetTagCandidatesByIdsAsync([biology.Id], default))),
            ("Foreign Cave detail", async () => await Assert.ThrowsAsync<ApiException>(() =>
                manager.Services.Caves.GetCave(foreign.CaveId))),
            ("Foreign File metadata", async () => Assert.Null(await fileRepository.GetFileVm(foreignFile.FileId))),
            ("Foreign File download", async () => await Assert.ThrowsAsync<ApiException>(() =>
                manager.Services.Files.CreateFileResponse(foreignFile.FileId, true, default))),
            ("Foreign map line plot", async () => Assert.Null(await map.GetLinePlotGeoJson(foreignPlotId, default))),
            ("Foreign revision history", async () => await Assert.ThrowsAsync<ApiException>(() =>
                revision.ListAsync(foreign.CaveId, default))),
            ("Foreign proposal context", async () => await Assert.ThrowsAsync<ApiException>(() =>
                contributorReads.ChangeRequests.GetAuthoringContextAsync(foreign.CaveId, default)))
        };

        var baseline = await CaveDurableStateFingerprint.CaptureAsync(verify);
        foreach (var (name, read) in reads)
        {
            await read();
            await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, baseline, name);
            Assert.False(manager.Db.ChangeTracker.HasChanges(), $"{name} left pending Manager EF changes.");
            Assert.False(contributorReads.Db.ChangeTracker.HasChanges(),
                $"{name} left pending Contributor EF changes.");
        }
        Assert.Equal(0, blobs.DeleteIfExistsAttempts);
        Assert.Single(blobs.Keys);
    }

    [Fact]
    public async Task FingerprintDetectsEvenAnIdenticalValueRewrite()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(FingerprintDetectsEvenAnIdenticalValueRewrite));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        await using var verify = database.CreateDbContext("fingerprint-negative-control", cave.AccountId);
        var before = await CaveDurableStateFingerprint.CaptureAsync(verify);
        await using (var mutate = database.CreateDbContext("same-value-rewrite", cave.AccountId))
        {
            // Intentionally change the PostgreSQL row version without changing any domain field values.
            Assert.Equal(1, await mutate.Caves.IgnoreQueryFilters()
                .Where(row => row.AccountId == cave.AccountId && row.Id == cave.CaveId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Name, row => row.Name)));
        }
        var after = await CaveDurableStateFingerprint.CaptureAsync(verify);
        var caveTable = verify.Model.FindEntityType(typeof(Cave))!.GetTableName()!;
        Assert.NotEqual(before[caveTable], after[caveTable]);
    }

    [Fact]
    public async Task FingerprintSelectsDomainByEntityTypeAndExcludesAuthenticationAndDiagnostics()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FingerprintSelectsDomainByEntityTypeAndExcludesAuthenticationAndDiagnostics));
        await using var verify = database.CreateDbContext("fingerprint-coverage", null);
        var hashes = await CaveDurableStateFingerprint.CaptureAsync(verify);

        foreach (var entityType in new[]
                 {
                     typeof(Cave), typeof(CaveRevision), typeof(Entrance), typeof(File),
                     typeof(StorageCleanupTarget), typeof(State), typeof(Favorite),
                     typeof(TagType), typeof(LeadTag), typeof(TripTag)
                 })
        {
            var mappedTable = verify.Model.FindEntityType(entityType)!.GetTableName()!;
            Assert.Contains(mappedTable, hashes.Keys);
        }

        // Authentication refreshes User.LastActiveOn; request diagnostics are not Cave mutations.
        foreach (var entityType in new[] { typeof(User), typeof(ThrottleEventLog) })
            Assert.DoesNotContain(verify.Model.FindEntityType(entityType)!.GetTableName()!, hashes.Keys);
    }

    private static void AssertOnlyOwnCave(IEnumerable<string> visibleIds, string ownId, string foreignId)
    {
        var ids = visibleIds.ToList();
        Assert.Contains(ownId, ids);
        Assert.DoesNotContain(foreignId, ids);
    }

    [Fact]
    public async Task UninitializedExistingCaveReadsCannotManufactureLateRevisionBaseline()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(UninitializedExistingCaveReadsCannotManufactureLateRevisionBaseline));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var foreign = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "qual00000a");
        await CavePermissions.GrantManagerAsync(database, cave, "manager");
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await using (var seed = database.CreateDbContext("rollout-simulation", cave.AccountId))
            await seed.Caves.Where(row => row.Id == cave.CaveId)
                .IgnoreQueryFilters()
                .Where(row => row.AccountId == cave.AccountId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.CurrentRevisionId, (string?)null));

        await using var manager = await CaveTestActor.CreateAsync(database, cave.AccountId, "manager");
        await using var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor");
        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var fingerprint = await CaveDurableStateFingerprint.CaptureAsync(verify);
        Assert.Null((await manager.Services.Caves.GetCave(cave.CaveId))!.CurrentRevisionId);
        Assert.NotEmpty((await manager.Services.Caves.GetCaves(new FilterQuery())).Results);
        Assert.Null(await new CaveRepository(manager.Db, manager.Db.RequestUser).GetCave(foreign.CaveId));

        var editFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            manager.Services.Caves.GetEditAuthoringContextAsync(cave.CaveId, default));
        Assert.Contains("bootstrap", editFailure.Message, StringComparison.OrdinalIgnoreCase);
        var proposalFailure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            contributor.ChangeRequests.GetAuthoringContextAsync(cave.CaveId, default));
        Assert.Contains("bootstrap", proposalFailure.Message, StringComparison.OrdinalIgnoreCase);
        var repo = new CaveRepository(manager.Db, manager.Db.RequestUser);
        var revisions = new CaveRevisionService(repo,
            new CaveRevisionQueryRepository(manager.Db, manager.Db.RequestUser));
        await Assert.ThrowsAsync<InvalidOperationException>(() => revisions.ListAsync(cave.CaveId, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => revisions.CompareAsync(cave.CaveId, cave.RevisionId, default));
        var request = PublishableValues(cave, quality.Id, "Must not establish baseline");
        await Assert.ThrowsAsync<CaveRevisionConflictException>(() => manager.Services.Caves.AddCave(request, default));
        await CaveDurableStateFingerprint.AssertUnchangedAsync(verify, fingerprint);
        Assert.Equal(1, await verify.CaveRevisions.CountAsync(row => row.CaveId == cave.CaveId));
        Assert.Null(await verify.Caves.IgnoreQueryFilters().Where(row => row.AccountId == cave.AccountId && row.Id == cave.CaveId)
            .Select(row => row.CurrentRevisionId).SingleAsync());
    }
}
