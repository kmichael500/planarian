using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Notifications.Services;
using Planarian.Shared.Models;

namespace Planarian.Modules.Account.Archive.Services;

public class ArchiveJobCoordinator
{
    public const int MaxRetainedArchives = 5;
    public const string ArchivePrefix = "archives/";
    public const string TempArchivePrefix = "archives/temp/";
    internal static readonly TimeSpan AbandonedTempArchiveMinimumAge = TimeSpan.FromDays(7);

    private readonly ConcurrentDictionary<string, ArchiveJobHandle> _jobs = new();
    private readonly HashSet<string> _resettingAccounts = new(StringComparer.Ordinal);
    private readonly object _accountMaintenanceGate = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AccountMaintenanceLockRepository _maintenance;
    private readonly ILogger<ArchiveJobCoordinator> _logger;

    public ArchiveJobCoordinator(IServiceScopeFactory scopeFactory, AccountMaintenanceLockRepository maintenance,
        ILogger<ArchiveJobCoordinator>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _maintenance = maintenance;
        _logger = logger ?? NullLogger<ArchiveJobCoordinator>.Instance;
    }

    public static string GetGroupName(string accountId) => $"archive-{accountId}";

    // Guard reset/archive admission as one atomic operation on this application instance.
    // A running archive must not repopulate an account container during reset.
    public bool TryBeginAccountReset(string accountId)
    {
        lock (_accountMaintenanceGate)
        {
            if (_resettingAccounts.Contains(accountId) || _jobs.ContainsKey(accountId))
                return false;
            return _resettingAccounts.Add(accountId);
        }
    }

    public void CompleteAccountReset(string accountId)
    {
        lock (_accountMaintenanceGate)
            _resettingAccounts.Remove(accountId);
    }

    public async Task<bool> StartArchiveJobAsync(string accountId, string accountContainerName,
        CancellationToken cancellationToken = default)
    {
        var archiveLease = await _maintenance.TryAcquireArchiveAsync(
            accountId, cancellationToken);
        if (archiveLease is null) return false;

        var jobHandle = new ArchiveJobHandle(accountId, accountContainerName, new CancellationTokenSource())
        {
            StatusMessage = "Preparing archive..."
        };
        lock (_accountMaintenanceGate)
        {
            if (_resettingAccounts.Contains(accountId) || !_jobs.TryAdd(accountId, jobHandle))
            {
                jobHandle.CancellationTokenSource.Dispose();
                // Cannot await under the in-process gate; release just below.
                jobHandle = null!;
            }
        }

        if (jobHandle is null)
        {
            await archiveLease.DisposeAsync();
            return false;
        }

        _ = Task.Run(async () =>
        {
            try { await RunArchiveJob(jobHandle); }
            finally { await archiveLease.DisposeAsync(); }
        }, CancellationToken.None);
        return true;
    }

    public void CancelArchiveJob(string accountId)
    {
        if (_jobs.TryGetValue(accountId, out var jobHandle))
        {
            jobHandle.CancellationTokenSource.Cancel();
        }
    }

    public ArchiveProgressVm? GetArchiveStatus(string accountId)
    {
        if (!_jobs.TryGetValue(accountId, out var jobHandle))
        {
            return null;
        }

        lock (jobHandle.SyncRoot)
        {
            return jobHandle.ToProgressVm();
        }
    }

    public string? GetActiveArchiveBlobKey(string accountId)
    {
        if (!_jobs.TryGetValue(accountId, out var jobHandle))
        {
            return null;
        }

        lock (jobHandle.SyncRoot)
        {
            return jobHandle.ArchiveBlobKey;
        }
    }

    private async Task RunArchiveJob(ArchiveJobHandle jobHandle)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var accountRepository = scope.ServiceProvider.GetRequiredService<AccountRepository>();
            var fileService = scope.ServiceProvider.GetRequiredService<FileService>();
            var exportService = scope.ServiceProvider.GetRequiredService<ExportService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var groupName = GetGroupName(jobHandle.AccountId);
            var fileName = await BuildArchiveFileName(accountRepository, jobHandle.AccountId);
            var finalArchiveBlobKey = $"{ArchivePrefix}{fileName}";
            jobHandle.ArchiveBlobKey = $"{TempArchivePrefix}{fileName}";
            jobHandle.ContainerName = jobHandle.AccountContainerName;

