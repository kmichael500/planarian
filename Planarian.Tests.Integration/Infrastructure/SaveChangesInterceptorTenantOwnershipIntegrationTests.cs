using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;
using Planarian.Model.Shared.Helpers;
using Xunit;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure;

public sealed class SaveChangesInterceptorTenantOwnershipIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{

    private static readonly EntityState[] MutatingStates =
        [EntityState.Added, EntityState.Modified, EntityState.Deleted];

    private static readonly string[] DirectAccountEntityKinds =
    [
        nameof(Account),
        nameof(AccountState),
        nameof(AccountUser),
        nameof(TagType),
        nameof(Cave),
        nameof(CaveRevision),
        nameof(CaveImportBatch),
        nameof(CaveChangeRequest),
        nameof(CaveProposalVersion),
        nameof(CaveChangeRequestStagedFile),
        nameof(CaveChangeRequestReviewEvent),
        nameof(StorageCleanupTarget),
        nameof(County),
        nameof(FeatureSetting),
        "AccountOwnedFile",
        nameof(CavePermission),
        nameof(Favorite),
        nameof(UserPermission)
    ];

    private static readonly string[] CaveOwnedEntityKinds =
    [
        nameof(ArcheologyTag),
        nameof(BiologyTag),
        nameof(CartographerNameTag),
        nameof(CaveOtherTag),
        nameof(CaveReportedByNameTag),
        nameof(GeologicAgeTag),
        nameof(GeologyTag),
        nameof(MapStatusTag),
        nameof(PhysiographicProvinceTag),
        nameof(CaveGeoJson),
        nameof(Entrance),
        "CaveOwnedFile"
    ];

    private static readonly string[] EntranceOwnedEntityKinds =
    [
        nameof(EntranceHydrologyTag),
        nameof(EntranceReportedByNameTag),
        nameof(EntranceStatusTag),
        nameof(FieldIndicationTag),
        nameof(EntranceOtherTag)
    ];

