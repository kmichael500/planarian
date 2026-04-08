using Planarian.Shared.Options;

namespace Planarian.Modules.Account.Backup.Services;

public sealed class AccountBackupTempStorageService
{
    private const string ArchiveFilePrefix = "planarian-backup-";
    private const string ArchiveFileExtension = ".zip";
    private const string StageDirectoryPrefix = "planarian-backup-stage-";

    private readonly BackupOptions _backupOptions;

    public AccountBackupTempStorageService(BackupOptions backupOptions)
    {
        _backupOptions = backupOptions;
    }

    public string EnsureTempRootDirectory()
    {
        var tempRootDirectory = GetResolvedTempRootDirectory();
        Directory.CreateDirectory(tempRootDirectory);
        return tempRootDirectory;
    }

    public string CreateArchiveFilePath()
    {
        return Path.Combine(EnsureTempRootDirectory(), $"{ArchiveFilePrefix}{Guid.NewGuid():N}{ArchiveFileExtension}");
    }

    public string CreateStagingDirectoryPath()
    {
        return Path.Combine(EnsureTempRootDirectory(), $"{StageDirectoryPrefix}{Guid.NewGuid():N}");
    }

    public AccountBackupTempCleanupResult DeleteExpiredArtifacts(int retentionHours, CancellationToken cancellationToken)
    {
        var tempRootDirectory = GetResolvedTempRootDirectory();
        if (!Directory.Exists(tempRootDirectory))
        {
            return AccountBackupTempCleanupResult.Empty(tempRootDirectory);
        }

        var retentionCutoff = DateTime.UtcNow.AddHours(-Math.Max(1, retentionHours));
        var deletedFiles = 0;
        var deletedDirectories = 0;
        var failedDeletes = 0;

        foreach (var filePath in Directory.EnumerateFiles(
                     tempRootDirectory,
                     $"{ArchiveFilePrefix}*{ArchiveFileExtension}",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.LastWriteTimeUtc > retentionCutoff)
            {
                continue;
            }

            if (TryDeleteFile(filePath))
            {
                deletedFiles++;
            }
            else
            {
                failedDeletes++;
            }
        }

        foreach (var directoryPath in Directory.EnumerateDirectories(
                     tempRootDirectory,
                     $"{StageDirectoryPrefix}*",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var directoryInfo = new DirectoryInfo(directoryPath);
            if (directoryInfo.LastWriteTimeUtc > retentionCutoff)
            {
                continue;
            }

            if (TryDeleteDirectory(directoryPath))
            {
                deletedDirectories++;
            }
            else
            {
                failedDeletes++;
            }
        }

        return new AccountBackupTempCleanupResult(
            tempRootDirectory,
            deletedFiles,
            deletedDirectories,
            failedDeletes);
    }

    public string GetResolvedTempRootDirectory()
    {
        return string.IsNullOrWhiteSpace(_backupOptions.TempDirectory)
            ? Path.GetTempPath()
            : _backupOptions.TempDirectory;
    }

    public static bool TryDeleteFile(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            File.Delete(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return true;
        }

        try
        {
            Directory.Delete(path, true);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record AccountBackupTempCleanupResult(
    string TempRootDirectory,
    int DeletedFiles,
    int DeletedDirectories,
    int FailedDeletes)
{
    public static AccountBackupTempCleanupResult Empty(string tempRootDirectory)
    {
        return new AccountBackupTempCleanupResult(tempRootDirectory, 0, 0, 0);
    }
}