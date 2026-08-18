using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.Infrastructure.Data;
internal static class FileTestDataFactory
{
    public static async Task<TestFileData> AddFileAsync(PostgresTestDatabase database,
        PublishedCaveTestData cave, bool associateWithCave = false, string? fileId = null,
        string? name = null, string extension = ".pdf")
    {
        var suffix = cave.AccountId[^1];
        var fileTypeId = $"filetype0{suffix}";
        await ReferenceTestData.AddTagAsync(database, cave.AccountId, TagTypeKeyConstant.File, "Document", fileTypeId);
        fileId ??= $"file00000{suffix}";

        await using var db = database.CreateDbContext("file-seed", cave.AccountId);
        db.Files.Add(new File
        {
            Id = fileId,
            AccountId = cave.AccountId,
            CaveId = associateWithCave ? cave.CaveId : null,
            FileTypeTagId = fileTypeId,
            Name = name ?? $"seed-{suffix}",
            Extension = extension,
            BlobKey = $"seed-{suffix}",
            BlobContainer = "test"
        });
        await db.SaveChangesAsync();
        return new TestFileData(fileId, fileTypeId);
    }

}
