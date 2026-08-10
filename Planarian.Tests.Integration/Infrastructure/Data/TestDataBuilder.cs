using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests;

internal sealed record AccountCountyTestData(
    string AccountId,
    string StateId,
    string StateName,
    string StateAbbreviation,
    string CountyId,
    string CountyName,
    string CountyDisplayId);

internal sealed record CaveTestData(
    string AccountId,
    string StateId,
    string StateName,
    string StateAbbreviation,
    string CountyId,
    string CountyName,
    string CountyDisplayId,
    string CaveId,
    string CaveName,
    int CountyNumber);

internal sealed record PublishedCaveTestData(
    string AccountId,
    string StateId,
    string StateName,
    string StateAbbreviation,
    string CountyId,
    string CountyName,
    string CountyDisplayId,
    string CaveId,
    string CaveName,
    int CountyNumber,
    string RevisionId);

internal sealed record TestFileData(string FileId, string FileTypeId);
internal sealed record PendingChangeRequestTestData(string ChangeRequestId, string? ProposalVersionId = null);
internal sealed record PendingReviewWithStagedFileTestData(
    string ChangeRequestId,
    string ProposalVersionId,
    string FileId,
    string StagedFileId);

/// <summary>
/// Small typed test-data primitives. Compositions create only the domain state
/// named by the method; review, file, and import-batch rows are always explicit.
/// </summary>
internal static class TestDataBuilder
{
    public static async Task<AccountCountyTestData> CreateAccountWithCountyAsync(
        PostgresTestDatabase database,
        char suffix,
        bool addAccountState = false)
    {
        var upper = char.ToUpperInvariant(suffix);
        var data = new AccountCountyTestData(
            $"acct00000{suffix}",
            $"state0000{suffix}",
            $"State {upper}",
            $"{upper}{upper}",
            $"county000{suffix}",
            $"County {upper}",
            $"{upper}01");

        await GlobalStateTestData.AddAsync(database, data.StateId, data.StateName, data.StateAbbreviation);

        await using var db = database.CreateDbContext($"user-{suffix}", data.AccountId);
        db.Accounts.Add(new Account
        {
            Id = data.AccountId,
            Name = $"Account {upper}",
            CountyIdDelimiter = "-",
            ExportEnabled = true
        });
        db.Counties.Add(new County
        {
            Id = data.CountyId,
            AccountId = data.AccountId,
            StateId = data.StateId,
            DisplayId = data.CountyDisplayId,
            Name = data.CountyName
        });
        if (addAccountState)
        {
            db.AccountStates.Add(new AccountState
            {
                Id = $"acctstate{suffix}",
                AccountId = data.AccountId,
                StateId = data.StateId
            });
        }
        await db.SaveChangesAsync();
        return data;
    }

    public static async Task<CaveTestData> AddCaveAsync(
        PostgresTestDatabase database,
        AccountCountyTestData tenant,
        string? caveId = null,
        string? name = null,
        int countyNumber = 1)
    {
        caveId ??= $"cave{tenant.AccountId[^1]}00000";
        name ??= $"Cave {tenant.StateAbbreviation[0]}";
        var data = new CaveTestData(
            tenant.AccountId, tenant.StateId, tenant.StateName, tenant.StateAbbreviation,
            tenant.CountyId, tenant.CountyName, tenant.CountyDisplayId,
            caveId, name, countyNumber);

        await using var db = database.CreateDbContext("cave-seed", tenant.AccountId);
        db.Caves.Add(new Cave
        {
            Id = data.CaveId,
            AccountId = data.AccountId,
            StateId = data.StateId,
            CountyId = data.CountyId,
            Name = data.CaveName,
            CountyNumber = data.CountyNumber,
            IsArchived = false
        });
        await db.SaveChangesAsync();
        return data;
    }

