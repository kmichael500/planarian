using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Tests;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Xunit;
using static Planarian.Tests.Integration.Caves.Revisions.CaveChangeRequestTestSupport;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class CaveDirectTagMutationIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task ManagerSaveReplacesEveryCaveTagFamilyAndRecordsRemovals()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ManagerSaveReplacesEveryCaveTagFamilyAndRecordsRemovals));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");

        var oldTags = await AddCaveTagsAsync(database, tenant.AccountId, "old", 'a');
        var newTags = await AddCaveTagsAsync(database, tenant.AccountId, "new", 'b');
        await CavePermissions.GrantManagerAsync(database, tenant, "manager");

        await using (var manager = await CaveTestActor.CreateAsync(database, tenant.AccountId, "manager"))
        {
            var initial = PublishableValues(tenant, quality.Id, "Old cave tag selections");
            Apply(initial, oldTags);
            await manager.Services.Caves.AddCave(initial, default);

            var context = await manager.Services.Caves.GetEditAuthoringContextAsync(tenant.CaveId, default);
            var replacement = ValuesFromCave(context.Cave);
            Apply(replacement, newTags);
            await manager.Services.Caves.AddCave(replacement, default);
        }

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        await AssertSingleAsync(verify.GeologyTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.Geology);
        await AssertSingleAsync(verify.GeologicAgeTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.GeologicAge);
        await AssertSingleAsync(verify.MapStatusTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.MapStatus);
        await AssertSingleAsync(verify.PhysiographicProvinceTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.Province);
        await AssertSingleAsync(verify.ArcheologyTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.Archeology);
        await AssertSingleAsync(verify.BiologyTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.Biology);
        await AssertSingleAsync(verify.CaveOtherTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.Other);
        await AssertSingleAsync(verify.CartographerNameTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.Cartographer);
        await AssertSingleAsync(verify.CaveReportedByNameTags.Where(tag => tag.CaveId == tenant.CaveId), newTags.ReportedBy);

        var revisions = await verify.CaveRevisions.AsNoTracking()
            .Where(revision => revision.CaveId == tenant.CaveId)
            .OrderByDescending(revision => revision.CreatedOn).Take(2).ToListAsync();
        Assert.Equal(2, revisions.Count);
        var current = CaveSnapshotJson.Deserialize(revisions[0].SnapshotJson, revisions[0].SnapshotSchemaVersion);
        var previous = CaveSnapshotJson.Deserialize(revisions[1].SnapshotJson, revisions[1].SnapshotSchemaVersion);
        var removed = new CaveRevisionDiffService().Compare(previous, current).RemovedTags
            .Select(tag => (tag.Role, tag.TagTypeId)).ToHashSet();

        Assert.Equal(new HashSet<(SnapshotTagRole, string)>
        {
            (SnapshotTagRole.Geology, oldTags.Geology),
            (SnapshotTagRole.GeologicAge, oldTags.GeologicAge),
            (SnapshotTagRole.MapStatus, oldTags.MapStatus),
            (SnapshotTagRole.PhysiographicProvince, oldTags.Province),
            (SnapshotTagRole.Archeology, oldTags.Archeology),
            (SnapshotTagRole.Biology, oldTags.Biology),
            (SnapshotTagRole.CaveOther, oldTags.Other),
            (SnapshotTagRole.Cartographer, oldTags.Cartographer),
            (SnapshotTagRole.CaveReportedBy, oldTags.ReportedBy)
        }, removed);
    }

    private static void Apply(Planarian.Modules.Caves.Models.AddCaveVm cave, CaveTagSet tags)
    {
        cave.GeologyTagIds = [tags.Geology];
        cave.GeologicAgeTagIds = [tags.GeologicAge];
        cave.MapStatusTagIds = [tags.MapStatus];
        cave.PhysiographicProvinceTagIds = [tags.Province];
        cave.ArcheologyTagIds = [tags.Archeology];
        cave.BiologyTagIds = [tags.Biology];
        cave.OtherTagIds = [tags.Other];
        cave.CartographerNameTagIds = [tags.Cartographer];
        cave.ReportedByNameTagIds = [tags.ReportedBy];
    }

    private static async Task<CaveTagSet> AddCaveTagsAsync(PostgresTestDatabase database, string accountId,
        string prefix, char suffix)
    {
        async Task<string> Add(string key, string role, int index) =>
            (await ReferenceTestData.AddTagAsync(database, accountId, key, $"{prefix} {role}",
                $"{prefix[0]}{index:00}tag{suffix}000")).Id;

        return new CaveTagSet(
            await Add(TagTypeKeyConstant.Geology, "geology", 1),
            await Add(TagTypeKeyConstant.GeologicAge, "age", 2),
            await Add(TagTypeKeyConstant.MapStatus, "map", 3),
            await Add(TagTypeKeyConstant.PhysiographicProvince, "province", 4),
            await Add(TagTypeKeyConstant.Archeology, "archeology", 5),
            await Add(TagTypeKeyConstant.Biology, "biology", 6),
            await Add(TagTypeKeyConstant.CaveOther, "other", 7),
            await Add(TagTypeKeyConstant.People, "cartographer", 8),
            await Add(TagTypeKeyConstant.People, "reporter", 9));
    }
    private static async Task AssertSingleAsync<T>(IQueryable<T> query, string expectedTagId)
        where T : class
    {
        var row = Assert.Single(await query.ToListAsync());
        Assert.Equal(expectedTagId, row.GetType().GetProperty("TagTypeId")!.GetValue(row));
    }

    private sealed record CaveTagSet(string Geology, string GeologicAge, string MapStatus,
        string Province, string Archeology, string Biology, string Other, string Cartographer, string ReportedBy);
}
