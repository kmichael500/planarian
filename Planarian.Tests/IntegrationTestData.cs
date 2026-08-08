using Microsoft.EntityFrameworkCore;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared.Helpers;

namespace Planarian.Tests;

internal sealed record TenantSeed(
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

internal static class IntegrationTestData
{
    public static async Task<TenantSeed> SeedTenantAsync(PostgresTestDatabase database, char suffix)
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

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                insert into "States" ("Id", "Name", "Abbreviation", "CreatedOn")
                values (@state_id, @state_name, @state_abbr, now());

                insert into "Accounts" ("Id", "Name", "CountyIdDelimiter", "DefaultViewAccessAllCaves", "ExportEnabled", "CreatedOn")
                values (@account_id, @account_name, '-', false, true, now());

                insert into "Counties" ("Id", "AccountId", "StateId", "DisplayId", "Name", "CreatedOn")
                values (@county_id, @account_id, @state_id, @county_display, @county_name, now());

                insert into "TagTypes" ("Id", "AccountId", "Name", "Key", "IsDefault", "CreatedOn")
                values (@file_type_id, @account_id, 'Document', 'file-type', false, now());

                insert into "Caves" ("Id", "AccountId", "StateId", "CountyId", "Name", "AlternateNames", "CountyNumber", "IsArchived", "CreatedOn")
                values (@cave_id, @account_id, @state_id, @county_id, @cave_name, '[]', 1, false, now());
                """;
            command.Parameters.AddWithValue("state_id", stateId);
            command.Parameters.AddWithValue("state_name", $"State {char.ToUpperInvariant(suffix)}");
            command.Parameters.AddWithValue("state_abbr", $"{char.ToUpperInvariant(suffix)}{char.ToUpperInvariant(suffix)}");
            command.Parameters.AddWithValue("account_id", accountId);
            command.Parameters.AddWithValue("account_name", $"Account {char.ToUpperInvariant(suffix)}");
            command.Parameters.AddWithValue("county_id", countyId);
            command.Parameters.AddWithValue("county_display", $"{char.ToUpperInvariant(suffix)}01");
            command.Parameters.AddWithValue("county_name", $"County {char.ToUpperInvariant(suffix)}");
            command.Parameters.AddWithValue("file_type_id", fileTypeId);
            command.Parameters.AddWithValue("cave_id", caveId);
            command.Parameters.AddWithValue("cave_name", $"Cave {char.ToUpperInvariant(suffix)}");
            await command.ExecuteNonQueryAsync();
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
                SyncExisting = false
            });
            await db.SaveChangesAsync();
            var cave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == caveId);
            cave.CurrentRevisionId = revisionId;
            await db.SaveChangesAsync();
        }

        // Review/File fixtures are inserted using the exact persisted column
        // contract from the current migration. Keep this raw SQL intentionally
        // small so the shared seed can exercise query filters independently of
        // SaveChanges interception while still failing when the schema drifts.
        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                insert into "CaveChangeRequests" ("Id", "AccountId", "CaveId", "BaseRevisionId", "Status", "CreatedOn")
                values (@request_id, @account_id, @cave_id, @revision_id, 'Pending', now());

                insert into "CaveProposalVersions" ("Id", "AccountId", "ChangeRequestId", "SchemaVersion", "ProposalJson", "CreatedOn")
                values (@proposal_id, @account_id, @request_id, 1, @snapshot::jsonb, now());

                insert into "Files" ("Id", "AccountId", "FileTypeTagId", "FileName", "BlobKey", "BlobContainer", "CreatedOn")
                values (@file_id, @account_id, @file_type_id, @file_name, @blob_key, 'test', now());

                insert into "CaveChangeRequestStagedFiles" ("Id", "AccountId", "ChangeRequestId", "FileId", "CreatedOn")
                values (@staged_id, @account_id, @request_id, @file_id, now());
                """;
            command.Parameters.AddWithValue("request_id", changeRequestId);
            command.Parameters.AddWithValue("account_id", accountId);
            command.Parameters.AddWithValue("cave_id", caveId);
            command.Parameters.AddWithValue("revision_id", revisionId);
            command.Parameters.AddWithValue("proposal_id", proposalVersionId);
            command.Parameters.AddWithValue("snapshot", "{\"schemaVersion\":1}");
            command.Parameters.AddWithValue("file_id", fileId);
            command.Parameters.AddWithValue("file_type_id", fileTypeId);
            command.Parameters.AddWithValue("file_name", $"seed-{suffix}.pdf");
            command.Parameters.AddWithValue("blob_key", $"seed-{suffix}");
            command.Parameters.AddWithValue("staged_id", stagedFileId);
            await command.ExecuteNonQueryAsync();
        }

        return new TenantSeed(accountId, stateId, countyId, caveId, revisionId, importBatchId,
            changeRequestId, proposalVersionId, fileId, stagedFileId);
    }
}