    public static Task<CaveTestData> AddCaveAsync(
        PostgresTestDatabase database,
        PublishedCaveTestData tenant,
        string? caveId = null,
        string? name = null,
        int countyNumber = 1) =>
        AddCaveAsync(database, new AccountCountyTestData(
            tenant.AccountId, tenant.StateId, tenant.StateName, tenant.StateAbbreviation,
            tenant.CountyId, tenant.CountyName, tenant.CountyDisplayId), caveId, name, countyNumber);

    public static async Task<PublishedCaveTestData> PublishBaselineRevisionAsync(
        PostgresTestDatabase database,
        CaveTestData cave,
        string? revisionId = null)
    {
        revisionId ??= $"revision0{cave.AccountId[^1]}";
        var snapshot = new CavePublishedSnapshotV1
        {
            CaveId = cave.CaveId,
            AccountId = cave.AccountId,
            Name = cave.CaveName,
            State = new SnapshotReference(cave.StateId, cave.StateName, null, cave.StateAbbreviation),
            County = new SnapshotReference(cave.CountyId, cave.CountyName, cave.CountyDisplayId),
            CountyNumber = cave.CountyNumber
        };

        await using var db = database.CreateDbContext("revision-seed", cave.AccountId);
        db.CaveRevisions.Add(new CaveRevision
        {
            Id = revisionId,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            Source = CaveRevisionSource.SystemBaseline,
            Operation = CaveRevisionOperation.Create,
            SnapshotSchemaVersion = 1,
            SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
        });
        await db.SaveChangesAsync();

        var persisted = await db.Caves.IgnoreQueryFilters().SingleAsync(candidate => candidate.Id == cave.CaveId);
        persisted.CurrentRevisionId = revisionId;
        await db.SaveChangesAsync();

        return new PublishedCaveTestData(
            cave.AccountId, cave.StateId, cave.StateName, cave.StateAbbreviation,
            cave.CountyId, cave.CountyName, cave.CountyDisplayId,
            cave.CaveId, cave.CaveName, cave.CountyNumber, revisionId);
    }

    public static async Task<PublishedCaveTestData> CreatePublishedCaveAsync(
        PostgresTestDatabase database,
        char suffix)
    {
        var tenant = await CreateAccountWithCountyAsync(database, suffix);
        var cave = await AddCaveAsync(database, tenant, $"cave00000{suffix}");
        return await PublishBaselineRevisionAsync(database, cave);
    }

    public static async Task<string> AddAccountStateAsync(PostgresTestDatabase database, AccountCountyTestData tenant)
    {
        var id = $"acctstate{tenant.AccountId[^1]}";
        await using var db = database.CreateDbContext("account-state-seed", tenant.AccountId);
        db.AccountStates.Add(new AccountState { Id = id, AccountId = tenant.AccountId, StateId = tenant.StateId });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<TagType> AddTagAsync(PostgresTestDatabase database, string accountId, string key,
        string name, string? id = null, bool isDefault = false)
    {
        var tag = new TagType(name, key)
        {
            Id = id ?? IdGenerator.Generate(),
            AccountId = isDefault ? null : accountId,
            IsDefault = isDefault
        };
        await using var db = database.CreateDbContext("tag-seed", accountId);
        db.TagTypes.Add(tag);
        await db.SaveChangesAsync();
        return tag;
    }

    public static async Task<string> AddEntranceAsync(PostgresTestDatabase database, PublishedCaveTestData cave,
        string entranceId, bool isPrimary = true, string? locationQualityTagId = null,
        string? entranceStatusTagId = null)
    {
        locationQualityTagId ??= (await AddTagAsync(database, cave.AccountId,
            TagTypeKeyConstant.LocationQuality, "Survey Grade")).Id;
        await using var db = database.CreateDbContext("entrance-seed", cave.AccountId);
        db.Entrances.Add(new Entrance
        {
            Id = entranceId,
            CaveId = cave.CaveId,
            LocationQualityTagId = locationQualityTagId,
            IsPrimary = isPrimary,
            Location = new Point(new CoordinateZ(-86, 35, 500)) { SRID = 4326 }
        });
        if (entranceStatusTagId is not null)
        {
            db.EntranceStatusTags.Add(new EntranceStatusTag
            {
                Id = IdGenerator.Generate(),
                EntranceId = entranceId,
                TagTypeId = entranceStatusTagId
            });
        }
        await db.SaveChangesAsync();
        return entranceId;
    }

    public static async Task<TestFileData> AddFileAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, bool associateWithCave = false, string? fileId = null)
    {
        var suffix = cave.AccountId[^1];
        var fileTypeId = $"filetype0{suffix}";
        await AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File, "Document", fileTypeId);
        fileId ??= $"file00000{suffix}";