    [Fact]
    public async Task SynchronousSaveChangesIsRejected()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(SynchronousSaveChangesIsRejected));
        await using var db = database.CreateDbContext("sync-save", accountId: null);

        var exception = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());

        Assert.Contains("SaveChangesAsync", exception.Message);
    }

    [Fact]
    public async Task EveryDirectAccountEntityRejectsForeignTenantForAddModifyAndDelete()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EveryDirectAccountEntityRejectsForeignTenantForAddModifyAndDelete));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var ownedFile = await FileTestDataFactory.AddFileAsync(database, accountA);

        foreach (var kind in DirectAccountEntityKinds)
        foreach (var state in MutatingStates)
        {
            await using var db = database.CreateDbContext($"direct-{kind}-{state}", accountA.AccountId);
            var entity = CreateForeignDirectEntity(kind, db.RequestUser.Id, accountA, accountB, ownedFile.FileId);
            db.Entry(entity).State = state;
            await AssertForbiddenAsync(db, kind, state);
        }
    }

    [Fact]
    public async Task EveryCaveOwnedEntityRejectsPrepopulatedForeignNavigationForAddModifyAndDelete()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EveryCaveOwnedEntityRejectsPrepopulatedForeignNavigationForAddModifyAndDelete));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');

        foreach (var kind in CaveOwnedEntityKinds)
        foreach (var state in MutatingStates)
        {
            await using var db = database.CreateDbContext($"cave-{kind}-{state}", accountA.AccountId);
            var cave = ForeignCaveStub(accountB);
            db.Entry(cave).State = EntityState.Unchanged;
            var entity = CreateForeignCaveOwnedEntity(kind, cave, accountB);
            db.Entry(entity).State = state;
            await AssertForbiddenAsync(db, kind, state);
        }
    }

    [Fact]
    public async Task EveryEntranceOwnedEntityRejectsPrepopulatedForeignNavigationForAddModifyAndDelete()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(EveryEntranceOwnedEntityRejectsPrepopulatedForeignNavigationForAddModifyAndDelete));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00b");
        const string entranceId = "entrance0b";
        await EntranceTestData.AddEntranceAsync(database, accountB, entranceId, locationQualityTagId: quality.Id);

        foreach (var kind in EntranceOwnedEntityKinds)
        foreach (var state in MutatingStates)
        {
            await using var db = database.CreateDbContext($"entrance-{kind}-{state}", accountA.AccountId);
            var cave = ForeignCaveStub(accountB);
            var entrance = new Entrance { Id = entranceId, CaveId = accountB.CaveId, Cave = cave };
            db.Entry(cave).State = EntityState.Unchanged;
            db.Entry(entrance).State = EntityState.Unchanged;
            var entity = CreateForeignEntranceOwnedEntity(kind, entrance);
            db.Entry(entity).State = state;
            await AssertForbiddenAsync(db, kind, state);
        }
    }

    [Fact]
    public async Task ForeignParentWithMissingNavigationFailsClosedForCaveAndEntranceAssociations()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ForeignParentWithMissingNavigationFailsClosedForCaveAndEntranceAssociations));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        var quality = await ReferenceTestData.AddTagAsync(database, accountB.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00b");
        const string entranceId = "entrance0b";
        await EntranceTestData.AddEntranceAsync(database, accountB, entranceId, locationQualityTagId: quality.Id);

        await using (var caveDb = database.CreateDbContext("missing-cave-nav", accountA.AccountId))
        {
            caveDb.BiologyTags.Add(new BiologyTag
                { Id = "foreignbio1", CaveId = accountB.CaveId, TagTypeId = "unusedtag1" });
            await AssertForbiddenAsync(caveDb, nameof(BiologyTag), EntityState.Added);
        }

        await using (var entranceDb = database.CreateDbContext("missing-entrance-nav", accountA.AccountId))
        {
            entranceDb.EntranceStatusTags.Add(new EntranceStatusTag
                { Id = "foreignent1", EntranceId = entranceId, TagTypeId = "unusedtag2" });
            await AssertForbiddenAsync(entranceDb, nameof(EntranceStatusTag), EntityState.Added);
        }
    }

    [Fact]
    public async Task OwnedAccountIdCannotSmuggleForeignCaveOrCountyRelationships()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(OwnedAccountIdCannotSmuggleForeignCaveOrCountyRelationships));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');

        await using (var favorite = database.CreateDbContext("favorite-smuggle", accountA.AccountId))
        {
            favorite.Favorites.Add(new Favorite
            {
                AccountId = accountA.AccountId,
                UserId = favorite.RequestUser.Id,
                CaveId = accountB.CaveId
            });
            await AssertForbiddenAsync(favorite, nameof(Favorite), EntityState.Added);
        }

        await using (var countyPermission = database.CreateDbContext("county-permission-smuggle", accountA.AccountId))
        {
            countyPermission.CavePermissions.Add(new CavePermission
            {
                AccountId = accountA.AccountId,
                UserId = countyPermission.RequestUser.Id,
                PermissionId = "permission",
                CountyId = accountB.CountyId
            });
            await AssertForbiddenAsync(countyPermission, nameof(CavePermission), EntityState.Added);
        }

        await using (var cavePermission = database.CreateDbContext("cave-permission-smuggle", accountA.AccountId))
        {
            cavePermission.CavePermissions.Add(new CavePermission
            {
                AccountId = accountA.AccountId,
                UserId = cavePermission.RequestUser.Id,
                PermissionId = "permission",
                CaveId = accountB.CaveId
            });
            await AssertForbiddenAsync(cavePermission, nameof(CavePermission), EntityState.Added);
        }

        await using (var cave = database.CreateDbContext("cave-county-smuggle", accountA.AccountId))
        {
            cave.Caves.Add(new Cave
            {
                AccountId = accountA.AccountId,
                StateId = accountA.StateId,
                CountyId = accountB.CountyId,
                Name = "Cross-tenant county",
                CountyNumber = 999
            });
            await AssertForbiddenAsync(cave, nameof(Cave), EntityState.Added);
        }
    }

    [Fact]
    public async Task OwnedCaveCannotMoveToForeignCounty()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(OwnedCaveCannotMoveToForeignCounty));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');

        await using (var attack = database.CreateDbContext("county-move-attack", accountA.AccountId))
        {
            var cave = await attack.Caves.IgnoreQueryFilters().SingleAsync(row => row.Id == accountA.CaveId);
            cave.CountyId = accountB.CountyId;
            await AssertForbiddenAsync(attack, nameof(Cave), EntityState.Modified);
        }

        await using var verify = database.CreateDbContext("county-move-verify", accountA.AccountId);
        Assert.Equal(accountA.CountyId,
            await verify.Caves.IgnoreQueryFilters().Where(row => row.Id == accountA.CaveId)
                .Select(row => row.CountyId).SingleAsync());
    }

    [Fact]
    public async Task CurrentTenantWritesSucceedAcrossEachOwnershipMechanism()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(CurrentTenantWritesSucceedAcrossEachOwnershipMechanism));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');

        await using (var direct = database.CreateDbContext("direct-owned", cave.AccountId))
        {
            var target = new StorageCleanupTarget
            {
                AccountId = cave.AccountId,
                StoragePartition = "test",
                StorageKey = "owned-one",
                Purpose = StorageCleanupPurpose.CaveAssetRemoval
            };
            direct.StorageCleanupTargets.Add(target);
            await direct.SaveChangesAsync();
            target.StorageKey = "owned-two";
            await direct.SaveChangesAsync();
            direct.StorageCleanupTargets.Remove(target);
            await direct.SaveChangesAsync();
        }

        await using (var caveOwned = database.CreateDbContext("cave-owned", cave.AccountId))
        {
            var plot = new CaveGeoJson { CaveId = cave.CaveId, Name = "Plot", GeoJson = "{}" };
            caveOwned.CaveGeoJsons.Add(plot);
            await caveOwned.SaveChangesAsync();
            plot.Name = "Plot updated";
            await caveOwned.SaveChangesAsync();
            caveOwned.CaveGeoJsons.Remove(plot);
            await caveOwned.SaveChangesAsync();
        }

        var quality = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade", "locqual00a");
        var statusA = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Open", "entstat00a");
        var statusB = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.EntranceStatus, "Restricted", "entstat00b");
        const string entranceId = "entrance0a";
        await EntranceTestData.AddEntranceAsync(database, cave, entranceId, locationQualityTagId: quality.Id);

        await using (var entranceOwned = database.CreateDbContext("entrance-owned", cave.AccountId))
        {
            var row = new EntranceStatusTag { EntranceId = entranceId, TagTypeId = statusA.Id };
            entranceOwned.EntranceStatusTags.Add(row);
            await entranceOwned.SaveChangesAsync();
            row.ModifiedOn = DateTime.UtcNow.AddMinutes(-1);
            await entranceOwned.SaveChangesAsync();
            entranceOwned.EntranceStatusTags.Remove(row);
            await entranceOwned.SaveChangesAsync();
        }

        var fileType = await ReferenceTestData.AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.File, "Document", "filetype1a");
        await using (var fileOwned = database.CreateDbContext("file-owned", cave.AccountId))
        {
            var file = new File
            {
                AccountId = cave.AccountId,
                FileTypeTagId = fileType.Id,
                Name = "temporary",
                Extension = ".pdf"
            };
            fileOwned.Files.Add(file);
            await fileOwned.SaveChangesAsync();
            file.Name = "temporary-updated";
            await fileOwned.SaveChangesAsync();
            fileOwned.Files.Remove(file);
            await fileOwned.SaveChangesAsync();
        }

        var request = await CaveChangeRequestTestDataFactory.CreateChangeRequestAsync(database, cave);
        var stagedFile = await FileTestDataFactory.AddFileAsync(database, cave, fileId: "stagefile1");
        await using (var staged = database.CreateDbContext("staged-owned", cave.AccountId))
        {
            var row = new CaveChangeRequestStagedFile
            {
                AccountId = cave.AccountId,
                ChangeRequestId = request.ChangeRequestId,
                FileId = stagedFile.FileId
            };
            staged.CaveChangeRequestStagedFiles.Add(row);
            await staged.SaveChangesAsync();
            row.ModifiedOn = DateTime.UtcNow.AddMinutes(-1);
            staged.Entry(row).State = EntityState.Modified;
            await staged.SaveChangesAsync();
            staged.CaveChangeRequestStagedFiles.Remove(row);
            await staged.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ForeignParentRelationshipCannotBeLaunderedByChangingParentBeforeSave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ForeignParentRelationshipCannotBeLaunderedByChangingParentBeforeSave));
        var accountA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var accountB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');
        const string plotId = "plot0000b1";

        await using (var seed = database.CreateDbContext("seed-b", accountB.AccountId))
        {
            seed.CaveGeoJsons.Add(new CaveGeoJson
            {
                Id = plotId,
                CaveId = accountB.CaveId,
                Name = "Foreign plot",
                GeoJson = "{}"
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext("attacker", accountA.AccountId))
        {
            var plot = await attack.CaveGeoJsons.IgnoreQueryFilters().SingleAsync(row => row.Id == plotId);
            plot.CaveId = accountA.CaveId;
            await AssertForbiddenAsync(attack, nameof(CaveGeoJson), EntityState.Modified);
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        var persisted = await verify.CaveGeoJsons.IgnoreQueryFilters().SingleAsync(row => row.Id == plotId);
        Assert.Equal(accountB.CaveId, persisted.CaveId);
    }

    [Fact]
    public async Task ForeignDirectAccountRowCannotBeLaunderedByChangingAccountIdBeforeSave()
    {
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(ForeignDirectAccountRowCannotBeLaunderedByChangingAccountIdBeforeSave));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');
        const string targetId = "cleanup00b";

        await using (var seed = database.CreateDbContext("seed-b", accountB.AccountId))
        {
            seed.StorageCleanupTargets.Add(new StorageCleanupTarget
            {
                Id = targetId,
                AccountId = accountB.AccountId,
                StoragePartition = "test",
                StorageKey = "foreign-original",
                Purpose = StorageCleanupPurpose.AccountReset
            });
            await seed.SaveChangesAsync();
        }

        await using (var attack = database.CreateDbContext("attacker", accountA.AccountId))
        {
            var target = await attack.StorageCleanupTargets.IgnoreQueryFilters()
                .SingleAsync(row => row.Id == targetId);
            target.AccountId = accountA.AccountId;
            target.StorageKey = "laundered";
            await AssertForbiddenAsync(attack, nameof(StorageCleanupTarget), EntityState.Modified);
        }

        await using var verify = database.CreateDbContext("verify-b", accountB.AccountId);
        var persisted = await verify.StorageCleanupTargets.IgnoreQueryFilters().SingleAsync(row => row.Id == targetId);
        Assert.Equal(accountB.AccountId, persisted.AccountId);
        Assert.Equal("foreign-original", persisted.StorageKey);
    }

    [Fact]
    public async Task MixedOwnedAndForeignBatchIsAtomic()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(MixedOwnedAndForeignBatchIsAtomic));
        var accountA = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        var accountB = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');

        await using (var db = database.CreateDbContext("mixed-batch", accountA.AccountId))
        {
            db.StorageCleanupTargets.AddRange(
                new StorageCleanupTarget
                {
                    Id = "cleanup00a",
                    AccountId = accountA.AccountId,
                    StoragePartition = "test",
                    StorageKey = "owned",
                    Purpose = StorageCleanupPurpose.AccountReset
                },
                new StorageCleanupTarget
                {
                    Id = "cleanup00b",
                    AccountId = accountB.AccountId,
                    StoragePartition = "test",
                    StorageKey = "foreign",
                    Purpose = StorageCleanupPurpose.AccountReset
                });

            await AssertForbiddenAsync(db, nameof(StorageCleanupTarget), EntityState.Added);
        }

        await using var verify = database.CreateDbContext("verify", accountA.AccountId);
        Assert.False(await verify.StorageCleanupTargets.IgnoreQueryFilters()
            .AnyAsync(row => row.Id == "cleanup00a" || row.Id == "cleanup00b"));
    }

    private static async Task AssertForbiddenAsync(
        Microsoft.EntityFrameworkCore.DbContext db, string kind, EntityState state)
    {
        var exception = await Assert.ThrowsAsync<ApiException>(() => db.SaveChangesAsync());
        Assert.True(exception.StatusCode == 403,
            $"{kind} in state {state} should fail with 403, but returned {exception.StatusCode}.");
    }

    private static Cave ForeignCaveStub(PublishedCaveTestData cave) => new()
    {
        Id = cave.CaveId,
        AccountId = cave.AccountId,
        StateId = cave.StateId,
        CountyId = cave.CountyId,
        Name = cave.CaveName,
        CountyNumber = cave.CountyNumber
    };

    private static EntityBase CreateForeignDirectEntity(
        string kind,
        string requestUserId,
        PublishedCaveTestData accountA,
        PublishedCaveTestData accountB,
        string ownedFileId) =>
        kind switch
        {
            nameof(Account) => new Account { Id = accountB.AccountId, Name = "Foreign" },
            nameof(AccountState) => new AccountState
                { AccountId = accountB.AccountId, StateId = accountB.StateId },
            nameof(AccountUser) => new AccountUser
                { AccountId = accountB.AccountId, UserId = requestUserId },
            nameof(TagType) => new TagType("Foreign", TagTypeKeyConstant.Biology)
                { AccountId = accountB.AccountId, IsDefault = false },
            nameof(Cave) => new Cave
            {
                AccountId = accountB.AccountId, StateId = accountB.StateId, CountyId = accountB.CountyId,
                Name = "Foreign", CountyNumber = 1
            },
            nameof(CaveRevision) => new CaveRevision
            {
                AccountId = accountB.AccountId, CaveId = accountB.CaveId,
                Source = CaveRevisionSource.ManagerEdit, Operation = CaveRevisionOperation.Update,
                SnapshotSchemaVersion = 1, SnapshotJson = "{}"
            },
            nameof(CaveImportBatch) => new CaveImportBatch
            {
                AccountId = accountB.AccountId, Kind = CaveImportKind.CaveCsv
            },
            nameof(CaveChangeRequest) => new CaveChangeRequest
            {
                AccountId = accountB.AccountId, CaveId = accountB.CaveId,
                BaseRevisionId = accountB.RevisionId
            },
            nameof(CaveProposalVersion) => new CaveProposalVersion
            {
                AccountId = accountB.AccountId, ChangeRequestId = "request00b", CaveId = accountB.CaveId,
                BaseRevisionId = accountB.RevisionId, ProposalJson = "{}"
            },
            nameof(CaveChangeRequestStagedFile) => new CaveChangeRequestStagedFile
            {
                AccountId = accountB.AccountId, ChangeRequestId = "request00b", FileId = ownedFileId
            },
            nameof(CaveChangeRequestReviewEvent) => new CaveChangeRequestReviewEvent
            {
                AccountId = accountB.AccountId, ChangeRequestId = "request00b",
                ProposalVersionId = "proposal0b", ActorUserId = requestUserId,
                Action = CaveChangeRequestReviewAction.ChangesRequested
            },
            nameof(StorageCleanupTarget) => new StorageCleanupTarget
            {
                AccountId = accountB.AccountId, StoragePartition = "test", StorageKey = "foreign",
                Purpose = StorageCleanupPurpose.CaveAssetRemoval
            },
            nameof(County) => new County
            {
                AccountId = accountB.AccountId, StateId = accountB.StateId,
                Name = "Foreign", DisplayId = "ZZZ"
            },
            nameof(FeatureSetting) => new FeatureSetting
                { AccountId = accountB.AccountId, Key = FeatureKey.EnabledFieldCaveName, IsEnabled = true },
            "AccountOwnedFile" => new File
            {
                AccountId = accountB.AccountId, FileTypeTagId = "filetype0b",
                Name = "foreign", Extension = ".pdf"
            },
            nameof(CavePermission) => new CavePermission
            {
                AccountId = accountB.AccountId, UserId = requestUserId, PermissionId = "permission"
            },
            nameof(Favorite) => new Favorite
            {
                AccountId = accountB.AccountId, UserId = requestUserId
            },
            nameof(UserPermission) => new UserPermission
            {
                AccountId = accountB.AccountId, UserId = requestUserId, PermissionId = "permission"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static EntityBase CreateForeignCaveOwnedEntity(
        string kind, Cave cave, PublishedCaveTestData accountB) =>
        kind switch
        {
            nameof(ArcheologyTag) => new ArcheologyTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(BiologyTag) => new BiologyTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(CartographerNameTag) => new CartographerNameTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(CaveOtherTag) => new CaveOtherTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(CaveReportedByNameTag) => new CaveReportedByNameTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(GeologicAgeTag) => new GeologicAgeTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(GeologyTag) => new GeologyTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(MapStatusTag) => new MapStatusTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(PhysiographicProvinceTag) => new PhysiographicProvinceTag
                { CaveId = cave.Id, Cave = cave, TagTypeId = "tag0000001" },
            nameof(CaveGeoJson) => new CaveGeoJson
                { CaveId = cave.Id, Cave = cave, Name = "Foreign", GeoJson = "{}" },
            nameof(Entrance) => new Entrance
            {
                CaveId = cave.Id, Cave = cave, LocationQualityTagId = "quality001",
                Location = new Point(new CoordinateZ(-86, 35, 500)) { SRID = 4326 }
            },
            "CaveOwnedFile" => new File
            {
                AccountId = accountB.AccountId, CaveId = cave.Id, Cave = cave,
                FileTypeTagId = "filetype0b", Name = "foreign", Extension = ".pdf"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static EntityBase CreateForeignEntranceOwnedEntity(string kind, Entrance entrance) =>
        kind switch
        {
            nameof(EntranceHydrologyTag) => new EntranceHydrologyTag
                { EntranceId = entrance.Id, Entrance = entrance, TagTypeId = "tag0000001" },
            nameof(EntranceReportedByNameTag) => new EntranceReportedByNameTag
                { EntranceId = entrance.Id, Entrance = entrance, TagTypeId = "tag0000001" },
            nameof(EntranceStatusTag) => new EntranceStatusTag
                { EntranceId = entrance.Id, Entrance = entrance, TagTypeId = "tag0000001" },
            nameof(FieldIndicationTag) => new FieldIndicationTag
                { EntranceId = entrance.Id, Entrance = entrance, TagTypeId = "tag0000001" },
            nameof(EntranceOtherTag) => new EntranceOtherTag
                { EntranceId = entrance.Id, Entrance = entrance, TagTypeId = "tag0000001" },
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
