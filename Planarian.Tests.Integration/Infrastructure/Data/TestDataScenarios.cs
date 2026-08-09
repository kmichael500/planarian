using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Tests;

internal sealed record PublishedCaveScenario(
    string AccountId,
    string StateId,
    string CountyId,
    string CaveId,
    string RevisionId,
    string ImportBatchId,
    string ChangeRequestId,
    string ProposalVersionId,
    string FileId,
    string StagedFileId);

internal static class TestDataScenarios
{
    public static async Task<PublishedCaveScenario> CreatePublishedCaveScenarioAsync(PostgresTestDatabase database, char suffix)
    {
        var accountId = $"acct00000{suffix}";
        var stateId = $"state0000{suffix}";
        var countyId = $"county000{suffix}";
        var caveId = $"cave00000{suffix}";
        var revisionId = $"revision0{suffix}";
        var importBatchId = $"batch0000{suffix}";
        var changeRequestId = $"request00{suffix}";
        var proposalVersionId = $"proposal0{suffix}";
        var fileId = $"file00000{suffix}";
        var stagedFileId = $"staged000{suffix}";
        var fileTypeId = $"filetype0{suffix}";

        await using (var db = database.CreateDbContext($"state-seed-{suffix}", null))
        {
            // State is a protected global lookup: SaveChangesInterceptor rejects
            // every State mutation, including fixture setup. Keep this one narrow
            // exception here; all tenant and workflow data below uses typed EF.
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                insert into "States" ("Id", "Name", "Abbreviation", "CreatedOn")
                values ({stateId}, {$"State {char.ToUpperInvariant(suffix)}"},
                    {$"{char.ToUpperInvariant(suffix)}{char.ToUpperInvariant(suffix)}"}, now())
                """);
        }

        await using (var db = database.CreateDbContext($"user-{suffix}", accountId))
        {
            db.Accounts.Add(new Account { Id = accountId, Name = $"Account {char.ToUpperInvariant(suffix)}",
                CountyIdDelimiter = "-", ExportEnabled = true });
            db.Counties.Add(new County { Id = countyId, AccountId = accountId, StateId = stateId,
                DisplayId = $"{char.ToUpperInvariant(suffix)}01", Name = $"County {char.ToUpperInvariant(suffix)}" });
            db.TagTypes.Add(new TagType("Document", TagTypeKeyConstant.File)
                { Id = fileTypeId, AccountId = accountId, IsDefault = false });
            db.Caves.Add(new Cave { Id = caveId, AccountId = accountId, StateId = stateId, CountyId = countyId,
                Name = $"Cave {char.ToUpperInvariant(suffix)}", CountyNumber = 1, IsArchived = false });
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext($"user-{suffix}", accountId))
        {
            var snapshot = new CavePublishedSnapshotV1
            {
                CaveId = caveId,
                AccountId = accountId,
                Name = $"Cave {char.ToUpperInvariant(suffix)}",
                State = new SnapshotReference(stateId, $"State {char.ToUpperInvariant(suffix)}", null,
                    $"{char.ToUpperInvariant(suffix)}{char.ToUpperInvariant(suffix)}"),
                County = new SnapshotReference(countyId, $"County {char.ToUpperInvariant(suffix)}",
                    $"{char.ToUpperInvariant(suffix)}01"),
                CountyNumber = 1
            };
            db.CaveRevisions.Add(new CaveRevision
            {
                Id = revisionId,
                AccountId = accountId,
                CaveId = caveId,
                Source = CaveRevisionSource.SystemBaseline,
                Operation = CaveRevisionOperation.Create,
                SnapshotSchemaVersion = 1,
                SnapshotJson = CaveSnapshotJson.Serialize(snapshot)
            });
            db.CaveImportBatches.Add(new CaveImportBatch
            {
                Id = importBatchId,
                AccountId = accountId,
                SourceFileName = $"seed-{suffix}.csv",
                SyncExisting = false,
                Kind = CaveImportKind.CaveCsv,
                SourceRecordCount = 1
            });
            await db.SaveChangesAsync();
            var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == caveId);
            cave.CurrentRevisionId = revisionId;
            await db.SaveChangesAsync();
        }

        await using (var db = database.CreateDbContext($"user-{suffix}", accountId))
        {
            db.CaveChangeRequests.Add(new CaveChangeRequest { Id = changeRequestId, AccountId = accountId,
                CaveId = caveId, BaseRevisionId = revisionId, Status = CaveChangeRequestStatus.Pending });
            await db.SaveChangesAsync();
            db.CaveProposalVersions.Add(new CaveProposalVersion { Id = proposalVersionId, AccountId = accountId,
                ChangeRequestId = changeRequestId, SchemaVersion = 1, ProposalJson = "{\"schemaVersion\":1}" });
            db.Files.Add(new Planarian.Model.Database.Entities.RidgeWalker.File { Id = fileId, AccountId = accountId,
                FileTypeTagId = fileTypeId, FileName = $"seed-{suffix}.pdf", BlobKey = $"seed-{suffix}",
                BlobContainer = "test" });
            await db.SaveChangesAsync();
            db.CaveChangeRequestStagedFiles.Add(new CaveChangeRequestStagedFile { Id = stagedFileId,
                AccountId = accountId, ChangeRequestId = changeRequestId, FileId = fileId });
            await db.SaveChangesAsync();
        }

        return new PublishedCaveScenario(accountId, stateId, countyId, caveId, revisionId, importBatchId,
            changeRequestId, proposalVersionId, fileId, stagedFileId);
    }
}
