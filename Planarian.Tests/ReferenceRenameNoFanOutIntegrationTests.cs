using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Xunit;

namespace Planarian.Tests;

public sealed class ReferenceRenameNoFanOutIntegrationTests(PostgresIntegrationFixture fixture)
    : IClassFixture<PostgresIntegrationFixture>
{
    [Fact]
    public async Task SharedTagRenameDoesNotFanOutButNextLegitimateRevisionCapturesRename()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(SharedTagRenameDoesNotFanOutButNextLegitimateRevisionCapturesRename));
        var tenant = await IntegrationTestData.SeedTenantAsync(database, 'a');
        var caveIds = new[] { tenant.CaveId, "cave0000b", "cave0000c" };
        string tagId;

        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            db.Caves.AddRange(NewCave(caveIds[1], tenant, 2, "Second Cave"), NewCave(caveIds[2], tenant, 3, "Third Cave"));
            var tag = new TagType("Old Geology Label", "geology") { Id = IdGenerator.Generate(), AccountId = tenant.AccountId, IsDefault = false };
            db.TagTypes.Add(tag);
            await db.SaveChangesAsync();
            tagId = tag.Id;
            db.GeologyTags.AddRange(caveIds.Select(id => new GeologyTag { Id = IdGenerator.Generate(), CaveId = id, TagTypeId = tagId }));
            await db.SaveChangesAsync();
        }

        var pointers = new Dictionary<string, string>();
        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            var reader = new CavePublishedSnapshotReader(db, db.RequestUser);
            foreach (var caveId in caveIds.Skip(1))
            {
                var snapshot = await reader.BuildAsync(caveId);
                var revision = new CaveRevision { Id = IdGenerator.Generate(), AccountId = tenant.AccountId, CaveId = caveId, Source = CaveRevisionSource.ManagerEdit, Operation = CaveRevisionOperation.Create, SnapshotSchemaVersion = 1, SnapshotJson = CaveSnapshotJson.Serialize(snapshot) };
                db.CaveRevisions.Add(revision);
                await db.SaveChangesAsync();
                var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == caveId);
                cave.CurrentRevisionId = revision.Id;
                await db.SaveChangesAsync();
                pointers[caveId] = revision.Id;
            }
            pointers[tenant.CaveId] = tenant.RevisionId;
        }

        Dictionary<string,int> beforeCounts;
        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            beforeCounts = await db.CaveRevisions.Where(r => caveIds.Contains(r.CaveId)).GroupBy(r => r.CaveId).ToDictionaryAsync(g => g.Key, g => g.Count());
            var tag = await db.TagTypes.SingleAsync(t => t.Id == tagId);
            tag.Name = "Renamed Geology Label";
            await db.SaveChangesAsync();
        }

        await using (var verify = database.CreateDbContext("manager", tenant.AccountId))
        {
            var afterCounts = await verify.CaveRevisions.Where(r => caveIds.Contains(r.CaveId)).GroupBy(r => r.CaveId).ToDictionaryAsync(g => g.Key, g => g.Count());
            Assert.Equal(beforeCounts, afterCounts);
            var current = await verify.Caves.IgnoreQueryFilters().Where(c => caveIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CurrentRevisionId);
            foreach (var id in caveIds) Assert.Equal(pointers[id], current[id]);
        }

        CaveMutationResult result;
        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            var reader = new CavePublishedSnapshotReader(db, db.RequestUser);
            var coordinator = new CaveMutationCoordinator(db, db.RequestUser, reader);
            result = await coordinator.PublishExistingAsync(tenant.CaveId, tenant.RevisionId, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Name = "Legitimate edit");
        }
        Assert.True(result.CreatedRevision);

        await using (var verify = database.CreateDbContext("manager", tenant.AccountId))
        {
            var previous = await verify.CaveRevisions.SingleAsync(r => r.Id == tenant.RevisionId);
            var current = await verify.CaveRevisions.SingleAsync(r => r.Id == result.RevisionId);
            var diff = new CaveRevisionDiffService().Compare(
                CaveSnapshotJson.Deserialize(previous.SnapshotJson, previous.SnapshotSchemaVersion),
                CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion));
            Assert.Contains(diff.ReferenceMetadataChanges, c => c.StableId == tagId && c.PreviousValue == "Old Geology Label" && c.CurrentValue == "Renamed Geology Label");
            var untouched = await verify.Caves.IgnoreQueryFilters().Where(c => caveIds.Skip(1).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CurrentRevisionId);
            Assert.All(untouched, p => Assert.Equal(pointers[p.Key], p.Value));
        }
    }

    private static Cave NewCave(string id, TenantSeed tenant, int number, string name) => new()
    {
        Id = id, AccountId = tenant.AccountId, StateId = tenant.StateId, CountyId = tenant.CountyId,
        CountyNumber = number, Name = name, IsArchived = false
    };
}
