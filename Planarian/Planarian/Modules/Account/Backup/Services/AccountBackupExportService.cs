using System.IO.Compression;
using System.Text;
using Azure.Storage.Blobs;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Backup.Models;
using FileOptions = Planarian.Shared.Options.FileOptions;
using BackupOptions = Planarian.Shared.Options.BackupOptions;

namespace Planarian.Modules.Account.Backup.Services;

public class AccountBackupExportService
{
    private readonly FileOptions _fileOptions;
    private readonly RequestUser _requestUser;
    private readonly BackupOptions _backupOptions;

    public AccountBackupExportService(FileOptions fileOptions, RequestUser requestUser, BackupOptions backupOptions)
    {
        _fileOptions = fileOptions;
        _requestUser = requestUser;
        _backupOptions = backupOptions;
    }

    public async Task<Stream> ExportAccount(
        List<AccountBackupCaveDto> caves,
        List<AccountBackupEntranceByCaveDto> entrances,
        List<AccountBackupFileByCaveDto> files,
        List<AccountBackupGeoJsonByCaveDto> geoJsons,
        Func<int, int, Task> onCaveProgress,
        Func<string, Task> onMessage,
        CancellationToken cancellationToken)
    {
        var memoryStream = new MemoryStream();

        using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            await onMessage("Writing cave data...");
            await WriteCavesAsync(archive, caves, cancellationToken);

            await onMessage("Writing entrance data...");
            await WriteEntrancesAsync(archive, entrances, cancellationToken);

            await onMessage("Writing map data...");
            await WriteGeoJsonsAsync(archive, geoJsons, cancellationToken);

            await onMessage("Writing file attachments...");
            await WriteFilesAsync(archive, files, onCaveProgress, cancellationToken);
        }

