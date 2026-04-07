using System.Globalization;
using System.IO.Compression;
using System.Text;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CsvHelper;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Models;
using Planarian.Shared.Options;

namespace Planarian.Modules.Account.Services;

public class ExportService
{
    private readonly FileService _fileService;
    private readonly BackupOptions _backupOptions;

    public ExportService(FileService fileService, BackupOptions backupOptions)
    {
        _fileService = fileService;
        _backupOptions = backupOptions;
    }

    public async Task<Stream> ExportAccount(
        IReadOnlyList<AccountBackupCaveDto> caves,
        IReadOnlyList<AccountBackupEntranceByCaveDto> entrances,
        IReadOnlyList<AccountBackupFileByCaveDto> files,
        IReadOnlyList<AccountBackupGeoJsonByCaveDto> geoJsons,
        Func<int, int, Task>? reportProgress,
        Func<string, Task>? reportStatus,
        CancellationToken cancellationToken)
    {
        var totalCaves = caves.Count;
        var tempRootDirectory = string.IsNullOrWhiteSpace(_backupOptions.TempDirectory)
            ? Path.GetTempPath()
            : _backupOptions.TempDirectory;
        Directory.CreateDirectory(tempRootDirectory);
        var stagingDirectory = files.Count > 0
            ? Path.Combine(tempRootDirectory, $"planarian-backup-stage-{Guid.NewGuid():N}")
            : null;

        if (!string.IsNullOrWhiteSpace(stagingDirectory))
        {
            Directory.CreateDirectory(stagingDirectory);
        }

        var tempZipPath = Path.Combine(tempRootDirectory, $"planarian-backup-{Guid.NewGuid():N}.zip");
        var outputStream = new FileStream(
            tempZipPath,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.Read,
            65536,
            System.IO.FileOptions.Asynchronous | System.IO.FileOptions.DeleteOnClose | System.IO.FileOptions.SequentialScan);

        var stagingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var missingFiles = new List<MissingBackupFile>();

        try
        {
            using (var archive = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
            {
                var existingEntryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                await WriteCsvEntry(
                    archive,
                    "caves.csv",
                    BuildCaveCsvRows(caves),
                    new CaveCsvModelMap(),
                    cancellationToken);
                existingEntryPaths.Add("caves.csv");

                await WriteCsvEntry(
                    archive,
                    "entrances.csv",
                    BuildEntranceCsvRows(entrances),
                    new EntranceCsvModelMap(),
                    cancellationToken);
                existingEntryPaths.Add("entrances.csv");

                var geoJsonsByCaveId = geoJsons.ToLookup(geoJson => geoJson.CavePlanarianId);
                var fileWorkItemsByCaveId = BuildFileWorkItems(caves, files, existingEntryPaths);
                var accountBlobContainerClient = files.Count > 0
                    ? await _fileService.GetAccountBlobContainerClient()
                    : null;

                await WriteCaveEntries(
                    archive,
                    caves,
                    geoJsonsByCaveId,
                    fileWorkItemsByCaveId,
                    accountBlobContainerClient,
                    stagingDirectory,
                    existingEntryPaths,
                    missingFiles,
                    reportProgress,
                    stagingCancellationTokenSource,
                    cancellationToken);

                await ReportStatus(reportStatus, "Finalizing archive...");
                await WriteMissingFilesEntry(
                    archive,
                    missingFiles,
                    existingEntryPaths,
                    cancellationToken);
            }

            await ReportStatus(reportStatus, "Cleaning up temporary files...");
            if (!string.IsNullOrWhiteSpace(stagingDirectory))
            {
                TryDeleteDirectory(stagingDirectory);
            }

            outputStream.Position = 0;
            await ReportProgress(reportProgress, totalCaves, totalCaves);
            await ReportStatus(reportStatus, "Archive ready. Starting download...");
            return outputStream;
        }
        catch
        {
            stagingCancellationTokenSource.Cancel();
            await outputStream.DisposeAsync();
            throw;
        }
        finally
        {
            stagingCancellationTokenSource.Cancel();
            stagingCancellationTokenSource.Dispose();
        }
    }

    private static async Task ReportProgress(Func<int, int, Task>? reportProgress, int processed, int total, int? interval = null)
    {
        if (reportProgress == null)
        {
            return;
        }

        if (interval.HasValue && total > 0 && processed != total && processed % interval.Value != 0)
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

    private static int GetProgressInterval(int total)
    {
        return total <= 0 ? 1 : 15;
    }

    private async Task WriteCaveEntries(
        ZipArchive archive,
        IReadOnlyList<AccountBackupCaveDto> caves,
        ILookup<string, AccountBackupGeoJsonByCaveDto> geoJsonsByCaveId,
        IReadOnlyDictionary<string, List<BackupFileWorkItem>> fileWorkItemsByCaveId,
        BlobContainerClient? accountBlobContainerClient,
        string? stagingDirectory,
        ISet<string> existingEntryPaths,
        ICollection<MissingBackupFile> missingFiles,
        Func<int, int, Task>? reportProgress,
        CancellationTokenSource stagingCancellationTokenSource,
        CancellationToken cancellationToken)
    {
        await ReportProgress(reportProgress, 0, caves.Count);
        var caveProgressInterval = GetProgressInterval(caves.Count);

        for (var index = 0; index < caves.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cave = caves[index];
            var caveFolder = AccountBackupArchivePaths.BuildCaveFolder(cave);

            if (fileWorkItemsByCaveId.TryGetValue(cave.PlanarianId, out var caveFileWorkItems))
            {
                if (accountBlobContainerClient != null && !string.IsNullOrWhiteSpace(stagingDirectory))
                {
                    await WriteCaveFileEntries(
                        archive,
                        caveFileWorkItems,
                        accountBlobContainerClient,
                        stagingDirectory,
                        missingFiles,
                        stagingCancellationTokenSource,
                        cancellationToken);
                }
            }

            foreach (var geoJson in geoJsonsByCaveId[cave.PlanarianId])
            {
                var geoJsonName = AccountBackupArchivePaths.SanitizeFileName(
                    AccountBackupArchivePaths.EnsureGeoJsonFileName(geoJson.Name),
                    "Line Plot.geojson");
                var desiredPath = $"files/{caveFolder}/Line Plots/{geoJsonName}";
                var entryPath = AccountBackupArchivePaths.CreateUniqueEntryPath(desiredPath, existingEntryPaths);
                var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);

                await using var entryStream = entry.Open();
                await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
                await writer.WriteAsync(geoJson.GeoJson);
                await writer.FlushAsync(cancellationToken);
            }

            await ReportProgress(reportProgress, index + 1, caves.Count, caveProgressInterval);
        }
    }

    private async Task WriteCaveFileEntries(
        ZipArchive archive,
        IReadOnlyList<BackupFileWorkItem> caveFileWorkItems,
        BlobContainerClient accountBlobContainerClient,
        string stagingDirectory,
        ICollection<MissingBackupFile> missingFiles,
        CancellationTokenSource stagingCancellationTokenSource,
        CancellationToken cancellationToken)
    {
        Task<PrefetchedBackupFile>? nextPrefetchTask = null;

        try
        {
            for (var index = 0; index < caveFileWorkItems.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var currentWorkItem = caveFileWorkItems[index];
                var currentPrefetchTask = nextPrefetchTask ?? PrefetchBackupFile(
                    currentWorkItem,
                    accountBlobContainerClient,
                    stagingDirectory,
                    stagingCancellationTokenSource.Token);

                nextPrefetchTask = index + 1 < caveFileWorkItems.Count
                    ? PrefetchBackupFile(
                        caveFileWorkItems[index + 1],
                        accountBlobContainerClient,
                        stagingDirectory,
                        stagingCancellationTokenSource.Token)
                    : null;

                var prefetchedFile = await currentPrefetchTask;
                if (prefetchedFile.MissingFile != null)
                {
                    missingFiles.Add(prefetchedFile.MissingFile);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(prefetchedFile.StagingPath))
                {
                    continue;
                }

                try
                {
                    var entry = archive.CreateEntry(prefetchedFile.EntryPath, CompressionLevel.NoCompression);
                    await using var stagedFileStream = new FileStream(
                        prefetchedFile.StagingPath,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        65536,
                        System.IO.FileOptions.Asynchronous | System.IO.FileOptions.SequentialScan);
                    await using var entryStream = entry.Open();
                    await stagedFileStream.CopyToAsync(entryStream, cancellationToken);
                }
                finally
                {
                    TryDeleteFile(prefetchedFile.StagingPath);
                }
            }
        }
        catch
        {
            stagingCancellationTokenSource.Cancel();
            throw;
        }
        finally
        {
            await AwaitPendingPrefetch(nextPrefetchTask);
        }
    }

    private static Dictionary<string, List<BackupFileWorkItem>> BuildFileWorkItems(
        IReadOnlyList<AccountBackupCaveDto> caves,
        IReadOnlyList<AccountBackupFileByCaveDto> files,
        ISet<string> existingEntryPaths)
    {
        var filesByCaveId = files.ToLookup(file => file.CavePlanarianId);
        var sequenceIndex = 0;
        var result = new Dictionary<string, List<BackupFileWorkItem>>(StringComparer.OrdinalIgnoreCase);

        foreach (var cave in caves)
        {
            var caveFolder = AccountBackupArchivePaths.BuildCaveFolder(cave);
            var caveFileWorkItems = new List<BackupFileWorkItem>();

            foreach (var file in filesByCaveId[cave.PlanarianId])
            {
                if (string.IsNullOrWhiteSpace(file.BlobKey))
                {
                    continue;
                }

                var fileTagFolder = AccountBackupArchivePaths.SanitizePathSegment(file.FileTypeDisplayName, FileTypeTagName.Other);
                var fileNameInArchive = AccountBackupArchivePaths.SanitizeFileName(file.FileName, $"file-{file.Id}");
                var desiredPath = $"files/{caveFolder}/{fileTagFolder}/{fileNameInArchive}";
                var entryPath = AccountBackupArchivePaths.CreateUniqueEntryPath(desiredPath, existingEntryPaths);
                var currentSequenceIndex = sequenceIndex++;

                caveFileWorkItems.Add(new BackupFileWorkItem(
                    entryPath,
                    file.BlobKey,
                    $"{currentSequenceIndex:D8}{Path.GetExtension(file.FileName)}"));
            }

            result[cave.PlanarianId] = caveFileWorkItems;
        }

        return result;
    }

    private async Task<PrefetchedBackupFile> PrefetchBackupFile(
        BackupFileWorkItem workItem,
        BlobContainerClient accountBlobContainerClient,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        var stagingPath = Path.Combine(stagingDirectory, workItem.StagingFileName);

        try
        {
            await DownloadBlobToFile(
                accountBlobContainerClient,
                workItem.BlobKey,
                stagingPath,
                cancellationToken);

            return new PrefetchedBackupFile(
                workItem.EntryPath,
                stagingPath,
                null);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 || string.Equals(ex.ErrorCode, BlobErrorCode.BlobNotFound.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteFile(stagingPath);
            return new PrefetchedBackupFile(
                workItem.EntryPath,
                null,
                new MissingBackupFile(
                    workItem.EntryPath,
                    workItem.BlobKey,
                    ex.ErrorCode ?? BlobErrorCode.BlobNotFound.ToString()));
        }
        catch (OperationCanceledException)
        {
            TryDeleteFile(stagingPath);
            throw;
        }
        catch
        {
            TryDeleteFile(stagingPath);
            throw;
        }
    }

    private static async Task AwaitPendingPrefetch(Task<PrefetchedBackupFile>? prefetchTask)
    {
        if (prefetchTask == null)
        {
            return;
        }

        try
        {
            await prefetchTask;
        }
        catch
        {
        }
    }

    private async Task DownloadBlobToFile(
        BlobContainerClient containerClient,
        string blobKey,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var transferSizeBytes = Math.Max(1, _backupOptions.BlobDownloadTransferSizeMb) * 1024L * 1024L;
        var blobClient = containerClient.GetBlobClient(blobKey);
        var downloadOptions = new BlobDownloadToOptions
        {
            TransferOptions = new StorageTransferOptions
            {
                MaximumConcurrency = Math.Max(1, _backupOptions.BlobDownloadTransferConcurrency),
                InitialTransferSize = transferSizeBytes,
                MaximumTransferSize = transferSizeBytes
            }
        };

        await blobClient.DownloadToAsync(destinationPath, downloadOptions, cancellationToken);
    }

    private static async Task WriteMissingFilesEntry(
        ZipArchive archive,
        IEnumerable<MissingBackupFile> missingFiles,
        ISet<string> existingEntryPaths,
        CancellationToken cancellationToken)
    {
        var missingFileList = missingFiles
            .OrderBy(file => file.EntryPath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (missingFileList.Count == 0)
        {
            return;
        }

        var entryPath = AccountBackupArchivePaths.CreateUniqueEntryPath("missing-files.csv", existingEntryPaths);
        var entry = archive.CreateEntry(entryPath, CompressionLevel.Fastest);

        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        await writer.WriteLineAsync("EntryPath,BlobKey,ErrorCode");

        foreach (var missingFile in missingFileList)
        {
            await writer.WriteLineAsync(string.Join(",",
                EscapeCsv(missingFile.EntryPath),
                EscapeCsv(missingFile.BlobKey),
                EscapeCsv(missingFile.ErrorCode)));
        }

        await writer.FlushAsync(cancellationToken);
    }

    private static string EscapeCsv(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static void TryDeleteFile(string path)
    {
        if (!System.IO.File.Exists(path))
        {
            return;
        }

        try
        {
            System.IO.File.Delete(path);
        }
        catch
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
        }
    }

    private static List<CaveCsvModel> BuildCaveCsvRows(IEnumerable<AccountBackupCaveDto> caves)
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

    private static List<EntranceCsvModel> BuildEntranceCsvRows(IEnumerable<AccountBackupEntranceDto> entrances)
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
        TMap classMap,
        CancellationToken cancellationToken) where TMap : class
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false));
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap(classMap.GetType());
        csv.WriteRecords(records);
        await writer.FlushAsync(cancellationToken);
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record BackupFileWorkItem(
        string EntryPath,
        string BlobKey,
        string StagingFileName);

