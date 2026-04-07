using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planarian.Modules.Account.Backup.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;

namespace Planarian.Shared.HostedServices;

public sealed class TemporaryBackupCleanupService : BackgroundService
{
    private readonly AccountBackupTempStorageService _accountBackupTempStorageService;
    private readonly BlobService _blobService;
    private readonly BackupOptions _backupOptions;
    private readonly ILogger<TemporaryBackupCleanupService> _logger;

    public TemporaryBackupCleanupService(
        AccountBackupTempStorageService accountBackupTempStorageService,
        BlobService blobService,
        BackupOptions backupOptions,
        ILogger<TemporaryBackupCleanupService> logger)
    {
        _accountBackupTempStorageService = accountBackupTempStorageService;
        _blobService = blobService;
        _backupOptions = backupOptions;
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
                _logger.LogError(ex, "Failed to run temporary backup cleanup pass.");
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
                _backupOptions.TempBlobExpirationHours,
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
            _logger.LogError(ex, "Failed to clean up expired local backup temp files.");
        }

        try
        {
            await _blobService.DeleteExpiredTemporaryBackupBlobs(
                _backupOptions.TempBlobExpirationHours,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up expired temporary backup blobs.");
        }
    }
}