            var processedCount = 0;
            var totalCount = 0;
            var accountBlobContainerClient = await fileService.GetBlobContainerClient(
                jobHandle.AccountContainerName,
                createIfNotExists: true);
            await CleanupAbandonedTempArchives(
                fileService,
                accountBlobContainerClient,
                jobHandle.AccountContainerName,
                jobHandle.ArchiveBlobKey,
                CancellationToken.None);
            await using (var outputStream = await fileService.OpenBlobWriteStream(
                             accountBlobContainerClient,
                             jobHandle.ArchiveBlobKey,
                             jobHandle.CancellationTokenSource.Token))
            {
                await exportService.WriteArchive(
                    outputStream,
                    jobHandle.AccountId,
                    jobHandle.AccountContainerName,
                    async (processed, total) =>
                    {
                        lock (jobHandle.SyncRoot)
                        {
                            jobHandle.StatusMessage = $"Exported {processed} of {total} files...";
                            jobHandle.ProcessedCount = processed;
                            jobHandle.TotalCount = total;
                        }

                        processedCount = processed;
                        totalCount = total;
                        await notificationService.SendNotificationToGroupAsync(groupName, new ArchiveProgressVm
                        {
                            State = ProgressState.Running,
                            StatusMessage = $"Exported {processed} of {total} files...",
                            ProcessedCount = processed,
                            TotalCount = total
                        });
                    },
                    async message =>
                    {
                        lock (jobHandle.SyncRoot)
                        {
                            jobHandle.StatusMessage = message;
                            jobHandle.ProcessedCount = processedCount;
                            jobHandle.TotalCount = totalCount;
                        }

                        await notificationService.SendNotificationToGroupAsync(groupName, new ArchiveProgressVm
                        {
                            State = ProgressState.Running,
                            StatusMessage = message,
                            ProcessedCount = processedCount,
                            TotalCount = totalCount
                        });
                    },
                    jobHandle.CancellationTokenSource.Token);
            }

            jobHandle.CancellationTokenSource.Token.ThrowIfCancellationRequested();

            var publishedTempBlobKey = jobHandle.ArchiveBlobKey;
            await PublishArchive(
                accountBlobContainerClient,
                publishedTempBlobKey,
                finalArchiveBlobKey,
                CancellationToken.None);

            // The final archive is authoritative once the copy completes. Temporary/archive
            // retention cleanup must not retroactively turn that successful export into a
            // failed job merely because Azure cleanup is temporarily unavailable.
            if (!await TryDeleteArchiveObjectAsync(
                    fileService, publishedTempBlobKey, jobHandle.AccountContainerName, CancellationToken.None))
            {
                _logger.LogWarning(
                    "Published archive {ArchiveKey} for account {AccountId}, but temporary archive {TempKey} could not be deleted; a later archive job will retry it",
                    finalArchiveBlobKey, jobHandle.AccountId, publishedTempBlobKey);
            }
            jobHandle.ArchiveBlobKey = null;

            await PruneOldArchivesBestEffort(
                fileService,
                accountBlobContainerClient,
                jobHandle.AccountContainerName,
                finalArchiveBlobKey,
                CancellationToken.None);

