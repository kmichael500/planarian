using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planarian.Shared.Options;

namespace Planarian.Shared.Services;

public sealed class TemporaryBackupCleanupService : BackgroundService
{
    private readonly BlobService _blobService;
    private readonly BackupOptions _backupOptions;
    private readonly ILogger<TemporaryBackupCleanupService> _logger;

    public TemporaryBackupCleanupService(
        BlobService blobService,
        BackupOptions backupOptions,
        ILogger<TemporaryBackupCleanupService> logger)
    {
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
                await _blobService.DeleteExpiredTemporaryBackupBlobs(
                    _backupOptions.TempBlobExpirationHours,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to clean up expired temporary backup blobs.");
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
}