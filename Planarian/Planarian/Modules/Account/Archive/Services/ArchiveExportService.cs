using System.Globalization;
using System.IO.Compression;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CsvHelper;
using CsvHelper.Configuration;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Account.Archive.Services;

public class ArchiveExportService
{
    private readonly AccountRepository _accountRepository;
    private readonly FileService _fileService;

    public ArchiveExportService(
        AccountRepository accountRepository,
        FileService fileService)
    {
        _accountRepository = accountRepository;
        _fileService = fileService;
    }

    public async Task<Stream> ExportArchive(
        Func<int, int, Task>? reportProgress,
        Func<string, Task>? reportStatus,
        CancellationToken cancellationToken)
    {
        await ReportStatus(reportStatus, "Gathering cave data...");
        var caves = await _accountRepository.GetArchiveCaves(cancellationToken);

        await ReportStatus(reportStatus, "Gathering entrance data...");
        var entrances = await _accountRepository.GetArchiveEntrances(cancellationToken);

        await ReportStatus(reportStatus, "Gathering file data...");
        var files = await _accountRepository.GetArchiveFiles(cancellationToken);

        await ReportStatus(reportStatus, "Gathering map data...");
        var geoJsons = await _accountRepository.GetArchiveGeoJsons(cancellationToken);

        await ReportStatus(reportStatus, "Creating archive...");

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"planarian-archive-{Guid.NewGuid():N}.zip");
        var outputStream = new FileStream(
            tempZipPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            65536,
            System.IO.FileOptions.Asynchronous | System.IO.FileOptions.DeleteOnClose | System.IO.FileOptions.SequentialScan);

        try
        {
            var missingFiles = new List<MissingArchiveFile>();

            using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
            {
                await WriteCsvEntry<CaveCsvModel, CaveCsvModelMap>(
                    archive,
                    "caves.csv",
                    BuildCaveCsvRows(caves),
                    cancellationToken);

                await WriteCsvEntry<EntranceCsvModel, EntranceCsvModelMap>(
                    archive,
                    "entrances.csv",
                    BuildEntranceCsvRows(entrances),
                    cancellationToken);

                var geoJsonsByCaveId = geoJsons.ToLookup(geoJson => geoJson.CavePlanarianId);
                var filesByCaveId = files.ToLookup(file => file.CavePlanarianId);
                BlobContainerClient? accountBlobContainerClient = null;

                if (files.Count > 0)
                {
                    var candidateContainerClient = await _fileService.GetAccountBlobContainerClient();

                    try
                    {
                        var containerExists = await candidateContainerClient.ExistsAsync(cancellationToken);
                        if (containerExists.Value)
                        {
                            accountBlobContainerClient = candidateContainerClient;
                        }
                    }
                    catch (RequestFailedException ex) when (ex.Status == 404 || string.Equals(ex.ErrorCode, BlobErrorCode.ContainerNotFound.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                    }
                }

                await WriteCaveEntries(
                    archive,
                    caves,
                    geoJsonsByCaveId,
                    filesByCaveId,
                    accountBlobContainerClient,
                    missingFiles,
                    reportProgress,
                    cancellationToken);

                await ReportStatus(reportStatus, "Finalizing archive...");
                await WriteMissingFilesEntry(archive, missingFiles, cancellationToken);
            }

            outputStream.Position = 0;
            return outputStream;
        }
        catch
        {
            await outputStream.DisposeAsync();
            throw;
        }
    }