            await notificationService.SendNotificationToGroupAsync(groupName, new ArchiveProgressVm
            {
                State = ProgressState.Completed,
                StatusMessage = "Archive ready.",
                ProcessedCount = processedCount,
                TotalCount = totalCount,
                FileName = fileName
            });
        }
        catch (OperationCanceledException)
        {
            await CleanupPartialArchive(jobHandle);

            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
            await notificationService.SendNotificationToGroupAsync(GetGroupName(jobHandle.AccountId), new ArchiveProgressVm
            {
                State = ProgressState.Canceled,
                StatusMessage = "Archive canceled."
            });
        }
        catch (Exception ex)
        {
            await CleanupPartialArchive(jobHandle);

            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
            await notificationService.SendNotificationToGroupAsync(GetGroupName(jobHandle.AccountId), new ArchiveProgressVm
            {
                State = ProgressState.Failed,
                StatusMessage = $"Archive failed. {ex.Message}"
            });
        }
        finally
        {
            _jobs.TryRemove(jobHandle.AccountId, out _);
            jobHandle.CancellationTokenSource.Dispose();
        }
    }

    private async Task PruneOldArchivesBestEffort(
        FileService fileService,
        Azure.Storage.Blobs.BlobContainerClient accountBlobContainerClient,
        string accountContainerName,
        string currentArchiveBlobKey,
        CancellationToken cancellationToken)
    {
        List<(string BlobKey, DateTimeOffset LastModified)> existingArchives = [];
        try
        {
            await foreach (var blobItem in accountBlobContainerClient.GetBlobsAsync(
                               prefix: ArchivePrefix, cancellationToken: cancellationToken))
            {
                if (string.Equals(blobItem.Name, currentArchiveBlobKey, StringComparison.Ordinal) ||
                    blobItem.Name.StartsWith(TempArchivePrefix, StringComparison.OrdinalIgnoreCase) ||
                    !blobItem.Properties.LastModified.HasValue)
                {
                    continue;
                }

                existingArchives.Add((blobItem.Name, blobItem.Properties.LastModified.Value));
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Archive {ArchiveKey} was published, but existing archives could not be listed for pruning; a later archive job will retry",
                currentArchiveBlobKey);
            return;
        }

        var archivesToDelete = SelectArchivesToPrune(existingArchives);
        var failures = await DeleteArchiveKeysBestEffortAsync(
            archivesToDelete, key => fileService.DeleteFile(key, accountContainerName));
        foreach (var failedKey in failures)
        {
            _logger.LogWarning(
                "Archive {ArchiveKey} was published, but old archive {OldArchiveKey} could not be pruned; a later archive job will retry",
                currentArchiveBlobKey, failedKey);
        }
    }

    private async Task CleanupAbandonedTempArchives(
        FileService fileService,
        Azure.Storage.Blobs.BlobContainerClient accountBlobContainerClient,
        string accountContainerName,
        string currentTempArchiveBlobKey,
        CancellationToken cancellationToken)
    {
        var tempArchives = new List<(string BlobKey, DateTimeOffset LastModified)>();
        try
        {
            await foreach (var blobItem in accountBlobContainerClient.GetBlobsAsync(
                               prefix: TempArchivePrefix, cancellationToken: cancellationToken))
            {
                if (blobItem.Properties.LastModified.HasValue)
                    tempArchives.Add((blobItem.Name, blobItem.Properties.LastModified.Value));
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Unable to list abandoned temporary archives for account container {Container}; archive creation will continue",
                accountContainerName);
            return;
        }

        var abandonedKeys = SelectAbandonedTempArchives(
            tempArchives, currentTempArchiveBlobKey, DateTimeOffset.UtcNow);
        var failures = await DeleteArchiveKeysBestEffortAsync(
            abandonedKeys, key => fileService.DeleteFile(key, accountContainerName));
        foreach (var failedKey in failures)
        {
            _logger.LogWarning(
                "Abandoned temporary archive {TempArchiveKey} could not be deleted from {Container}; a later archive job will retry",
                failedKey, accountContainerName);
        }
    }

    internal static IReadOnlyList<string> SelectArchivesToPrune(
        IEnumerable<(string BlobKey, DateTimeOffset LastModified)> existingArchives) =>
        existingArchives
            .OrderByDescending(x => x.LastModified)
            .ThenByDescending(x => x.BlobKey, StringComparer.Ordinal)
            .Skip(MaxRetainedArchives - 1)
            .Select(x => x.BlobKey)
            .ToList();

    internal static IReadOnlyList<string> SelectAbandonedTempArchives(
        IEnumerable<(string BlobKey, DateTimeOffset LastModified)> tempArchives,
        string currentTempArchiveBlobKey, DateTimeOffset now) =>
        tempArchives
            .Where(item => !string.Equals(
                               item.BlobKey, currentTempArchiveBlobKey, StringComparison.Ordinal) &&
                           item.LastModified <= now - AbandonedTempArchiveMinimumAge)
            .Select(item => item.BlobKey)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    internal static async Task<IReadOnlyList<string>> DeleteArchiveKeysBestEffortAsync(
        IEnumerable<string> blobKeys, Func<string, Task> deleteAsync)
    {
        var failures = new List<string>();
        foreach (var key in blobKeys.Distinct(StringComparer.Ordinal))
        {
            try
            {
                await deleteAsync(key);
            }
            catch
            {
                failures.Add(key);
            }
        }

        return failures;
    }

    private static async Task<bool> TryDeleteArchiveObjectAsync(
        FileService fileService, string blobKey, string accountContainerName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failures = await DeleteArchiveKeysBestEffortAsync(
            [blobKey], key => fileService.DeleteFile(key, accountContainerName));
        return failures.Count == 0;
    }

    private static async Task PublishArchive(
        Azure.Storage.Blobs.BlobContainerClient accountBlobContainerClient,
        string tempArchiveBlobKey,
        string finalArchiveBlobKey,
        CancellationToken cancellationToken)
    {
        var tempBlobClient = accountBlobContainerClient.GetBlobClient(tempArchiveBlobKey);
        var finalBlobClient = accountBlobContainerClient.GetBlobClient(finalArchiveBlobKey);

        var copyOperation = await finalBlobClient.StartCopyFromUriAsync(
            tempBlobClient.Uri,
            cancellationToken: cancellationToken);
        await copyOperation.WaitForCompletionAsync(cancellationToken);
    }

    private async Task CleanupPartialArchive(ArchiveJobHandle jobHandle)
    {
        if (string.IsNullOrWhiteSpace(jobHandle.ArchiveBlobKey) || string.IsNullOrWhiteSpace(jobHandle.ContainerName))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var fileService = scope.ServiceProvider.GetRequiredService<FileService>();
        if (!await TryDeleteArchiveObjectAsync(
                fileService, jobHandle.ArchiveBlobKey, jobHandle.ContainerName, CancellationToken.None))
        {
            _logger.LogWarning(
                "Partial archive {ArchiveKey} for account {AccountId} could not be deleted; the next archive job will retry it",
                jobHandle.ArchiveBlobKey, jobHandle.AccountId);
        }
    }

    private static async Task<string> BuildArchiveFileName(AccountRepository accountRepository, string accountId)
    {
        var accountName = await accountRepository.GetAccountName(accountId) ?? "Account";
        var normalizedAccountName = string.IsNullOrWhiteSpace(accountName)
            ? "Account"
            : NormalizeArchiveFileNameSegment(accountName);
        return $"{normalizedAccountName} Archive {DateTime.UtcNow:yyyyMMddHHmmss}.tar.gz";
    }

    private static string NormalizeArchiveFileNameSegment(string value)
    {
        var normalizedValue = value.Trim();

        foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
        {
            normalizedValue = normalizedValue.Replace(invalidCharacter, '-');
        }

        normalizedValue = normalizedValue.Trim().TrimEnd('.', ' ');
        return string.IsNullOrWhiteSpace(normalizedValue) ? "Account" : normalizedValue;
    }

    private sealed class ArchiveJobHandle
    {
        public ArchiveJobHandle(string accountId, string accountContainerName, CancellationTokenSource cancellationTokenSource)
        {
            AccountId = accountId;
            AccountContainerName = accountContainerName;
            CancellationTokenSource = cancellationTokenSource;
        }

        public object SyncRoot { get; } = new();
        public string AccountId { get; }
        public string AccountContainerName { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        public string? ArchiveBlobKey { get; set; }
        public string? ContainerName { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public int? ProcessedCount { get; set; }
        public int? TotalCount { get; set; }
        public ArchiveProgressVm ToProgressVm()
        {
            return new ArchiveProgressVm
            {
                State = ProgressState.Running,
                StatusMessage = StatusMessage,
                ProcessedCount = ProcessedCount,
                TotalCount = TotalCount
            };
        }
    }
}
