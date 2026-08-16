using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class ReferenceRenameNoFanOutIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task PendingProposalKeepsCapturedLabelWhileAcceptedRevisionUsesPublicationTimeLabel()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PendingProposalKeepsCapturedLabelWhileAcceptedRevisionUsesPublicationTimeLabel));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, tenant.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade A", "locqual00a");
        await CavePermissions.GrantViewAsync(database, tenant, "contributor");
        await CavePermissions.GrantManagerAsync(database, tenant, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
        {
            requestId = await contributor.ChangeRequests.CreateAsync(tenant.CaveId,
                CaveChangeRequestTestSupport.PublishableValues(tenant, quality.Id, "Pending rename"),
                tenant.RevisionId, default);
            versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(contributor.Db, requestId);
        }

        await using (var rename = database.CreateDbContext("manager", tenant.AccountId))
        {
            (await rename.TagTypes.SingleAsync(tag => tag.Id == quality.Id)).Name = "Survey Grade B";
            await rename.SaveChangesAsync();
            var persisted = await rename.CaveProposalVersions.SingleAsync(version => version.Id == versionId);
            Assert.Contains("Survey Grade A", persisted.ProposalJson);
            Assert.DoesNotContain("Survey Grade B", persisted.ProposalJson);
        }

        await using (var contributor = await CaveTestActor.CreateAsync(database, tenant.AccountId, "contributor"))
            Assert.Equal("Survey Grade A", (await contributor.ChangeRequests.GetVersionAsync(requestId,
                versionId, default)).Proposed.Entrances.Single().LocationQualityNameAtRevision);

        string acceptedRevisionId;
        await using (var reviewer = await CaveTestActor.CreateAsync(database, tenant.AccountId, "reviewer"))
            acceptedRevisionId = (await reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default))
                .PublishedRevisionId!;

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var accepted = CaveSnapshotJson.Deserialize((await verify.CaveRevisions
            .SingleAsync(revision => revision.Id == acceptedRevisionId)).SnapshotJson, 1);
        Assert.Equal("Survey Grade B", accepted.Entrances.Single().LocationQualityNameAtRevision);
    }

    [Fact]
    public async Task ReportedByPeopleTagRenamePreservesHistoricalNameUntilNextLegitimateRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ReportedByPeopleTagRenamePreservesHistoricalNameUntilNextLegitimateRevision));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var caveIds = new[] { tenant.CaveId, "cave00000b", "cave00000c" };
        string tagId;

        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            db.Caves.AddRange(NewCave(caveIds[1], tenant, 2, "Second Cave"), NewCave(caveIds[2], tenant, 3, "Third Cave"));
            var tag = new TagType("Alice Reporter", "people") { Id = IdGenerator.Generate(), AccountId = tenant.AccountId, IsDefault = false };
            db.TagTypes.Add(tag);
            await db.SaveChangesAsync();
            tagId = tag.Id;
            db.CaveReportedByNameTags.AddRange(caveIds.Select(id => new CaveReportedByNameTag
                { Id = IdGenerator.Generate(), CaveId = id, TagTypeId = tagId }));
            await db.SaveChangesAsync();
        }

        var pointers = new Dictionary<string, string>();
        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            var reader = new CavePublishedSnapshotRepository(db, db.RequestUser);
            foreach (var caveId in caveIds)
            {
                var snapshot = await reader.BuildAsync(caveId);
                var previousRevisionId = caveId == tenant.CaveId ? tenant.RevisionId : null;
                var revision = new CaveRevision
                {
                    Id = IdGenerator.Generate(), AccountId = tenant.AccountId, CaveId = caveId,
                    PreviousRevisionId = previousRevisionId, Source = CaveRevisionSource.ManagerEdit,
                    Operation = CaveRevisionOperation.Update, SnapshotSchemaVersion = 1,
                    SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
                };
                db.CaveRevisions.Add(revision);
                await db.SaveChangesAsync();
                var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == caveId);
                cave.CurrentRevisionId = revision.Id;
                await db.SaveChangesAsync();
                pointers[caveId] = revision.Id;
            }
        }

        Dictionary<string,int> beforeCounts;
        await using (var db = database.CreateDbContext("manager", tenant.AccountId))
        {
            beforeCounts = await db.CaveRevisions.Where(r => caveIds.Contains(r.CaveId)).GroupBy(r => r.CaveId).ToDictionaryAsync(g => g.Key, g => g.Count());
            var tag = await db.TagTypes.SingleAsync(t => t.Id == tagId);
            tag.Name = "Alice Renamed";
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
            var reader = new CavePublishedSnapshotRepository(db, db.RequestUser);
            var coordinator = new CaveMutationRepository(db, db.RequestUser, reader);
            result = await coordinator.PublishExistingAsync(tenant.CaveId, pointers[tenant.CaveId], CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update, cave => cave.Name = "Legitimate edit");
        }
        Assert.True(result.CreatedRevision);

        await using (var verify = database.CreateDbContext("manager", tenant.AccountId))
        {
            var previous = await verify.CaveRevisions.SingleAsync(r => r.Id == pointers[tenant.CaveId]);
            var current = await verify.CaveRevisions.SingleAsync(r => r.Id == result.RevisionId);
            var diff = new CaveRevisionDiffService().Compare(
                CaveSnapshotJson.Deserialize(previous.SnapshotJson, previous.SnapshotSchemaVersion),
                CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion));
            Assert.Contains(diff.ReferenceMetadataChanges, c => c.StableId == tagId &&
                c.PreviousValue == "Alice Reporter" && c.CurrentValue == "Alice Renamed");
            var untouched = await verify.Caves.IgnoreQueryFilters().Where(c => caveIds.Skip(1).Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.CurrentRevisionId);
            Assert.All(untouched, p => Assert.Equal(pointers[p.Key], p.Value));
        }
    }

    private static Cave NewCave(string id, PublishedCaveTestData tenant, int number, string name) => new()
    {
        Id = id, AccountId = tenant.AccountId, StateId = tenant.StateId, CountyId = tenant.CountyId,
        CountyNumber = number, Name = name, IsArchived = false
    };
}