    private async Task WriteCaveEntries(
        ZipArchive archive,
        IReadOnlyList<ArchiveCaveCsvModel> caves,
        ILookup<string, ArchiveGeoJsonByCaveModel> geoJsonsByCaveId,
        ILookup<string, ArchiveFileByCaveModel> filesByCaveId,
        BlobContainerClient? accountBlobContainerClient,
        ICollection<MissingArchiveFile> missingFiles,
        Func<int, int, Task>? reportProgress,
        CancellationToken cancellationToken)
    {
        await ReportProgress(reportProgress, 0, caves.Count);

        for (var index = 0; index < caves.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cave = caves[index];
            var caveFolder = BuildCaveFolder(cave);

            if (accountBlobContainerClient != null &&
                filesByCaveId.Contains(cave.PlanarianId))
            {
                await WriteCaveFileEntries(
                    archive,
                    caveFolder,
                    filesByCaveId[cave.PlanarianId],
                    accountBlobContainerClient,
                    missingFiles,
                    cancellationToken);
            }

            foreach (var geoJson in geoJsonsByCaveId[cave.PlanarianId])
            {
                cancellationToken.ThrowIfCancellationRequested();

                var geoJsonName = EnsureGeoJsonFileName(geoJson.Name);
                await WriteTextEntry(
                    archive,
                    $"files/{caveFolder}/Line Plots/{geoJsonName}",
                    geoJson.GeoJson,
                    CompressionLevel.Fastest,
                    cancellationToken);
            }

            await ReportProgress(reportProgress, index + 1, caves.Count);
        }
    }

    private async Task WriteCaveFileEntries(
        ZipArchive archive,
        string caveFolder,
        IEnumerable<ArchiveFileByCaveModel> files,
        BlobContainerClient accountBlobContainerClient,
        ICollection<MissingArchiveFile> missingFiles,
        CancellationToken cancellationToken)
    {
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(file.BlobKey))
            {
                continue;
            }

            var fileTagFolder = NormalizePathSegment(file.FileTypeDisplayName, FileTypeTagName.Other);
            var fileNameInArchive = NormalizePathSegment(file.FileName, $"file-{file.Id}");
            var entryPath = $"files/{caveFolder}/{fileTagFolder}/{fileNameInArchive}";

            try
            {
                await using var blobStream = await _fileService.OpenBlobReadStream(
                    accountBlobContainerClient,
                    file.BlobKey,
                    cancellationToken);
                var entry = archive.CreateEntry(entryPath, CompressionLevel.NoCompression);
                await using var entryStream = entry.Open();
                await blobStream.CopyToAsync(entryStream, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 404 || string.Equals(ex.ErrorCode, BlobErrorCode.BlobNotFound.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                missingFiles.Add(new MissingArchiveFile(
                    entryPath,
                    file.BlobKey,
                    ex.ErrorCode ?? BlobErrorCode.BlobNotFound.ToString()));
            }
        }
    }