    private sealed record PrefetchedBackupFile(
        string EntryPath,
        string? StagingPath,
        MissingBackupFile? MissingFile);

    private sealed record MissingBackupFile(
        string EntryPath,
        string BlobKey,
        string ErrorCode);
}

internal static class AccountBackupArchivePaths
{
    public static string BuildCaveFolder(AccountBackupCaveDto cave)
    {
        var stateFolder = SanitizePathSegment(cave.State, "Unknown State");
        var countyFolder = SanitizePathSegment($"{cave.CountyCode} {cave.CountyName}", cave.CountyCode);
        var caveDisplayId = $"{cave.CountyCode}{cave.CountyIdDelimiter}{cave.CountyCaveNumber}";
        var caveFolder = SanitizePathSegment($"{caveDisplayId} {cave.CaveName}", caveDisplayId);

        return $"{stateFolder}/{countyFolder}/{caveFolder}";
    }

    public static string CreateUniqueEntryPath(string desiredPath, ISet<string> existingPaths)
    {
        if (existingPaths.Add(desiredPath))
        {
            return desiredPath;
        }

        var extension = Path.GetExtension(desiredPath);
        var directory = Path.GetDirectoryName(desiredPath)?.Replace('\\', '/');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(desiredPath);
        var suffix = 2;

        while (true)
        {
            var candidateFileName = $"{fileNameWithoutExtension} ({suffix}){extension}";
            var candidatePath = string.IsNullOrWhiteSpace(directory)
                ? candidateFileName
                : $"{directory}/{candidateFileName}";

            if (existingPaths.Add(candidatePath))
            {
                return candidatePath;
            }

            suffix++;
        }
    }

    public static string EnsureGeoJsonFileName(string? fileName)
    {
        var normalizedFileName = string.IsNullOrWhiteSpace(fileName) ? "Line Plot" : fileName.Trim();
        return Path.HasExtension(normalizedFileName)
            ? normalizedFileName
            : $"{normalizedFileName}.geojson";
    }

    public static string SanitizeFileName(string? value, string fallback)
    {
        return SanitizePathSegment(value, fallback);
    }

    public static string SanitizePathSegment(string? value, string fallback)
    {
        var workingValue = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        var invalidCharacters = Path.GetInvalidFileNameChars()
            .Concat(['/', '\\'])
            .Distinct()
            .ToHashSet();
        var sanitizedCharacters = workingValue
            .Select(character => invalidCharacters.Contains(character) ? ' ' : character)
            .ToArray();
        var sanitized = string.Join(
            " ",
            new string(sanitizedCharacters).Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}