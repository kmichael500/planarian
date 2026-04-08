using Planarian.Shared.Options;

namespace Planarian.Modules.Account.Backup.Services;

public sealed class TempCleanupResult
{
    public int DeletedFiles { get; set; }
    public int DeletedDirectories { get; set; }
    public int FailedDeletes { get; set; }
    public string TempRootDirectory { get; set; } = string.Empty;
}

public class AccountBackupTempStorageService
{
    private readonly BackupOptions _backupOptions;

    public AccountBackupTempStorageService(BackupOptions backupOptions)
    {
        _backupOptions = backupOptions;
    }

    private string GetTempRootDirectory()
    {
        return string.IsNullOrWhiteSpace(_backupOptions.TempDirectory)
            ? Path.Combine(Path.GetTempPath(), "planarian-backups")
            : _backupOptions.TempDirectory;
    }

    public TempCleanupResult DeleteExpiredArtifacts(int expirationHours, CancellationToken cancellationToken)
    {
        var tempRoot = GetTempRootDirectory();
        var result = new TempCleanupResult { TempRootDirectory = tempRoot };

        if (!Directory.Exists(tempRoot))
        {
            return result;
        }

        var cutoff = DateTime.UtcNow.AddHours(-Math.Max(1, expirationHours));

        foreach (var file in Directory.EnumerateFiles(tempRoot, "*", SearchOption.AllDirectories))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var lastWrite = System.IO.File.GetLastWriteTimeUtc(file);
                if (lastWrite <= cutoff)
                {
                    System.IO.File.Delete(file);
                    result.DeletedFiles++;
                }
            }
            catch
            {
                result.FailedDeletes++;
            }
        }

        foreach (var dir in Directory.EnumerateDirectories(tempRoot, "*", SearchOption.TopDirectoryOnly))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                    result.DeletedDirectories++;
                }
            }
            catch
            {
                result.FailedDeletes++;
            }
        }

        return result;
    }
}
