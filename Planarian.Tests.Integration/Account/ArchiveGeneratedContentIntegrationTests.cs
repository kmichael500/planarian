using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using Azure.Storage.Blobs;
using CsvHelper;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Archive.Services;
using Planarian.Modules.Account.Repositories;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

namespace Planarian.Tests.Integration.AccountArchive;

public sealed class ArchiveGeneratedContentIntegrationTests(
    PostgresTestServer postgres,
    AzuriteTestServer azurite)
    : IClassFixture<PostgresTestServer>, IClassFixture<AzuriteTestServer>
{
    [Fact]
    public async Task WriteArchiveProducesCompleteCurrentTenantSafeArchiveWithExactPayloads()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(WriteArchiveProducesCompleteCurrentTenantSafeArchiveWithExactPayloads));
        var caveA = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var second = await CaveTestDataFactory.AddCaveAsync(
            database, caveA, caveId: "caveA00002", name: "Álpha / Unicode Cave", countyNumber: 2);
        var caveA2 = await CaveTestDataFactory.PublishBaselineRevisionAsync(
            database, second, revisionId: "revisionA2");
        var empty = await CaveTestDataFactory.AddCaveAsync(
            database, caveA, caveId: "caveA00003", name: "Empty Cave", countyNumber: 3);
        await CaveTestDataFactory.PublishBaselineRevisionAsync(database, empty, revisionId: "revisionA3");
        var caveB = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'b');

        var documentTag = await ReferenceTestData.AddTagAsync(
            database, caveA.AccountId, TagTypeKeyConstant.File, "Document", "filetag001");
        var unsafeFolderTag = await ReferenceTestData.AddTagAsync(
            database, caveA.AccountId, TagTypeKeyConstant.File, "..", "filetag002");
        var geology = await ReferenceTestData.AddTagAsync(
            database, caveA.AccountId, TagTypeKeyConstant.Geology, "Limestone", "geology001");
        var biology = await ReferenceTestData.AddTagAsync(
            database, caveA.AccountId, TagTypeKeyConstant.Biology, "Bats 🦇", "biology001");
        var mapStatus = await ReferenceTestData.AddTagAsync(
            database, caveA.AccountId, TagTypeKeyConstant.MapStatus, "Complete", "mapstat001");
        var caveOther = await ReferenceTestData.AddTagAsync(
            database, caveA.AccountId, TagTypeKeyConstant.CaveOther, "Special, Tag", "other00001");

        var entranceId = await EntranceTestData.AddEntranceAsync(
            database, caveA, "entranceA1");
        var entrance2Id = await EntranceTestData.AddEntranceAsync(
            database, caveA2, "entranceA2", isPrimary: false);

        var payloads = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["objects/zero"] = [],
            ["objects/unicode"] = Encoding.UTF8.GetBytes("hello π cave\n"),
            ["objects/binary"] = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray(),
            ["objects/pdf"] = Encoding.ASCII.GetBytes("%PDF-1.7\nnot-a-real-pdf-but-byte-exact\n"),
            ["objects/large"] = Enumerable.Range(
                    0,
                    checked((int)ExportService.ArchiveFileMemoryBufferThresholdBytes + 65_537))
                .Select(i => (byte)(i * 31))
                .ToArray(),
            ["objects/dup-a"] = [1, 3, 3, 7],
            ["objects/dup-b"] = [2, 4, 6, 8],
            ["objects/dot-folder"] = [9, 9, 9],
        };

        await using (var db = database.CreateDbContext("archive-seed", caveA.AccountId))
        {
            var firstCave = await db.Caves.IgnoreQueryFilters().SingleAsync(c => c.Id == caveA.CaveId);
            firstCave.Name = "Cave: One / Primary";
            firstCave.SetAlternateNamesList(["Alt One", "Ált Two"]);
            firstCave.Narrative = "Current narrative, with commas and Unicode π.";
            firstCave.LengthFeet = 1234.5;
            firstCave.DepthFeet = 222;
            firstCave.MaxPitDepthFeet = 88;
            firstCave.NumberOfPits = 4;

            var entrance1 = await db.Entrances.SingleAsync(e => e.Id == entranceId);
            entrance1.Name = "Main / Entrance";
            entrance1.Description = "Current entrance description";
            entrance1.PitDepthFeet = 42;
            entrance1.ReportedOn = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
            var entrance2 = await db.Entrances.SingleAsync(e => e.Id == entrance2Id);
            entrance2.Name = "Éntrance #2";
            entrance2.Description = "Second entrance";

            db.GeologyTags.Add(new GeologyTag { CaveId = caveA.CaveId, TagTypeId = geology.Id });
            db.BiologyTags.Add(new BiologyTag { CaveId = caveA.CaveId, TagTypeId = biology.Id });
            db.MapStatusTags.Add(new MapStatusTag { CaveId = caveA.CaveId, TagTypeId = mapStatus.Id });
            db.CaveOtherTags.Add(new CaveOtherTag { CaveId = caveA.CaveId, TagTypeId = caveOther.Id });

            AddFile(db, caveA, documentTag.Id, "file000001", "zero", ".bin", "objects/zero");
            AddFile(db, caveA, documentTag.Id, "file000002", "résumé.v2 final", ".txt", "objects/unicode");
            AddFile(db, caveA, documentTag.Id, "file000003", "raw-data", ".bin", "objects/binary");
            AddFile(db, caveA, documentTag.Id, "file000004", "map.final", ".pdf", "objects/pdf");
            AddFile(db, caveA, documentTag.Id, "file000005", "large payload", ".dat", "objects/large");
            AddFile(db, caveA, documentTag.Id, "file000006", "duplicate", ".txt", "objects/dup-a");
            AddFile(db, caveA, documentTag.Id, "file000007", "duplicate", ".txt", "objects/dup-b");
            AddFile(db, caveA, unsafeFolderTag.Id, "file000008", "dot-folder", ".dat", "objects/dot-folder");
            AddFile(db, caveA2, documentTag.Id, "file000009", "missing source", ".pdf", "objects/missing");
            db.Files.AddRange(
                new File
                {
                    Id = "file000010", AccountId = caveA.AccountId, CaveId = caveA.CaveId,
                    FileTypeTagId = documentTag.Id, Name = "missing blob key", Extension = ".txt",
                    BlobContainer = db.RequestUser.AccountContainerName, BlobKey = null
                },
                new File
                {
                    Id = "file000011", AccountId = caveA.AccountId, CaveId = caveA.CaveId,
                    FileTypeTagId = documentTag.Id, Name = "foreign locator", Extension = ".bin",
                    BlobContainer = "account-acct00000b", BlobKey = "objects/mismatch"
                },
                new File
                {
                    Id = "file000012", AccountId = caveA.AccountId, CaveId = caveA.CaveId,
                    FileTypeTagId = documentTag.Id, Name = "missing container", Extension = ".bin",
                    BlobContainer = null, BlobKey = "objects/containerless"
                });

            // Current line plots, including a collision that must be resolved safely.
            db.CaveGeoJsons.AddRange(
                new CaveGeoJson
                {
                    Id = "lineplot01", CaveId = caveA.CaveId, Name = "Survey / Main",
                    GeoJson = "{\"type\":\"FeatureCollection\",\"features\":[{\"id\":1}]}"
                },
                new CaveGeoJson
                {
                    Id = "lineplot02", CaveId = caveA.CaveId, Name = "Survey / Main",
                    GeoJson = "{\"type\":\"FeatureCollection\",\"features\":[{\"id\":2}]}"
                });

            // Unpublished/staged File: an object may exist, but no current Cave owns it.
            db.Files.Add(new File
            {
                Id = "staged0001", AccountId = caveA.AccountId, CaveId = null,
                FileTypeTagId = documentTag.Id, Name = "staged-only", Extension = ".pdf",
                BlobContainer = db.RequestUser.AccountContainerName, BlobKey = "objects/staged",
                ExpiresOn = DateTime.UtcNow.AddHours(1)
            });
            db.StorageCleanupTargets.AddRange(
                new StorageCleanupTarget
                {
                    Id = "cleanup001",
                    AccountId = caveA.AccountId,
                    StoragePartition = db.RequestUser.AccountContainerName,
                    StorageKey = "objects/rejected-proposal",
                    Purpose = StorageCleanupPurpose.UnpublishedFileRemoval
                },
                new StorageCleanupTarget
                {
                    Id = "cleanup002",
                    AccountId = caveA.AccountId,
                    StoragePartition = db.RequestUser.AccountContainerName,
                    StorageKey = "objects/removed-historical",
                    Purpose = StorageCleanupPurpose.CaveAssetRemoval
                });

            await db.SaveChangesAsync();
        }

        // Same human filename in another account must never leak into A's archive.
        var otherFile = await FileTestDataFactory.AddFileAsync(
            database, caveB, associateWithCave: true, fileId: "fileB00001",
            name: "duplicate", extension: ".txt");
        await using (var dbB = database.CreateDbContext("archive-b", caveB.AccountId))
        {
            var row = await dbB.Files.IgnoreQueryFilters().SingleAsync(f => f.Id == otherFile.FileId);
            row.BlobContainer = dbB.RequestUser.AccountContainerName;
            row.BlobKey = "objects/dup-a";
            await dbB.SaveChangesAsync();
        }

        var blobs = new BlobServiceClient(azurite.ConnectionString);
        var containerAName = AccountContainerName(database, caveA.AccountId);
        var containerBName = AccountContainerName(database, caveB.AccountId);
        var containerA = blobs.GetBlobContainerClient(containerAName);
        var containerB = blobs.GetBlobContainerClient(containerBName);
        await containerA.CreateIfNotExistsAsync();
        await containerB.CreateIfNotExistsAsync();

        foreach (var (key, bytes) in payloads)
            await containerA.GetBlobClient(key).UploadAsync(new BinaryData(bytes), overwrite: true);
        await containerA.GetBlobClient("objects/staged").UploadAsync(new BinaryData([7, 7, 7]), overwrite: true);
        await containerA.GetBlobClient("objects/removed-historical").UploadAsync(new BinaryData([6, 6, 6]), overwrite: true);
        await containerA.GetBlobClient("objects/rejected-proposal").UploadAsync(new BinaryData([5, 5, 5]), overwrite: true);
        await containerA.GetBlobClient("objects/mismatch").UploadAsync(new BinaryData([42, 42]), overwrite: true);
        await containerA.GetBlobClient("objects/containerless").UploadAsync(new BinaryData([33, 33]), overwrite: true);
        await containerB.GetBlobClient("objects/dup-a").UploadAsync(new BinaryData([99, 98, 97]), overwrite: true);
        await containerB.GetBlobClient("objects/mismatch").UploadAsync(new BinaryData([88, 88]), overwrite: true);

        await using var exportDb = database.CreateDbContext("archive-export", caveA.AccountId);
        var services = IntegrationTestServices.For(
            exportDb,
            fileConnectionString: azurite.ConnectionString);
        var export = new ExportService(
            new AccountRepository(exportDb, exportDb.RequestUser),
            services.Files);

        var expectedLinePlotPayloads = await exportDb.CaveGeoJsons
            .IgnoreQueryFilters()
            .Where(row => row.Cave.AccountId == caveA.AccountId)
            .OrderBy(row => row.Id)
            .Select(row => row.GeoJson)
            .ToListAsync();

        var firstArchive = await GenerateAsync(export, caveA.AccountId, containerAName);
        var secondArchive = await GenerateAsync(export, caveA.AccountId, containerAName);

        var first = ReadArchive(firstArchive);
        var secondResult = ReadArchive(secondArchive);

        Assert.Equal(first.Keys.Order(), secondResult.Keys.Order());
        foreach (var path in first.Keys.Where(path => path != "missing-files.csv"))
            Assert.Equal(first[path], secondResult[path]);

        // Azure diagnostic text in the missing-file Reason can include a request ID,
        // so the stable contract is the missing object/path identity rather than byte-identical diagnostics.
        var firstMissingRows = ReadCsv(first["missing-files.csv"]);
        var secondMissingRows = ReadCsv(secondResult["missing-files.csv"]);
        Assert.Equal(
            firstMissingRows.Select(row => (row["CaveDisplayId"], row["CaveName"], row["EntryPath"], row["BlobKey"])),
            secondMissingRows.Select(row => (row["CaveDisplayId"], row["CaveName"], row["EntryPath"], row["BlobKey"])));

        Assert.Contains("caves.csv", first.Keys);
        Assert.Contains("entrances.csv", first.Keys);
        Assert.Contains("missing-files.csv", first.Keys);
        Assert.Equal(first.Count, first.Keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        Assert.All(first.Keys, AssertSafeArchivePath);

        var caveRows = ReadCsv(first["caves.csv"]);
        Assert.Equal(3, caveRows.Count);
        var caveOne = Assert.Single(caveRows.Where(r => r["CaveName"] == "Cave: One / Primary"));
        Assert.Equal("Alt One, Ált Two", caveOne["AlternateNames"]);
        Assert.Equal("Limestone", caveOne["Geology"]);
        Assert.Equal("Bats 🦇", caveOne["Biology"]);
        Assert.Equal("Complete", caveOne["MapStatuses"]);
        Assert.Equal("Special, Tag", caveOne["OtherTags"]);
        Assert.Equal("Current narrative, with commas and Unicode π.", caveOne["Narrative"]);
        Assert.DoesNotContain(caveRows, row => row["CaveName"] == caveB.CaveName);

        var entranceRows = ReadCsv(first["entrances.csv"]);
        Assert.Equal(2, entranceRows.Count);
        Assert.Contains(entranceRows, row =>
            row["EntranceName"] == "Main / Entrance" &&
            row["EntranceDescription"] == "Current entrance description" &&
            row["EntrancePitDepth"] == "42");
        Assert.Contains(entranceRows, row => row["EntranceName"] == "Éntrance #2");

        var payloadEntries = first
            .Where(pair => payloads.Values.Any(bytes => bytes.AsSpan().SequenceEqual(pair.Value)))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        Assert.Equal(payloads.Count, payloadEntries.Count);
        foreach (var expected in payloads.Values)
            Assert.Single(payloadEntries.Values.Where(actual => expected.AsSpan().SequenceEqual(actual)));

        var duplicateEntries = payloadEntries
            .Where(pair => pair.Key.EndsWith("/duplicate.txt", StringComparison.OrdinalIgnoreCase) ||
                           pair.Key.EndsWith("/duplicate (2).txt", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(2, duplicateEntries.Length);
        Assert.Contains(duplicateEntries, pair => pair.Value.SequenceEqual(payloads["objects/dup-a"]));
        Assert.Contains(duplicateEntries, pair => pair.Value.SequenceEqual(payloads["objects/dup-b"]));

        Assert.DoesNotContain(first.Keys, path => path.Contains("/../", StringComparison.Ordinal));
        Assert.Contains(payloadEntries.Keys, path => path.Contains("/Other/dot-folder.dat", StringComparison.Ordinal));
        Assert.DoesNotContain(first.Values, bytes => bytes.SequenceEqual(new byte[] { 7, 7, 7 }));
        Assert.DoesNotContain(first.Values, bytes => bytes.SequenceEqual(new byte[] { 6, 6, 6 }));
        Assert.DoesNotContain(first.Values, bytes => bytes.SequenceEqual(new byte[] { 5, 5, 5 }));
        Assert.DoesNotContain(first.Values, bytes => bytes.SequenceEqual(new byte[] { 99, 98, 97 }));
        Assert.DoesNotContain(first.Values, bytes => bytes.SequenceEqual(new byte[] { 42, 42 }));
        Assert.DoesNotContain(first.Values, bytes => bytes.SequenceEqual(new byte[] { 33, 33 }));
        Assert.DoesNotContain(first.Values, bytes => bytes.SequenceEqual(new byte[] { 88, 88 }));

        var linePlots = first
            .Where(pair => pair.Key.Contains("/Line Plots/", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, linePlots.Length);
        Assert.Equal(2, linePlots.Select(pair => pair.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(
            expectedLinePlotPayloads.Order(StringComparer.Ordinal),
            linePlots.Select(pair => Encoding.UTF8.GetString(pair.Value)).Order(StringComparer.Ordinal));

        var missing = ReadCsv(first["missing-files.csv"]);
        Assert.Equal(4, missing.Count);

        var missingPayload = Assert.Single(missing.Where(row => row["BlobKey"] == "objects/missing"));
        Assert.Contains("missing source.pdf", missingPayload["EntryPath"], StringComparison.Ordinal);
        Assert.False(first.ContainsKey(missingPayload["EntryPath"]));

        var missingKey = Assert.Single(missing.Where(row => row["BlobKey"] == string.Empty));
        Assert.Contains("missing blob key.txt", missingKey["EntryPath"], StringComparison.Ordinal);
        Assert.Equal("Blob key is missing.", missingKey["Reason"]);
        Assert.False(first.ContainsKey(missingKey["EntryPath"]));

        var foreignLocator = Assert.Single(missing.Where(row => row["BlobKey"] == "objects/mismatch"));
        Assert.Contains("foreign locator.bin", foreignLocator["EntryPath"], StringComparison.Ordinal);
        Assert.Equal("Blob container does not match the account container.", foreignLocator["Reason"]);
        Assert.False(first.ContainsKey(foreignLocator["EntryPath"]));

        var missingContainer = Assert.Single(missing.Where(row => row["BlobKey"] == "objects/containerless"));
        Assert.Contains("missing container.bin", missingContainer["EntryPath"], StringComparison.Ordinal);
        Assert.Equal("Blob container is missing.", missingContainer["Reason"]);
        Assert.False(first.ContainsKey(missingContainer["EntryPath"]));
    }

    [Fact]
    public async Task WriteArchiveHonorsCancellationBeforeAndDuringGeneration()
    {
        await using var database = await postgres.CreateDatabaseAsync(
            nameof(WriteArchiveHonorsCancellationBeforeAndDuringGeneration));
        var cave = await CaveTestDataFactory.CreatePublishedCaveAsync(database, 'a');
        var file = await FileTestDataFactory.AddFileAsync(
            database, cave, associateWithCave: true, fileId: "cancel0001",
            name: "cancel-me", extension: ".bin");
        var containerName = AccountContainerName(database, cave.AccountId);
        await using (var db = database.CreateDbContext("cancel-seed", cave.AccountId))
        {
            var row = await db.Files.IgnoreQueryFilters().SingleAsync(f => f.Id == file.FileId);
            row.BlobContainer = containerName;
            row.BlobKey = "objects/cancel";
            await db.SaveChangesAsync();
        }

        var container = new BlobServiceClient(azurite.ConnectionString).GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync();
        await container.GetBlobClient("objects/cancel")
            .UploadAsync(new BinaryData(new byte[2_000_000]), overwrite: true);

        await using var dbExport = database.CreateDbContext("cancel-export", cave.AccountId);
        var services = IntegrationTestServices.For(dbExport, fileConnectionString: azurite.ConnectionString);
        var export = new ExportService(new AccountRepository(dbExport, dbExport.RequestUser), services.Files);

        using (var alreadyCanceled = new CancellationTokenSource())
        {
            alreadyCanceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                export.WriteArchive(new MemoryStream(), cave.AccountId, containerName, null, null,
                    alreadyCanceled.Token));
        }

        using var duringCsv = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            export.WriteArchive(
                new MemoryStream(),
                cave.AccountId,
                containerName,
                null,
                message =>
                {
                    if (message == "Creating archive...")
                        duringCsv.Cancel();
                    return Task.CompletedTask;
                },
                duringCsv.Token));

        using var duringFiles = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            export.WriteArchive(
                new MemoryStream(),
                cave.AccountId,
                containerName,
                (processed, total) =>
                {
                    if (processed > 0 && processed < total + 1)
                        duringFiles.Cancel();
                    return Task.CompletedTask;
                },
                null,
                duringFiles.Token));
    }

    private static void AddFile(
        Planarian.Model.Database.PlanarianDbContext db,
        PublishedCaveTestData cave,
        string fileTypeTagId,
        string id,
        string name,
        string extension,
        string blobKey)
    {
        db.Files.Add(new File
        {
            Id = id,
            AccountId = cave.AccountId,
            CaveId = cave.CaveId,
            FileTypeTagId = fileTypeTagId,
            Name = name,
            Extension = extension,
            BlobContainer = db.RequestUser.AccountContainerName,
            BlobKey = blobKey
        });
    }

    private static string AccountContainerName(PostgresTestDatabase database, string accountId)
    {
        using var db = database.CreateDbContext("container-name", accountId);
        return db.RequestUser.AccountContainerName;
    }

    private static async Task<byte[]> GenerateAsync(
        ExportService export,
        string accountId,
        string containerName)
    {
        await using var stream = new MemoryStream();
        await export.WriteArchive(stream, accountId, containerName, null, null, CancellationToken.None);
        return stream.ToArray();
    }

    private static Dictionary<string, byte[]> ReadArchive(byte[] bytes)
    {
        using var source = new MemoryStream(bytes, writable: false);
        using var gzip = new GZipStream(source, CompressionMode.Decompress);
        using var reader = new TarReader(gzip);
        var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            Assert.False(entries.ContainsKey(entry.Name), $"Duplicate tar entry: {entry.Name}");
            using var copy = new MemoryStream();
            entry.DataStream?.CopyTo(copy);
            entries[entry.Name] = copy.ToArray();
        }

        return entries;
    }

    private static List<Dictionary<string, string>> ReadCsv(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord!;
        var rows = new List<Dictionary<string, string>>();
        while (csv.Read())
        {
            rows.Add(headers.ToDictionary(
                header => header,
                header => csv.GetField(header) ?? string.Empty,
                StringComparer.Ordinal));
        }

        return rows;
    }

    private static void AssertSafeArchivePath(string path)
    {
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.False(path.StartsWith('/'));
        Assert.DoesNotContain('\\', path);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain(".", segments);
        Assert.DoesNotContain("..", segments);
    }
}
