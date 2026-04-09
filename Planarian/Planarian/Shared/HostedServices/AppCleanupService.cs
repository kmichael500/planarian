using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planarian.Modules.Account.Backup.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Shared.HostedServices;

public sealed class AppCleanupService : BackgroundService
{
    private readonly AccountBackupTempStorageService _accountBackupTempStorageService;
    private readonly BlobService _blobService;
    private readonly BackupOptions _backupOptions;
    private readonly FileOptions _fileOptions;
    private readonly ILogger<AppCleanupService> _logger;

    public AppCleanupService(
        AccountBackupTempStorageService accountBackupTempStorageService,
        BlobService blobService,
        BackupOptions backupOptions,
        FileOptions fileOptions,
        ILogger<AppCleanupService> logger)
    {
        _accountBackupTempStorageService = accountBackupTempStorageService;
        _blobService = blobService;
        _backupOptions = backupOptions;
        _fileOptions = fileOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanupInterval = TimeSpan.FromMinutes(Math.Max(5, _backupOptions.TempBlobCleanupIntervalMinutes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanupPass(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run app cleanup pass.");
            }

            try
            {
                await Task.Delay(cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunCleanupPass(CancellationToken stoppingToken)
    {
        try
        {
            var cleanupResult = _accountBackupTempStorageService.DeleteExpiredArtifacts(
                _fileOptions.TempExpirationHours,
                stoppingToken);

            if (cleanupResult.DeletedFiles > 0 || cleanupResult.DeletedDirectories > 0)
            {
                _logger.LogInformation(
                    "Deleted {DeletedFiles} stale backup archive files and {DeletedDirectories} staging directories from {TempRootDirectory}.",
                    cleanupResult.DeletedFiles,
                    cleanupResult.DeletedDirectories,
                    cleanupResult.TempRootDirectory);
            }

            if (cleanupResult.FailedDeletes > 0)
            {
                _logger.LogWarning(
                    "Failed to delete {FailedDeletes} stale backup temp artifacts from {TempRootDirectory}.",
                    cleanupResult.FailedDeletes,
                    cleanupResult.TempRootDirectory);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up expired local backup temp files during app cleanup.");
        }

        try
        {
            await _blobService.DeleteExpiredTemporaryBackupBlobs(
            _fileOptions.TempExpirationHours,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up expired temporary backup blobs during app cleanup.");
        }
    }
}