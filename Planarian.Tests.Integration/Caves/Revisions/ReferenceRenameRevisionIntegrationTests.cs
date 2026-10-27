using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Caves.Revisions;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

public sealed class ReferenceRenameRevisionIntegrationTests(PostgresTestServer fixture)
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
            await IntegrationTestServices.For(rename).Account.CreateOrUpdateTagType(
                new CreateEditTagTypeVm { Name = "Survey Grade B", Key = TagTypeKeyConstant.LocationQuality },
                quality.Id, default);
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
    public async Task RenameOfPublishedReferenceMakesPendingProposalStaleWithoutRewritingProposalVersion()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(RenameOfPublishedReferenceMakesPendingProposalStaleWithoutRewritingProposalVersion));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var biology = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        var proposalBase = await AttachReferenceAndPublishAsync(
            database, cave, TagTypeKeyConstant.Biology, biology.Id);
        cave = cave with { RevisionId = proposalBase };
        await CavePermissions.GrantViewAsync(database, cave, "contributor");
        await CavePermissions.GrantManagerAsync(database, cave, "reviewer");

        string requestId;
        string versionId;
        await using (var contributor = await CaveTestActor.CreateAsync(database, cave.AccountId, "contributor"))
        {
            var values = CaveChangeRequestTestSupport.PublishableValues(cave, quality.Id, "Pending after rename");
            values.BiologyTagIds = [biology.Id];
            requestId = await contributor.ChangeRequests.CreateAsync(
                cave.CaveId, values, proposalBase, default);
            versionId = await CaveChangeRequestTestSupport.CurrentVersionAsync(contributor.Db, requestId);
        }

        await using (var rename = database.CreateDbContext("manager", cave.AccountId))
            await IntegrationTestServices.For(rename).Account.CreateOrUpdateTagType(
                new CreateEditTagTypeVm { Name = "Cave Cricket", Key = TagTypeKeyConstant.Biology },
                biology.Id, default);

        await using (var reviewer = await CaveTestActor.CreateAsync(database, cave.AccountId, "reviewer"))
        {
            var detail = await reviewer.ChangeRequests.GetAsync(requestId, default);
            Assert.True(detail.Request.IsStale);
            await Assert.ThrowsAsync<CaveRevisionConflictException>(
                () => reviewer.ChangeRequests.ApproveAsync(requestId, versionId, null, default));
        }

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var stored = await verify.CaveProposalVersions.SingleAsync(version => version.Id == versionId);
        Assert.Equal(proposalBase, stored.BaseRevisionId);
        Assert.Contains("Cricket", stored.ProposalJson);
        Assert.DoesNotContain("Cave Cricket", stored.ProposalJson);
        Assert.Equal(CaveChangeRequestStatus.Pending,
            (await verify.CaveChangeRequests.SingleAsync(request => request.Id == requestId)).Status);
    }

    [Theory]
    [InlineData(TagTypeKeyConstant.Geology)]
    [InlineData(TagTypeKeyConstant.EntranceStatus)]
    [InlineData(TagTypeKeyConstant.FieldIndication)]
    [InlineData(TagTypeKeyConstant.EntranceHydrology)]
    [InlineData(TagTypeKeyConstant.File)]
    [InlineData(TagTypeKeyConstant.People)]
    [InlineData(TagTypeKeyConstant.Biology)]
    [InlineData(TagTypeKeyConstant.Archeology)]
    [InlineData(TagTypeKeyConstant.MapStatus)]
    [InlineData(TagTypeKeyConstant.CaveOther)]
    [InlineData(TagTypeKeyConstant.GeologicAge)]
    [InlineData(TagTypeKeyConstant.PhysiographicProvince)]
    [InlineData(TagTypeKeyConstant.LocationQuality)]
    public async Task RenamePublishesReferenceMetadataRevisionForEveryAccountTagFamily(string key)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(RenamePublishesReferenceMetadataRevisionForEveryAccountTagFamily)}_{key}");
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(database, cave.AccountId, key, "Original label");
        var beforeId = await AttachReferenceAndPublishAsync(database, cave, key, tag.Id);

        await using (var rename = database.CreateDbContext("manager", cave.AccountId))
            await IntegrationTestServices.For(rename).Account.CreateOrUpdateTagType(
                new CreateEditTagTypeVm { Name = "Renamed label", Key = key }, tag.Id, default);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        var caveRow = await verify.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == cave.CaveId);
        Assert.NotEqual(beforeId, caveRow.CurrentRevisionId);
        var before = await verify.CaveRevisions.SingleAsync(revision => revision.Id == beforeId);
        var current = await verify.CaveRevisions.SingleAsync(revision => revision.Id == caveRow.CurrentRevisionId);
        Assert.Equal(beforeId, current.PreviousRevisionId);
        Assert.Equal(CaveRevisionSource.ManagerEdit, current.Source);
        Assert.Equal(CaveRevisionOperation.Update, current.Operation);

        var diff = new CaveRevisionDiffService().Compare(
            CaveSnapshotJson.Deserialize(before.SnapshotJson, before.SnapshotSchemaVersion),
            CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion));
        Assert.Contains(diff.ReferenceMetadataChanges, change =>
            change.StableId == tag.Id &&
            change.PreviousValue == "Original label" &&
            change.CurrentValue == "Renamed label");
    }

    [Fact]
    public async Task PeopleTagRenamePublishesOneRevisionPerAffectedCaveOnly()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(PeopleTagRenamePublishesOneRevisionPerAffectedCaveOnly));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var secondCave = await CaveTestDataFactory.AddCaveAsync(
            database, tenant, "cave00000b", "Second Cave", 2);
        var second = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, secondCave, "revision0b");
        var unaffectedCave = await CaveTestDataFactory.AddCaveAsync(
            database, tenant, "cave00000c", "Unaffected Cave", 3);
        var unaffected = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, unaffectedCave, "revision0c");
        var person = await ReferenceTestData.AddTagAsync(
            database, tenant.AccountId, TagTypeKeyConstant.People, "Alice Reporter", "people000a");

        await using (var seed = database.CreateDbContext("seed", tenant.AccountId))
        {
            seed.CaveReportedByNameTags.Add(new CaveReportedByNameTag
                { Id = IdGenerator.Generate(), CaveId = tenant.CaveId, TagTypeId = person.Id });
            seed.CartographerNameTags.Add(new CartographerNameTag
                { Id = IdGenerator.Generate(), CaveId = second.CaveId, TagTypeId = person.Id });
            await seed.SaveChangesAsync();
        }

        var firstBefore = await PublishCurrentSnapshotAsync(
            database, tenant.AccountId, tenant.CaveId, tenant.RevisionId);
        var secondBefore = await PublishCurrentSnapshotAsync(
            database, tenant.AccountId, second.CaveId, second.RevisionId);

        await using (var rename = database.CreateDbContext("manager", tenant.AccountId))
            await IntegrationTestServices.For(rename).Account.CreateOrUpdateTagType(
                new CreateEditTagTypeVm { Name = "Alice Renamed", Key = TagTypeKeyConstant.People },
                person.Id, default);

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var pointers = await verify.Caves.IgnoreQueryFilters()
            .Where(cave => new[] { tenant.CaveId, second.CaveId, unaffected.CaveId }.Contains(cave.Id))
            .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId);

        Assert.NotEqual(firstBefore, pointers[tenant.CaveId]);
        Assert.NotEqual(secondBefore, pointers[second.CaveId]);
        Assert.Equal(unaffected.RevisionId, pointers[unaffected.CaveId]);

        foreach (var (caveId, previousId) in new[]
                 {
                     (tenant.CaveId, firstBefore),
                     (second.CaveId, secondBefore)
                 })
        {
            var current = await verify.CaveRevisions.SingleAsync(revision => revision.Id == pointers[caveId]);
            Assert.Equal(previousId, current.PreviousRevisionId);
            Assert.Equal(CaveRevisionSource.ManagerEdit, current.Source);
            Assert.Equal(CaveRevisionOperation.Update, current.Operation);
            Assert.Equal("manager", current.CreatedByUserId);

            var previous = await verify.CaveRevisions.SingleAsync(revision => revision.Id == previousId);
            var previousSnapshot = CaveSnapshotJson.Deserialize(previous.SnapshotJson, previous.SnapshotSchemaVersion);
            var currentSnapshot = CaveSnapshotJson.Deserialize(current.SnapshotJson, current.SnapshotSchemaVersion);
            Assert.Contains(previousSnapshot.Tags,
                tag => tag.TagTypeId == person.Id && tag.NameAtRevision == "Alice Reporter");
            Assert.Contains(currentSnapshot.Tags,
                tag => tag.TagTypeId == person.Id && tag.NameAtRevision == "Alice Renamed");
        }

        var revisionCount = await verify.CaveRevisions.CountAsync(revision =>
            revision.PreviousRevisionId == firstBefore || revision.PreviousRevisionId == secondBefore);
        Assert.Equal(2, revisionCount);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task CountyNameOrCodeRenamePublishesOneRevisionPerAffectedCaveOnly(
        bool renameName, bool renameCode)
    {
        await using var database = await fixture.CreateDatabaseAsync(
            $"{nameof(CountyNameOrCodeRenamePublishesOneRevisionPerAffectedCaveOnly)}_{renameName}_{renameCode}");
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var updatedName = renameName ? "Renamed County" : tenant.CountyName;
        var updatedCode = renameCode ? "R99" : tenant.CountyDisplayId;
        var secondCave = await CaveTestDataFactory.AddCaveAsync(
            database, tenant, "cave00000b", "Second Cave", 2);
        var second = await CaveTestDataFactory.PublishBaselineRevisionAsync(database, secondCave, "revision0b");

        const string otherCountyId = "county00az";
        await using (var seed = database.CreateDbContext("county-seed", tenant.AccountId))
        {
            seed.Counties.Add(new County
            {
                Id = otherCountyId,
                AccountId = tenant.AccountId,
                StateId = tenant.StateId,
                Name = "Other County",
                DisplayId = "Z99"
            });
            await seed.SaveChangesAsync();
        }

        var otherTenant = new AccountCountyTestData(
            tenant.AccountId, tenant.StateId, tenant.StateName, tenant.StateAbbreviation,
            otherCountyId, "Other County", "Z99");
        var unaffectedCave = await CaveTestDataFactory.AddCaveAsync(
            database, otherTenant, "cave00000c", "Unaffected Cave", 3);
        var unaffected = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, unaffectedCave, "revision0c");

        await using (var rename = database.CreateDbContext("manager", tenant.AccountId))
            await IntegrationTestServices.For(rename).Account.CreateOrUpdateCounty(
                tenant.StateId,
                new CreateCountyVm { Name = updatedName, CountyDisplayId = updatedCode },
                tenant.CountyId, default);

        await using (var verify = database.CreateDbContext("verify", tenant.AccountId))
        {
            var pointers = await verify.Caves.IgnoreQueryFilters()
                .Where(cave => new[] { tenant.CaveId, second.CaveId, unaffected.CaveId }.Contains(cave.Id))
                .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId);

            Assert.NotEqual(tenant.RevisionId, pointers[tenant.CaveId]);
            Assert.NotEqual(second.RevisionId, pointers[second.CaveId]);
            Assert.Equal(unaffected.RevisionId, pointers[unaffected.CaveId]);

            foreach (var (caveId, previousId) in new[]
                     {
                         (tenant.CaveId, tenant.RevisionId),
                         (second.CaveId, second.RevisionId)
                     })
            {
                var current = await verify.CaveRevisions.SingleAsync(revision => revision.Id == pointers[caveId]);
                Assert.Equal(previousId, current.PreviousRevisionId);
                Assert.Equal(CaveRevisionSource.ManagerEdit, current.Source);
                Assert.Equal(CaveRevisionOperation.Update, current.Operation);

                var previous = await verify.CaveRevisions.SingleAsync(revision => revision.Id == previousId);
                var previousSnapshot = CaveSnapshotJson.Deserialize(
                    previous.SnapshotJson, previous.SnapshotSchemaVersion);
                var currentSnapshot = CaveSnapshotJson.Deserialize(
                    current.SnapshotJson, current.SnapshotSchemaVersion);
                Assert.Equal(tenant.CountyId, previousSnapshot.County.Id);
                Assert.Equal(tenant.CountyName, previousSnapshot.County.NameAtRevision);
                Assert.Equal(tenant.CountyDisplayId, previousSnapshot.County.DisplayIdAtRevision);
                Assert.Equal(tenant.CountyId, currentSnapshot.County.Id);
                Assert.Equal(updatedName, currentSnapshot.County.NameAtRevision);
                Assert.Equal(updatedCode, currentSnapshot.County.DisplayIdAtRevision);
                Assert.Equal(previousSnapshot.CountyNumber, currentSnapshot.CountyNumber);
            }
        }

    }

    [Fact]
    public async Task TagRenameWithIdenticalNameCreatesNoRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TagRenameWithIdenticalNameCreatesNoRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(
            database, cave.AccountId, TagTypeKeyConstant.Biology, "Cricket", "biology00a");
        var beforeId = await AttachReferenceAndPublishAsync(
            database, cave, TagTypeKeyConstant.Biology, tag.Id);

        await using (var rename = database.CreateDbContext("manager", cave.AccountId))
            await IntegrationTestServices.For(rename).Account.CreateOrUpdateTagType(
                new CreateEditTagTypeVm { Name = "Cricket", Key = TagTypeKeyConstant.Biology },
                tag.Id, default);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(beforeId, await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == cave.CaveId).Select(row => row.CurrentRevisionId).SingleAsync());
        Assert.False(await verify.CaveRevisions.AnyAsync(revision => revision.PreviousRevisionId == beforeId));
    }

    [Fact]
    public async Task CountyRenameWithIdenticalNameAndCodeCreatesNoRevision()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyRenameWithIdenticalNameAndCodeCreatesNoRevision));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');

        await using (var rename = database.CreateDbContext("manager", cave.AccountId))
            await IntegrationTestServices.For(rename).Account.CreateOrUpdateCounty(
                cave.StateId,
                new CreateCountyVm { Name = cave.CountyName, CountyDisplayId = cave.CountyDisplayId },
                cave.CountyId, default);

        await using var verify = database.CreateDbContext("verify", cave.AccountId);
        Assert.Equal(cave.RevisionId, await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == cave.CaveId).Select(row => row.CurrentRevisionId).SingleAsync());
        Assert.False(await verify.CaveRevisions.AnyAsync(revision => revision.PreviousRevisionId == cave.RevisionId));
    }

    [Fact]
    public async Task TagRenameRollsBackLabelWhenRevisionPublicationFails()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TagRenameRollsBackLabelWhenRevisionPublicationFails));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var tag = await ReferenceTestData.AddTagAsync(
            database, tenant.AccountId, TagTypeKeyConstant.Biology, "Original Biology", "biology00a");
        await using (var seed = database.CreateDbContext("seed", tenant.AccountId))
        {
            seed.BiologyTags.Add(new BiologyTag
                { Id = IdGenerator.Generate(), CaveId = tenant.CaveId, TagTypeId = tag.Id });
            await seed.SaveChangesAsync();
        }
        var before = await PublishCurrentSnapshotAsync(
            database, tenant.AccountId, tenant.CaveId, tenant.RevisionId);
        var fault = new FailAtRevisionPublicationInterceptor();

        await using (var rename = database.CreateDbContext("manager", tenant.AccountId, fault))
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                IntegrationTestServices.For(rename).Account.CreateOrUpdateTagType(
                    new CreateEditTagTypeVm { Name = "Renamed Biology", Key = TagTypeKeyConstant.Biology },
                    tag.Id, default));

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        Assert.Equal("Original Biology",
            await verify.TagTypes.Where(row => row.Id == tag.Id).Select(row => row.Name).SingleAsync());
        Assert.Equal(before, await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == tenant.CaveId).Select(row => row.CurrentRevisionId).SingleAsync());
        Assert.Empty(await verify.CaveRevisions
            .Where(revision => revision.PreviousRevisionId == before).ToListAsync());
    }

    [Fact]
    public async Task CountyRenameRollsBackLabelsWhenRevisionPublicationFails()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CountyRenameRollsBackLabelsWhenRevisionPublicationFails));
        var tenant = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var fault = new FailAtRevisionPublicationInterceptor();

        await using (var rename = database.CreateDbContext("manager", tenant.AccountId, fault))
            await Assert.ThrowsAsync<DbUpdateException>(() =>
                IntegrationTestServices.For(rename).Account.CreateOrUpdateCounty(
                    tenant.StateId,
                    new CreateCountyVm { Name = "Renamed County", CountyDisplayId = "R99" },
                    tenant.CountyId, default));

        await using var verify = database.CreateDbContext("verify", tenant.AccountId);
        var county = await verify.Counties.SingleAsync(row => row.Id == tenant.CountyId);
        Assert.Equal(tenant.CountyName, county.Name);
        Assert.Equal(tenant.CountyDisplayId, county.DisplayId);
        Assert.Equal(tenant.RevisionId, await verify.Caves.IgnoreQueryFilters()
            .Where(row => row.Id == tenant.CaveId).Select(row => row.CurrentRevisionId).SingleAsync());
        Assert.Empty(await verify.CaveRevisions
            .Where(revision => revision.PreviousRevisionId == tenant.RevisionId).ToListAsync());
    }

    private static async Task<string> AttachReferenceAndPublishAsync(
        PostgresTestDatabase database, PublishedCaveTestData cave, string key, string tagId)
    {
        string? entranceId = null;
        if (key == TagTypeKeyConstant.LocationQuality)
        {
            entranceId = await EntranceTestData.AddEntranceAsync(
                database, cave, "entrance0a", locationQualityTagId: tagId);
        }
        else if (key is TagTypeKeyConstant.EntranceStatus or TagTypeKeyConstant.FieldIndication or
                 TagTypeKeyConstant.EntranceHydrology)
        {
            var quality = await ReferenceTestData.AddTagAsync(
                database, cave.AccountId, TagTypeKeyConstant.LocationQuality, "Matrix quality");
            entranceId = await EntranceTestData.AddEntranceAsync(
                database, cave, "entrance0a", locationQualityTagId: quality.Id);
        }

        if (key != TagTypeKeyConstant.LocationQuality)
        {
            await using var seed = database.CreateDbContext("reference-family-seed", cave.AccountId);
            var id = IdGenerator.Generate();
            switch (key)
            {
                case TagTypeKeyConstant.Geology:
                    seed.GeologyTags.Add(new GeologyTag { Id = id, CaveId = cave.CaveId, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.EntranceStatus:
                    seed.EntranceStatusTags.Add(new EntranceStatusTag
                        { Id = id, EntranceId = entranceId!, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.FieldIndication:
                    seed.FieldIndicationTags.Add(new FieldIndicationTag
                        { Id = id, EntranceId = entranceId!, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.EntranceHydrology:
                    seed.EntranceHydrologyTags.Add(new EntranceHydrologyTag
                        { Id = id, EntranceId = entranceId!, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.File:
                    seed.Files.Add(new Planarian.Model.Database.Entities.RidgeWalker.File
                    {
                        Id = "file00000a", AccountId = cave.AccountId, CaveId = cave.CaveId,
                        FileTypeTagId = tagId, Name = "Reference file", Extension = ".pdf",
                        BlobContainer = "account-" + cave.AccountId, BlobKey = "objects/reference-file"
                    });
                    break;
                case TagTypeKeyConstant.People:
                    seed.CaveReportedByNameTags.Add(new CaveReportedByNameTag
                        { Id = id, CaveId = cave.CaveId, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.Biology:
                    seed.BiologyTags.Add(new BiologyTag { Id = id, CaveId = cave.CaveId, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.Archeology:
                    seed.ArcheologyTags.Add(new ArcheologyTag { Id = id, CaveId = cave.CaveId, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.MapStatus:
                    seed.MapStatusTags.Add(new MapStatusTag { Id = id, CaveId = cave.CaveId, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.CaveOther:
                    seed.CaveOtherTags.Add(new CaveOtherTag { Id = id, CaveId = cave.CaveId, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.GeologicAge:
                    seed.GeologicAgeTags.Add(new GeologicAgeTag { Id = id, CaveId = cave.CaveId, TagTypeId = tagId });
                    break;
                case TagTypeKeyConstant.PhysiographicProvince:
                    seed.PhysiographicProvinceTags.Add(new PhysiographicProvinceTag
                        { Id = id, CaveId = cave.CaveId, TagTypeId = tagId });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }
            await seed.SaveChangesAsync();
        }

        return await PublishCurrentSnapshotAsync(
            database, cave.AccountId, cave.CaveId, cave.RevisionId);
    }

    private static async Task<string> PublishCurrentSnapshotAsync(
        PostgresTestDatabase database, string accountId, string caveId, string? previousRevisionId)
    {
        await using var db = database.CreateDbContext("publish-current", accountId);
        var snapshot = await new CavePublishedSnapshotRepository(db, db.RequestUser).BuildAsync(caveId);
        var revision = new CaveRevision
        {
            Id = IdGenerator.Generate(),
            AccountId = accountId,
            CaveId = caveId,
            PreviousRevisionId = previousRevisionId,
            Source = CaveRevisionSource.ManagerEdit,
            Operation = CaveRevisionOperation.Update,
            SnapshotSchemaVersion = 1,
            SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
        };
        db.CaveRevisions.Add(revision);
        await db.SaveChangesAsync();
        var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == caveId);
        cave.CurrentRevisionId = revision.Id;
        await db.SaveChangesAsync();
        return revision.Id;
    }
}