    private static async Task WriteMissingFilesEntry(
        ZipArchive archive,
        IReadOnlyCollection<MissingArchiveFile> missingFiles,
        CancellationToken cancellationToken)
    {
        if (missingFiles.Count == 0)
        {
            return;
        }

        var entry = archive.CreateEntry("missing-files.csv", CompressionLevel.Fastest);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        await writer.WriteLineAsync("EntryPath,BlobKey,ErrorCode");

        foreach (var missingFile in missingFiles.OrderBy(file => file.EntryPath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            await writer.WriteLineAsync(string.Join(",",
                EscapeCsv(missingFile.EntryPath),
                EscapeCsv(missingFile.BlobKey),
                EscapeCsv(missingFile.ErrorCode)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static async Task WriteTextEntry(
        ZipArchive archive,
        string entryPath,
        string content,
        CompressionLevel compressionLevel,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryPath, compressionLevel);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        await writer.WriteAsync(content);
        await writer.FlushAsync(cancellationToken);
    }

    private static string BuildCaveFolder(ArchiveCaveCsvModel cave)
    {
        var stateFolder = NormalizePathSegment(cave.State, "Unknown State");
        var countyFolder = NormalizePathSegment($"{cave.CountyCode} {cave.CountyName}", cave.CountyCode);
        var caveDisplayId = $"{cave.CountyCode}{cave.CountyIdDelimiter}{cave.CountyCaveNumber}";
        var caveFolder = NormalizePathSegment($"{caveDisplayId} {cave.CaveName}", caveDisplayId);
        return $"{stateFolder}/{countyFolder}/{caveFolder}";
    }

    private static string EnsureGeoJsonFileName(string? fileName)
    {
        var normalizedFileName = NormalizePathSegment(fileName, "Line Plot");
        return Path.HasExtension(normalizedFileName)
            ? normalizedFileName
            : $"{normalizedFileName}.geojson";
    }

    private static string NormalizePathSegment(string? value, string fallback)
    {
        var normalizedValue = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        normalizedValue = normalizedValue.Replace('/', '-').Replace('\\', '-');
        return string.IsNullOrWhiteSpace(normalizedValue) ? fallback : normalizedValue;
    }

    private static async Task ReportProgress(Func<int, int, Task>? reportProgress, int processed, int total)
    {
        if (reportProgress == null)
        {
            return;
        }

        await reportProgress(processed, total);
    }

    private static async Task ReportStatus(Func<string, Task>? reportStatus, string message)
    {
        if (reportStatus == null)
        {
            return;
        }

        await reportStatus(message);
    }

    private static string EscapeCsv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static List<CaveCsvModel> BuildCaveCsvRows(IEnumerable<ArchiveCaveCsvModel> caves)
    {
        return caves.Select(cave => new CaveCsvModel
        {
            CaveName = cave.CaveName,
            AlternateNames = NullIfWhiteSpace(cave.AlternateNames),
            State = cave.State,
            CountyCode = cave.CountyCode,
            CountyName = cave.CountyName,
            CountyCaveNumber = cave.CountyCaveNumber,
            MapStatuses = NullIfWhiteSpace(cave.MapStatuses),
            CartographerNames = NullIfWhiteSpace(cave.CartographerNames),
            CaveLengthFt = cave.CaveLengthFt,
            CaveDepthFt = cave.CaveDepthFt,
            MaxPitDepthFt = cave.MaxPitDepthFt,
            NumberOfPits = cave.NumberOfPits,
            Narrative = cave.Narrative,
            Geology = NullIfWhiteSpace(cave.Geology),
            GeologicAges = NullIfWhiteSpace(cave.GeologicAges),
            PhysiographicProvinces = NullIfWhiteSpace(cave.PhysiographicProvinces),
            Archeology = NullIfWhiteSpace(cave.Archeology),
            Biology = NullIfWhiteSpace(cave.Biology),
            ReportedOnDate = cave.ReportedOnDate,
            ReportedByNames = NullIfWhiteSpace(cave.ReportedByNames),
            IsArchived = cave.IsArchived,
            OtherTags = NullIfWhiteSpace(cave.OtherTags)
        }).ToList();
    }

    private static List<EntranceCsvModel> BuildEntranceCsvRows(IEnumerable<ArchiveEntranceCsvModel> entrances)
    {
        return entrances
            .Select(entrance => new EntranceCsvModel
            {
                CountyCode = entrance.CountyCode,
                CountyCaveNumber = entrance.CountyCaveNumber,
                EntranceName = entrance.EntranceName,
                DecimalLatitude = entrance.DecimalLatitude,
                DecimalLongitude = entrance.DecimalLongitude,
                EntranceElevationFt = entrance.EntranceElevationFt,
                LocationQuality = entrance.LocationQuality,
                EntranceDescription = entrance.EntranceDescription,
                EntrancePitDepth = entrance.EntrancePitDepth,
                EntranceStatuses = NullIfWhiteSpace(entrance.EntranceStatuses),
                EntranceHydrology = NullIfWhiteSpace(entrance.EntranceHydrology),
                FieldIndication = NullIfWhiteSpace(entrance.FieldIndication),
                ReportedOnDate = entrance.ReportedOnDate,
                ReportedByNames = NullIfWhiteSpace(entrance.ReportedByNames),
                IsPrimaryEntrance = entrance.IsPrimaryEntrance
            })
            .ToList();
    }

    private static async Task WriteCsvEntry<TRecord, TMap>(
        ZipArchive archive,
        string entryName,
        IEnumerable<TRecord> records,
        CancellationToken cancellationToken) where TMap : ClassMap
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<TMap>();
        csv.WriteRecords(records);
        await writer.FlushAsync(cancellationToken);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record MissingArchiveFile(
        string EntryPath,
        string BlobKey,
        string ErrorCode);
}