        memoryStream.Position = 0;
        return memoryStream;
    }

    private static async Task WriteCavesAsync(ZipArchive archive, List<AccountBackupCaveDto> caves, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("caves.csv");
        await using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);

        await writer.WriteLineAsync(
            "Planarian ID,Cave Name,Alternate Names,State,County Name,County Code,County ID Delimiter," +
            "County Cave Number,Cave Length (ft),Cave Depth (ft),Max Pit Depth (ft),Number of Pits," +
            "Narrative,Geology,Geologic Ages,Physiographic Provinces,Archeology,Biology," +
            "Reported On Date,Reported By Names,Is Archived,Other Tags,Map Statuses,Cartographer Names");

        foreach (var cave in caves)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(
                $"{CsvEscape(cave.PlanarianId)}," +
                $"{CsvEscape(cave.CaveName)}," +
                $"{CsvEscape(cave.AlternateNames)}," +
                $"{CsvEscape(cave.State)}," +
                $"{CsvEscape(cave.CountyName)}," +
                $"{CsvEscape(cave.CountyCode)}," +
                $"{CsvEscape(cave.CountyIdDelimiter)}," +
                $"{cave.CountyCaveNumber}," +
                $"{cave.CaveLengthFt}," +
                $"{cave.CaveDepthFt}," +
                $"{cave.MaxPitDepthFt}," +
                $"{cave.NumberOfPits}," +
                $"{CsvEscape(cave.Narrative)}," +
                $"{CsvEscape(cave.Geology)}," +
                $"{CsvEscape(cave.GeologicAges)}," +
                $"{CsvEscape(cave.PhysiographicProvinces)}," +
                $"{CsvEscape(cave.Archeology)}," +
                $"{CsvEscape(cave.Biology)}," +
                $"{CsvEscape(cave.ReportedOnDate)}," +
                $"{CsvEscape(cave.ReportedByNames)}," +
                $"{cave.IsArchived}," +
                $"{CsvEscape(cave.OtherTags)}," +
                $"{CsvEscape(cave.MapStatuses)}," +
                $"{CsvEscape(cave.CartographerNames)}");
        }
    }

    private static async Task WriteEntrancesAsync(ZipArchive archive, List<AccountBackupEntranceByCaveDto> entrances, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry("entrances.csv");
        await using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);

        await writer.WriteLineAsync(
            "Cave Planarian ID,County Code,County Cave Number,Entrance Name," +
            "Decimal Latitude,Decimal Longitude,Entrance Elevation (ft),Location Quality," +
            "Entrance Description,Entrance Pit Depth,Entrance Statuses,Entrance Hydrology," +
            "Field Indication,Reported On Date,Reported By Names,Is Primary Entrance");

        foreach (var entrance in entrances)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(
                $"{CsvEscape(entrance.CavePlanarianId)}," +
                $"{CsvEscape(entrance.CountyCode)}," +
                $"{CsvEscape(entrance.CountyCaveNumber)}," +
                $"{CsvEscape(entrance.EntranceName)}," +
                $"{entrance.DecimalLatitude}," +
                $"{entrance.DecimalLongitude}," +
                $"{entrance.EntranceElevationFt}," +
                $"{CsvEscape(entrance.LocationQuality)}," +
                $"{CsvEscape(entrance.EntranceDescription)}," +
                $"{entrance.EntrancePitDepth}," +
                $"{CsvEscape(entrance.EntranceStatuses)}," +
                $"{CsvEscape(entrance.EntranceHydrology)}," +
                $"{CsvEscape(entrance.FieldIndication)}," +
                $"{CsvEscape(entrance.ReportedOnDate)}," +
                $"{CsvEscape(entrance.ReportedByNames)}," +
                $"{entrance.IsPrimaryEntrance}");
        }
    }

    private static async Task WriteGeoJsonsAsync(ZipArchive archive, List<AccountBackupGeoJsonByCaveDto> geoJsons, CancellationToken cancellationToken)
    {
        foreach (var geoJson in geoJsons)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var caveDirName = AccountBackupArchivePaths.SanitizePathSegment(geoJson.CavePlanarianId, "unknown");
            var fileName = AccountBackupArchivePaths.SanitizePathSegment(geoJson.Name, "map") + ".geojson";
            var entryPath = $"maps/{caveDirName}/{fileName}";

            var entry = archive.CreateEntry(entryPath);
            await using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            await writer.WriteAsync(geoJson.GeoJson);
        }
    }

    private async Task WriteFilesAsync(
        ZipArchive archive,
        List<AccountBackupFileByCaveDto> files,
        Func<int, int, Task> onCaveProgress,
        CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return;
        }

        var containerClient = new BlobContainerClient(_fileOptions.ConnectionString, _requestUser.AccountContainerName);

        var total = files.Count;
        var processed = 0;

        // ZipArchive is not thread-safe for concurrent writes; download blobs in parallel
        // into memory buffers first, then write to the archive sequentially.
        var concurrency = Math.Max(1, _backupOptions.BlobDownloadTransferConcurrency);
        var semaphore = new SemaphoreSlim(concurrency);
        var throttledTasks = files
            .Where(file => !string.IsNullOrWhiteSpace(file.BlobKey))
            .Select(async file =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var blobClient = containerClient.GetBlobClient(file.BlobKey!);
                    var buffer = new MemoryStream();
                    await blobClient.DownloadToAsync(buffer, cancellationToken);
                    buffer.Position = 0;
                    return (file, buffer);
                }
                finally
                {
                    semaphore.Release();
                }
            });

        var downloadResults = await Task.WhenAll(throttledTasks);

        foreach (var (file, buffer) in downloadResults)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var caveDirName = AccountBackupArchivePaths.SanitizePathSegment(file.CavePlanarianId, "unknown");
            var safeFileName = AccountBackupArchivePaths.SanitizePathSegment(file.FileName, file.Id);
            var entryPath = $"files/{caveDirName}/{safeFileName}";

            var entry = archive.CreateEntry(entryPath);
            await using var entryStream = entry.Open();
            await buffer.CopyToAsync(entryStream, cancellationToken);
            await buffer.DisposeAsync();

            processed++;
            await onCaveProgress(processed, total);
        }
    }

    private static string CsvEscape(string? value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
