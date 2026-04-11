using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Notifications.Services;

namespace Planarian.Modules.Account.Archive.Services;

public class ArchiveJobCoordinator
{
    public const int MaxRetainedArchives = 5;
    public const string ArchivePrefix = "archives/";
    public const string TempArchivePrefix = "archives/temp/";

    private readonly ConcurrentDictionary<string, ArchiveJobHandle> _jobs = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public ArchiveJobCoordinator(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public static string GetGroupName(string accountId) => $"archive-{accountId}";

    public bool StartArchiveJob(string accountId, string userId)
    {
        var jobHandle = new ArchiveJobHandle(accountId, new CancellationTokenSource())
        {
            StatusMessage = "Preparing archive..."
        };

        if (!_jobs.TryAdd(accountId, jobHandle))
        {
            return false;
        }

        _ = Task.Run(() => RunArchiveJob(userId, jobHandle), CancellationToken.None);
        return true;
    }

    public Task CancelArchiveJob(string accountId)
    {
        if (_jobs.TryGetValue(accountId, out var jobHandle))
        {
            jobHandle.CancellationTokenSource.Cancel();
        }

        return Task.CompletedTask;
    }

    public ArchiveStatusVm? GetArchiveStatus(string accountId)
    {
        if (!_jobs.TryGetValue(accountId, out var jobHandle))
        {
            return null;
        }

        lock (jobHandle.SyncRoot)
        {
            return jobHandle.ToStatusVm();
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

    private async Task RunArchiveJob(string userId, ArchiveJobHandle jobHandle)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var requestUser = scope.ServiceProvider.GetRequiredService<RequestUser>();
            await requestUser.Initialize(jobHandle.AccountId, userId);

            var accountRepository = scope.ServiceProvider.GetRequiredService<AccountRepository>();
            var fileService = scope.ServiceProvider.GetRequiredService<FileService>();
            var exportService = scope.ServiceProvider.GetRequiredService<ExportService>();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

            var groupName = GetGroupName(jobHandle.AccountId);
            var fileName = await BuildArchiveFileName(accountRepository, jobHandle.AccountId);
            var finalArchiveBlobKey = $"{ArchivePrefix}{fileName}";
            jobHandle.ArchiveBlobKey = $"{TempArchivePrefix}{fileName}";
            jobHandle.ContainerName = requestUser.AccountContainerName;

            var processedCount = 0;
            var totalCount = 0;
            var accountBlobContainerClient = await fileService.GetAccountBlobContainerClient(createIfNotExists: true);
            await using (var outputStream = await fileService.OpenBlobWriteStream(
                             accountBlobContainerClient,
                             jobHandle.ArchiveBlobKey,
                             jobHandle.CancellationTokenSource.Token))
            {
                await exportService.WriteArchive(
                    outputStream,
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
                        await notificationService.SendNotificationToGroupAsync(groupName, new
                        {
                            statusMessage = $"Exported {processed} of {total} files...",
                            processedCount = processed,
                            totalCount = total
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

                        await notificationService.SendNotificationToGroupAsync(groupName, new
                        {
                            statusMessage = message,
                            processedCount,
                            totalCount
                        });
                    },
                    jobHandle.CancellationTokenSource.Token);
            }

            jobHandle.CancellationTokenSource.Token.ThrowIfCancellationRequested();

            await PublishArchive(
                accountBlobContainerClient,
                jobHandle.ArchiveBlobKey,
                finalArchiveBlobKey,
                CancellationToken.None);
            jobHandle.ArchiveBlobKey = null;

            await PruneOldArchives(
                fileService,
                accountBlobContainerClient,
                requestUser.AccountContainerName,
                finalArchiveBlobKey,
                CancellationToken.None);

            await notificationService.SendNotificationToGroupAsync(groupName, new
            {
                statusMessage = "Archive ready.",
                processedCount,
                totalCount,
                fileName,
                isComplete = true
            });
        }
        catch (OperationCanceledException)
        {
            await CleanupPartialArchive(jobHandle);

            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
            await notificationService.SendNotificationToGroupAsync(GetGroupName(jobHandle.AccountId), new
            {
                statusMessage = "Archive canceled.",
                isComplete = true,
                isCanceled = true
            });
        }
        catch (Exception ex)
        {
            await CleanupPartialArchive(jobHandle);

            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
            await notificationService.SendNotificationToGroupAsync(GetGroupName(jobHandle.AccountId), new
            {
                statusMessage = "Archive failed.",
                message = ex.Message,
                isComplete = true,
                isError = true
            });
        }
        finally
        {
            _jobs.TryRemove(jobHandle.AccountId, out _);
            jobHandle.CancellationTokenSource.Dispose();
        }
    }

    private static async Task PruneOldArchives(
        FileService fileService,
        Azure.Storage.Blobs.BlobContainerClient accountBlobContainerClient,
        string accountContainerName,
        string currentArchiveBlobKey,
        CancellationToken cancellationToken)
    {
        var existingArchives = new List<(string BlobKey, DateTimeOffset LastModified)>();

        await foreach (var blobItem in accountBlobContainerClient.GetBlobsAsync(prefix: "archives/", cancellationToken: cancellationToken))
        {
            if (string.Equals(blobItem.Name, currentArchiveBlobKey, StringComparison.Ordinal))
            {
                continue;
            }

            if (blobItem.Name.StartsWith(TempArchivePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!blobItem.Properties.LastModified.HasValue)
            {
                continue;
            }

            existingArchives.Add((blobItem.Name, blobItem.Properties.LastModified.Value));
        }

        var archivesToDelete = existingArchives
            .OrderByDescending(x => x.LastModified)
            .Skip(MaxRetainedArchives - 1)
            .ToList();

        foreach (var archiveToDelete in archivesToDelete)
        {
            await fileService.DeleteFile(archiveToDelete.BlobKey, accountContainerName);
        }
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
        await tempBlobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    private async Task CleanupPartialArchive(ArchiveJobHandle jobHandle)
    {
        if (string.IsNullOrWhiteSpace(jobHandle.ArchiveBlobKey) || string.IsNullOrWhiteSpace(jobHandle.ContainerName))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var requestUser = scope.ServiceProvider.GetRequiredService<RequestUser>();
        requestUser.AccountId = ExtractAccountIdFromContainerName(jobHandle.ContainerName);
        var fileService = scope.ServiceProvider.GetRequiredService<FileService>();
        await fileService.DeleteFile(jobHandle.ArchiveBlobKey, jobHandle.ContainerName);
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

    private static string ExtractAccountIdFromContainerName(string containerName)
    {
        return containerName.StartsWith("account-", StringComparison.OrdinalIgnoreCase)
            ? containerName["account-".Length..]
            : containerName;
    }

    private sealed class ArchiveJobHandle
    {
        public ArchiveJobHandle(string accountId, CancellationTokenSource cancellationTokenSource)
        {
            AccountId = accountId;
            CancellationTokenSource = cancellationTokenSource;
        }

        public object SyncRoot { get; } = new();
        public string AccountId { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        public string? ArchiveBlobKey { get; set; }
        public string? ContainerName { get; set; }
        public string StatusMessage { get; set; } = string.Empty;
        public int? ProcessedCount { get; set; }
        public int? TotalCount { get; set; }
        public string? Message { get; set; }
        public bool? IsError { get; set; }
        public bool? IsCanceled { get; set; }

        public ArchiveStatusVm ToStatusVm()
        {
            return new ArchiveStatusVm
            {
                IsActive = true,
                StatusMessage = StatusMessage,
                ProcessedCount = ProcessedCount,
                TotalCount = TotalCount,
                Message = Message,
                IsError = IsError,
                IsCanceled = IsCanceled
            };
        }
    }
}
