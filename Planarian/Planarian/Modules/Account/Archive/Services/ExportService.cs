using System.Globalization;
using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Azure.Storage.Blobs;
using CsvHelper;
using CsvHelper.Configuration;
using Planarian.Library.Constants;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Account.Archive.Services;

public class ExportService
{
    private const int BlobPrefetchWindow = 8;

    private readonly AccountRepository _accountRepository;
    private readonly FileService _fileService;

    public ExportService(
        AccountRepository accountRepository,
        FileService fileService)
    {
        _accountRepository = accountRepository;
        _fileService = fileService;
    }

    public async Task WriteArchive(
        Stream outputStream,
        string accountId,
        string accountContainerName,
        Func<int, int, Task>? reportFileProgress,
        Func<string, Task>? reportStatus,
        CancellationToken cancellationToken)
    {
        await ReportStatus(reportStatus, "Gathering cave data...");
        var caves = await _accountRepository.GetArchiveCaves(accountId, cancellationToken);

        await ReportStatus(reportStatus, "Gathering entrance data...");
        var entrances = await _accountRepository.GetArchiveEntrances(accountId, cancellationToken);

        await ReportStatus(reportStatus, "Gathering file data...");
        var files = await _accountRepository.GetArchiveFiles(accountId, cancellationToken);
        var totalFiles = files.Count(file => !string.IsNullOrWhiteSpace(file.BlobKey));

        await ReportStatus(reportStatus, "Gathering map data...");
        var geoJsons = await _accountRepository.GetArchiveGeoJsons(accountId, cancellationToken);

        await ReportStatus(reportStatus, "Creating archive...");

        var missingFiles = new List<MissingArchiveFile>();
        var reservedEntryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var gzipStream = new GZipStream(outputStream, CompressionLevel.Fastest, leaveOpen: true);
        await using var archive = new TarWriter(gzipStream, TarEntryFormat.Pax, leaveOpen: true);

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

        if (totalFiles > 0)
        {
            try
            {
                var candidateContainerClient = await _fileService.GetBlobContainerClient(accountContainerName);
                var containerExists = await candidateContainerClient.ExistsAsync(cancellationToken);
                if (containerExists.Value)
                {
                    accountBlobContainerClient = candidateContainerClient;
                }
                else
                {
                    AddMissingFiles(
                        missingFiles,
                        caves,
                        files,
                        reservedEntryPaths,
                        "Container not found");
                    await ReportProgress(reportFileProgress, totalFiles, totalFiles);
                }
            }
            catch (Exception ex)
            {
                AddMissingFiles(
                    missingFiles,
                    caves,
                    files,
                    reservedEntryPaths,
                    ex.Message);
                await ReportProgress(reportFileProgress, totalFiles, totalFiles);
            }
        }
        else
        {
            await ReportProgress(reportFileProgress, 0, 0);
        }

        await WriteCaveEntries(
            archive,
            caves,
            geoJsonsByCaveId,
            filesByCaveId,
            accountBlobContainerClient,
            missingFiles,
            reservedEntryPaths,
            reportFileProgress,
            totalFiles,
            cancellationToken);

        await ReportStatus(reportStatus, "Finalizing archive...");
        await WriteMissingFilesEntry(archive, missingFiles, cancellationToken);

        await outputStream.FlushAsync(cancellationToken);
    }

    private async Task WriteCaveEntries(
        TarWriter archive,
        IReadOnlyList<ArchiveCaveCsvModel> caves,
        ILookup<string, ArchiveGeoJsonByCaveModel> geoJsonsByCaveId,
        ILookup<string, ArchiveFileByCaveModel> filesByCaveId,
        BlobContainerClient? accountBlobContainerClient,
        ICollection<MissingArchiveFile> missingFiles,
        ISet<string> reservedEntryPaths,
        Func<int, int, Task>? reportFileProgress,
        int totalFiles,
        CancellationToken cancellationToken)
    {
        var processedFiles = accountBlobContainerClient == null ? totalFiles : 0;
        await ReportProgress(reportFileProgress, processedFiles, totalFiles);

        for (var index = 0; index < caves.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var cave = caves[index];
            var caveFolder = BuildCaveFolder(cave);

            if (accountBlobContainerClient != null &&
                filesByCaveId.Contains(cave.PlanarianId))
            {
                processedFiles = await WriteCaveFileEntries(
                    archive,
                    cave,
                    caveFolder,
                    filesByCaveId[cave.PlanarianId],
                    accountBlobContainerClient,
                    missingFiles,
                    reservedEntryPaths,
                    processedFiles,
                    totalFiles,
                    reportFileProgress,
                    cancellationToken);
            }

            foreach (var geoJson in geoJsonsByCaveId[cave.PlanarianId])
            {
                cancellationToken.ThrowIfCancellationRequested();

                var geoJsonName = EnsureGeoJsonFileName(geoJson.Name);
                var entryPath = ReserveArchiveEntryPath(
                    reservedEntryPaths,
                    missingFiles,
                    $"files/{caveFolder}/Line Plots/{geoJsonName}");
                await WriteTextEntry(
                    archive,
                    entryPath,
                    geoJson.GeoJson,
                    cancellationToken);
            }

        }
    }