        await using var db = database.CreateDbContext("file-seed", cave.AccountId);
        db.Files.Add(new File
        {
            Id = fileId,
            AccountId = cave.AccountId,
            CaveId = associateWithCave ? cave.CaveId : null,
            FileTypeTagId = fileTypeId,
            FileName = $"seed-{suffix}.pdf",
            BlobKey = $"seed-{suffix}",
            BlobContainer = "test"
        });
        await db.SaveChangesAsync();
        return new TestFileData(fileId, fileTypeId);
    }

    public static async Task<PendingChangeRequestTestData> CreateChangeRequestAsync(
        PostgresTestDatabase database,
        PublishedCaveTestData cave,
        bool addProposalVersion = false)
    {
        var suffix = cave.AccountId[^1];
        var requestId = $"request00{suffix}";
        await using (var db = database.CreateDbContext("request-seed", cave.AccountId))
        {
            db.CaveChangeRequests.Add(new CaveChangeRequest
            {
                Id = requestId,
                AccountId = cave.AccountId,
                CaveId = cave.CaveId,
                BaseRevisionId = cave.RevisionId,
                Status = CaveChangeRequestStatus.Pending
            });
            await db.SaveChangesAsync();
        }

        var proposalId = addProposalVersion
            ? await AddProposalVersionAsync(database, cave.AccountId, requestId)
            : null;
        return new PendingChangeRequestTestData(requestId, proposalId);
    }

    public static async Task<string> AddProposalVersionAsync(PostgresTestDatabase database, string accountId,
        string changeRequestId)
    {
        var id = $"proposal0{accountId[^1]}";
        await using var db = database.CreateDbContext("proposal-seed", accountId);
        db.CaveProposalVersions.Add(new CaveProposalVersion
        {
            Id = id,
            AccountId = accountId,
            ChangeRequestId = changeRequestId,
            SchemaVersion = 1,
            ProposalJson = "{\"schemaVersion\":1}"
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<string> StageFileAsync(PostgresTestDatabase database, string accountId,
        string changeRequestId, string fileId)
    {
        var id = $"staged000{accountId[^1]}";
        await using var db = database.CreateDbContext("staged-file-seed", accountId);
        db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile
        {
            Id = id,
            AccountId = accountId,
            ChangeRequestId = changeRequestId,
            FileId = fileId
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<PendingReviewWithStagedFileTestData> CreatePendingReviewWithStagedFileAsync(
        PostgresTestDatabase database,
        PublishedCaveTestData cave)
    {
        var request = await CreateChangeRequestAsync(database, cave, addProposalVersion: true);
        var file = await AddFileAsync(database, cave);
        var stagedFileId = await StageFileAsync(database, cave.AccountId, request.ChangeRequestId, file.FileId);
        return new PendingReviewWithStagedFileTestData(
            request.ChangeRequestId,
            request.ProposalVersionId!,
            file.FileId,
            stagedFileId);
    }

    public static async Task<string> AddImportBatchAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave)
    {
        var id = $"batch0000{cave.AccountId[^1]}";
        await using var db = database.CreateDbContext("batch-seed", cave.AccountId);
        db.CaveImportBatches.Add(new CaveImportBatch
        {
            Id = id,
            AccountId = cave.AccountId,
            SourceFileName = $"seed-{cave.AccountId[^1]}.csv",
            SyncExisting = false,
            Kind = CaveImportKind.CaveCsv,
            SourceRecordCount = 1
        });
        await db.SaveChangesAsync();
        return id;
    }
}
