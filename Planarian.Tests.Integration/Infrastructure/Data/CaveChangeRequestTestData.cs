using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure.Data;
internal static class CaveChangeRequestTestDataFactory
{
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
            ? await AddProposalVersionAsync(database, cave.AccountId, requestId, cave.CaveId, cave.RevisionId)
            : null;
        return new PendingChangeRequestTestData(requestId, proposalId);
    }

    public static async Task<string> AddProposalVersionAsync(PostgresTestDatabase database, string accountId,
        string changeRequestId, string caveId, string baseRevisionId)
    {
        var id = $"proposal0{accountId[^1]}";
        await using var db = database.CreateDbContext("proposal-seed", accountId);
        db.CaveProposalVersions.Add(new CaveProposalVersion
        {
            Id = id,
            AccountId = accountId,
            ChangeRequestId = changeRequestId,
            CaveId = caveId,
            BaseRevisionId = baseRevisionId,
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
        var file = await FileTestDataFactory.AddFileAsync(database, cave);
        var stagedFileId = await StageFileAsync(database, cave.AccountId, request.ChangeRequestId, file.FileId);
        return new PendingReviewWithStagedFileTestData(
            request.ChangeRequestId,
            request.ProposalVersionId!,
            file.FileId,
            stagedFileId);
    }

}