    private async Task<int> WriteCaveFileEntries(
        TarWriter archive,
        ArchiveCaveCsvModel cave,
        string caveFolder,
        IEnumerable<ArchiveFileByCaveModel> files,
        BlobContainerClient accountBlobContainerClient,
        ICollection<MissingArchiveFile> missingFiles,
        ISet<string> reservedEntryPaths,
        int processedFiles,
        int totalFiles,
        Func<int, int, Task>? reportFileProgress,
        CancellationToken cancellationToken)
    {
        var pendingFiles = new Queue<Task<PrefetchedArchiveFile>>();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(file.BlobKey))
            {
                continue;
            }

            var fileTagFolder = NormalizePathSegment(file.FileTypeDisplayName, FileTypeTagName.Other);
            var fileNameInArchive = NormalizePathSegment(file.FileName, $"file-{file.Id}");
            var entryPath = ReserveArchiveEntryPath(
                reservedEntryPaths,
                missingFiles,
                $"files/{caveFolder}/{fileTagFolder}/{fileNameInArchive}");

            pendingFiles.Enqueue(PrefetchArchiveFile(
                cave,
                file.BlobKey,
                entryPath,
                accountBlobContainerClient,
                cancellationToken));

            if (pendingFiles.Count >= BlobPrefetchWindow)
            {
                processedFiles = await WritePrefetchedArchiveFile(
                    archive,
                    pendingFiles.Dequeue(),
                    missingFiles,
                    processedFiles,
                    totalFiles,
                    reportFileProgress,
                    cancellationToken);
            }
        }

        while (pendingFiles.Count > 0)
        {
            processedFiles = await WritePrefetchedArchiveFile(
                archive,
                pendingFiles.Dequeue(),
                missingFiles,
                processedFiles,
                totalFiles,
                reportFileProgress,
                cancellationToken);
        }

        return processedFiles;
    }

    private async Task<int> WritePrefetchedArchiveFile(
        TarWriter archive,
        Task<PrefetchedArchiveFile> prefetchedFileTask,
        ICollection<MissingArchiveFile> missingFiles,
        int processedFiles,
        int totalFiles,
        Func<int, int, Task>? reportFileProgress,
        CancellationToken cancellationToken)
    {
        var prefetchedFile = await prefetchedFileTask;

        if (prefetchedFile.DataStream != null)
        {
            await using (prefetchedFile.DataStream)
            {
                prefetchedFile.DataStream.Position = 0;
                await WriteStreamEntry(
                    archive,
                    prefetchedFile.EntryPath,
                    prefetchedFile.DataStream,
                    DateTimeOffset.UtcNow,
                    cancellationToken);
            }
        }
        else if (prefetchedFile.MissingFile != null)
        {
            missingFiles.Add(prefetchedFile.MissingFile);
        }

        processedFiles++;
        await ReportProgress(reportFileProgress, processedFiles, totalFiles);
        return processedFiles;
    }

    private async Task<PrefetchedArchiveFile> PrefetchArchiveFile(
        ArchiveCaveCsvModel cave,
        string blobKey,
        string entryPath,
        BlobContainerClient accountBlobContainerClient,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var blobStream = await _fileService.OpenBlobReadStream(
                accountBlobContainerClient,
                blobKey,
                cancellationToken);
            var bufferedStream = new MemoryStream();
            await blobStream.CopyToAsync(bufferedStream, cancellationToken);
            return new PrefetchedArchiveFile(entryPath, bufferedStream, null);
        }
        catch (Exception ex)
        {
            return new PrefetchedArchiveFile(
                entryPath,
                null,
                BuildMissingArchiveFile(cave, entryPath, blobKey, ex.Message));
        }
    }

    private static async Task WriteMissingFilesEntry(
        TarWriter archive,
        IReadOnlyCollection<MissingArchiveFile> missingFiles,
        CancellationToken cancellationToken)
    {
        if (missingFiles.Count == 0)
        {
            return;
        }

        var contentBuilder = new StringBuilder();
        contentBuilder.AppendLine("CaveDisplayId,CaveName,EntryPath,BlobKey,Reason");

        foreach (var missingFile in missingFiles.OrderBy(file => file.EntryPath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            contentBuilder.AppendLine(string.Join(",",
                EscapeCsv(missingFile.CaveDisplayId),
                EscapeCsv(missingFile.CaveName),
                EscapeCsv(missingFile.EntryPath),
                EscapeCsv(missingFile.BlobKey),
                EscapeCsv(missingFile.Reason)));
        }

        await WriteTextEntry(archive, "missing-files.csv", contentBuilder.ToString(), cancellationToken);
    }

    private static async Task WriteTextEntry(
        TarWriter archive,
        string entryPath,
        string content,
        CancellationToken cancellationToken)
    {
        await using var entryStream = new MemoryStream();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false), 1024, true);
        await writer.WriteAsync(content);
        await writer.FlushAsync(cancellationToken);
        entryStream.Position = 0;
        await WriteStreamEntry(archive, entryPath, entryStream, DateTimeOffset.UtcNow, cancellationToken);
    }

    private static string BuildCaveFolder(ArchiveCaveCsvModel cave)
    {
        var stateFolder = NormalizePathSegment(cave.State, "Unknown State");
        var countyFolder = NormalizePathSegment($"{cave.CountyCode} {cave.CountyName}", cave.CountyCode);
        var caveDisplayId = $"{cave.CountyCode}{cave.CountyIdDelimiter}{cave.CountyCaveNumber}";
        var caveFolder = NormalizePathSegment($"{caveDisplayId} {cave.CaveName}", caveDisplayId);
        return $"{stateFolder}/{countyFolder}/{caveFolder}";
    }

    private static MissingArchiveFile BuildMissingArchiveFile(
        ArchiveCaveCsvModel cave,
        string entryPath,
        string blobKey,
        string reason)
    {
        return new MissingArchiveFile(
            $"{cave.CountyCode}{cave.CountyIdDelimiter}{cave.CountyCaveNumber}",
            cave.CaveName,
            entryPath,
            blobKey,
            reason);
    }

    private static void AddMissingFiles(
        ICollection<MissingArchiveFile> missingFiles,
        IEnumerable<ArchiveCaveCsvModel> caves,
        IEnumerable<ArchiveFileByCaveModel> files,
        ISet<string> reservedEntryPaths,
        string reason)
    {
        var cavesById = caves.ToDictionary(cave => cave.PlanarianId);

        foreach (var file in files)
        {
            if (string.IsNullOrWhiteSpace(file.BlobKey) ||
                !cavesById.TryGetValue(file.CavePlanarianId, out var cave))
            {
                continue;
            }

            var caveFolder = BuildCaveFolder(cave);
            var fileTagFolder = NormalizePathSegment(file.FileTypeDisplayName, FileTypeTagName.Other);
            var fileNameInArchive = NormalizePathSegment(file.FileName, $"file-{file.Id}");
            var entryPath = ReserveArchiveEntryPath(
                reservedEntryPaths,
                missingFiles,
                $"files/{caveFolder}/{fileTagFolder}/{fileNameInArchive}");
            missingFiles.Add(BuildMissingArchiveFile(cave, entryPath, file.BlobKey, reason));
        }
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

    private static string ReserveArchiveEntryPath(
        ISet<string> reservedEntryPaths,
        IEnumerable<MissingArchiveFile> missingFiles,
        string entryPath)
    {
        if (IsArchiveEntryPathAvailable(reservedEntryPaths, missingFiles, entryPath))
        {
            reservedEntryPaths.Add(entryPath);
            return entryPath;
        }

        var directory = Path.GetDirectoryName(entryPath)?.Replace('\\', '/');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entryPath);
        var extension = Path.GetExtension(entryPath);

        for (var duplicateNumber = 2; ; duplicateNumber++)
        {
            var candidateFileName = $"{fileNameWithoutExtension} ({duplicateNumber}){extension}";
            var candidateEntryPath = string.IsNullOrWhiteSpace(directory)
                ? candidateFileName
                : $"{directory}/{candidateFileName}";

            if (IsArchiveEntryPathAvailable(reservedEntryPaths, missingFiles, candidateEntryPath))
            {
                reservedEntryPaths.Add(candidateEntryPath);
                return candidateEntryPath;
            }
        }
    }

    private static bool IsArchiveEntryPathAvailable(
        ISet<string> reservedEntryPaths,
        IEnumerable<MissingArchiveFile> missingFiles,
        string entryPath)
    {
        return !reservedEntryPaths.Contains(entryPath) &&
               !missingFiles.Any(file => string.Equals(file.EntryPath, entryPath, StringComparison.OrdinalIgnoreCase));
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
        TarWriter archive,
        string entryName,
        IEnumerable<TRecord> records,
        CancellationToken cancellationToken) where TMap : ClassMap
    {
        await using var entryStream = new MemoryStream();
        await using var writer = new StreamWriter(entryStream, new UTF8Encoding(false), 1024, true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<TMap>();
        csv.WriteRecords(records);
        await writer.FlushAsync(cancellationToken);
        entryStream.Position = 0;
        await WriteStreamEntry(archive, entryName, entryStream, DateTimeOffset.UtcNow, cancellationToken);
    }

    private static Task WriteStreamEntry(
        TarWriter archive,
        string entryPath,
        Stream dataStream,
        DateTimeOffset modificationTime,
        CancellationToken cancellationToken)
    {
        var entry = CreateFileEntry(entryPath, dataStream, modificationTime);
        return archive.WriteEntryAsync(entry, cancellationToken);
    }

    private static PaxTarEntry CreateFileEntry(
        string entryPath,
        Stream dataStream,
        DateTimeOffset modificationTime)
    {
        return new PaxTarEntry(TarEntryType.RegularFile, entryPath)
        {
            DataStream = dataStream,
            ModificationTime = modificationTime
        };
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private sealed record PrefetchedArchiveFile(
        string EntryPath,
        MemoryStream? DataStream,
        MissingArchiveFile? MissingFile);
}
