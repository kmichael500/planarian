using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Azure.Storage.Sas;
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
    // Azure Put Block From URL supports 50,000 committed blocks and 4,000 MiB per block
    // for service version 2020-04-08 and later. Azure.Storage.Blobs 12.20.0 uses a later version.
    internal const int MaxArchivePublicationBlocks = 50_000;
    internal const long MaxArchivePublicationBlockBytes = 4_000L * 1024 * 1024;
    internal const long PreferredArchivePublicationBlockBytes = 256L * 1024 * 1024;
    internal static readonly TimeSpan AbandonedTempArchiveMinimumAge = TimeSpan.FromDays(7);
    internal static readonly TimeSpan ArchiveSourceSasValidity = TimeSpan.FromHours(1);
    private static readonly TimeSpan ArchiveSourceSasClockSkewAllowance = TimeSpan.FromMinutes(15);

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
            return jobHandle.FinalArchiveBlobKey;
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
            var (tempArchiveBlobKey, finalArchiveBlobKey) = BuildArchiveBlobKeys(fileName, Guid.NewGuid());
            jobHandle.TempArchiveBlobKey = tempArchiveBlobKey;
            jobHandle.FinalArchiveBlobKey = finalArchiveBlobKey;
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
                jobHandle.TempArchiveBlobKey,
                CancellationToken.None);
            await using (var outputStream = await fileService.OpenBlobWriteStream(
                             accountBlobContainerClient,
                             jobHandle.TempArchiveBlobKey,
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

            var publishedTempBlobKey = jobHandle.TempArchiveBlobKey;
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
            jobHandle.TempArchiveBlobKey = null;

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
        catch (ArchivePublicationUncertainException)
        {
            // Publication reached a point where a client-side timeout or transport error
            // cannot prove whether the atomic final commit succeeded. Preserve the completed
            // temp source so a retry or later age-gated cleanup remains safe.
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
            await notificationService.SendNotificationToGroupAsync(GetGroupName(jobHandle.AccountId), new ArchiveProgressVm
            {
                State = ProgressState.Failed,
                StatusMessage = "Archive publication result is uncertain. The completed temporary archive was preserved for recovery."
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
        List<string> invalidLegacyCopyArchives = [];
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

                if (IsArchiveCopyFailed(blobItem.Properties.CopyStatus, blobItem.Properties.CopyId))
                {
                    invalidLegacyCopyArchives.Add(blobItem.Name);
                    continue;
                }

                if (!IsArchiveReady(blobItem.Properties.CopyStatus, blobItem.Properties.CopyId))
                    continue;

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

        var archivesToDelete = invalidLegacyCopyArchives
            .Concat(SelectArchivesToPrune(existingArchives));
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
        var safeToDelete = new List<string>(abandonedKeys.Count);
        foreach (var abandonedKey in abandonedKeys)
        {
            if (await IsAbandonedTempSafeToDelete(
                    accountBlobContainerClient, abandonedKey, cancellationToken))
            {
                safeToDelete.Add(abandonedKey);
            }
        }

        var failures = await DeleteArchiveKeysBestEffortAsync(
            safeToDelete, key => fileService.DeleteFile(key, accountContainerName));
        foreach (var failedKey in failures)
        {
            _logger.LogWarning(
                "Abandoned temporary archive {TempArchiveKey} could not be deleted from {Container}; a later archive job will retry",
                failedKey, accountContainerName);
        }
    }

    private async Task<bool> IsAbandonedTempSafeToDelete(
        Azure.Storage.Blobs.BlobContainerClient accountBlobContainerClient,
        string tempArchiveBlobKey,
        CancellationToken cancellationToken)
    {
        var finalArchiveBlobKey = TryGetFinalArchiveBlobKey(tempArchiveBlobKey);
        if (finalArchiveBlobKey is null)
        {
            _logger.LogWarning(
                "Skipping malformed abandoned temporary archive key {TempArchiveKey}",
                tempArchiveBlobKey);
            return false;
        }

        try
        {
            var properties = await accountBlobContainerClient.GetBlobClient(finalArchiveBlobKey)
                .GetPropertiesAsync(cancellationToken: cancellationToken);
            return !IsArchiveCopyInProgress(properties.Value.CopyStatus, properties.Value.CopyId);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Unable to verify publication state for abandoned temporary archive {TempArchiveKey}; leaving it in place",
                tempArchiveBlobKey);
            return false;
        }
    }

    internal static string? TryGetFinalArchiveBlobKey(string tempArchiveBlobKey)
    {
        if (!tempArchiveBlobKey.StartsWith(TempArchivePrefix, StringComparison.Ordinal) ||
            tempArchiveBlobKey.Length == TempArchivePrefix.Length)
        {
            return null;
        }

        return ArchivePrefix + tempArchiveBlobKey[TempArchivePrefix.Length..];
    }

    internal static IReadOnlyList<string> SelectArchivesToPrune(
        IEnumerable<(string BlobKey, DateTimeOffset LastModified)> existingArchives) =>
        existingArchives
            .GroupBy(x => x.BlobKey, StringComparer.Ordinal)
            .Select(group => (BlobKey: group.Key, LastModified: group.Max(x => x.LastModified)))
            .OrderByDescending(x => x.LastModified)
            .ThenByDescending(x => x.BlobKey, StringComparer.Ordinal)
            .Skip(MaxRetainedArchives - 1)
            .Select(x => x.BlobKey)
            .ToList();

    internal static bool IsArchiveReady(CopyStatus? copyStatus, string? copyId) =>
        string.IsNullOrWhiteSpace(copyId) || copyStatus == CopyStatus.Success;

    internal static bool IsArchiveCopyInProgress(CopyStatus? copyStatus, string? copyId) =>
        !string.IsNullOrWhiteSpace(copyId) && copyStatus == CopyStatus.Pending;

    internal static bool IsArchiveCopyFailed(CopyStatus? copyStatus, string? copyId) =>
        !string.IsNullOrWhiteSpace(copyId) &&
        copyStatus is CopyStatus.Failed or CopyStatus.Aborted;

    internal static (string TempBlobKey, string FinalBlobKey) BuildArchiveBlobKeys(
        string fileName, Guid operationId)
    {
        var operationSegment = operationId.ToString("N");
        return (
            $"{TempArchivePrefix}{operationSegment}/{fileName}",
            $"{ArchivePrefix}{operationSegment}/{fileName}");
    }

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

    internal static async Task PublishArchive(
        Azure.Storage.Blobs.BlobContainerClient accountBlobContainerClient,
        string tempArchiveBlobKey,
        string finalArchiveBlobKey,
        CancellationToken cancellationToken,
        long preferredBlockSizeBytes = PreferredArchivePublicationBlockBytes)
    {
        var tempBlobClient = accountBlobContainerClient.GetBlobClient(tempArchiveBlobKey);
        var finalBlobClient = accountBlobContainerClient.GetBlobClient(finalArchiveBlobKey);
        var finalBlockBlobClient = accountBlobContainerClient.GetBlockBlobClient(finalArchiveBlobKey);

        // Reading source properties happens before publication begins. A failure here has a
        // known outcome and can use normal partial-temp cleanup. The ETag is then pinned on
        // every server-side block copy so a source mutation cannot produce a mixed archive.
        var tempProperties = (await tempBlobClient.GetPropertiesAsync(
            cancellationToken: cancellationToken)).Value;
        var blocks = BuildArchivePublicationBlocks(
            tempProperties.ContentLength,
            preferredBlockSizeBytes);

        try
        {
            foreach (var block in blocks)
            {
                // The SAS stays inside the backend-to-Azure publication path and is never returned
                // to a client. Generate a fresh, blob-scoped, read-only credential for each
                // Azure-to-Azure block copy so even very large archives do not require one
                // long-lived bearer token or relay their bytes through Planarian.
                var sourceUri = CreateArchiveSourceReadSasUri(
                    tempBlobClient,
                    DateTimeOffset.UtcNow);
                await finalBlockBlobClient.StageBlockFromUriAsync(
                    sourceUri,
                    block.BlockId,
                    new StageBlockFromUriOptions
                    {
                        SourceRange = new HttpRange(block.Offset, block.Length),
                        SourceConditions = new RequestConditions { IfMatch = tempProperties.ETag }
                    },
                    cancellationToken);
            }

            // Uncommitted blocks are not a published archive. The single conditional block-list
            // commit makes the final blob visible atomically and refuses to overwrite an existing
            // destination, preserving mixed-version/retry safety.
            await finalBlockBlobClient.CommitBlockListAsync(
                blocks.Select(block => block.BlockId),
                new CommitBlockListOptions
                {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                },
                cancellationToken);
        }
        catch (Exception publicationException)
        {
            // A commit can succeed even if its response is lost. Reconcile against storage
            // before deciding whether publication failed. Hashing is deliberately an error-path
            // operation; the normal publication path never relays archive bytes through the app.
            ArchivePublicationVerification verification;
            try
            {
                verification = await VerifyPublishedArchiveAsync(
                    tempBlobClient, finalBlobClient, CancellationToken.None);
            }
            catch (Exception verificationException)
            {
                throw new ArchivePublicationUncertainException(
                    finalArchiveBlobKey,
                    new AggregateException(publicationException, verificationException));
            }

            switch (verification)
            {
                case ArchivePublicationVerification.Identical:
                    return;
                case ArchivePublicationVerification.Missing:
                    throw new IOException(
                        "Archive publication failed before a final archive was committed.",
                        publicationException);
                case ArchivePublicationVerification.Different:
                    throw new InvalidOperationException(
                        "Archive publication destination already exists with different content.",
                        publicationException);
                default:
                    throw new ArchivePublicationUncertainException(
                        finalArchiveBlobKey, publicationException);
            }
        }
    }

    internal static IReadOnlyList<ArchivePublicationBlock> BuildArchivePublicationBlocks(
        long contentLength,
        long preferredBlockSizeBytes = PreferredArchivePublicationBlockBytes)
    {
        if (contentLength < 0)
            throw new ArgumentOutOfRangeException(nameof(contentLength));
        if (preferredBlockSizeBytes <= 0 || preferredBlockSizeBytes > MaxArchivePublicationBlockBytes)
            throw new ArgumentOutOfRangeException(nameof(preferredBlockSizeBytes));

        var maxSupportedContentLength =
            MaxArchivePublicationBlockBytes * MaxArchivePublicationBlocks;
        if (contentLength > maxSupportedContentLength)
        {
            throw new InvalidOperationException(
                $"Archive size {contentLength} bytes exceeds Azure block-blob publication capacity.");
        }

        if (contentLength == 0)
            return [];

        var minimumBlockSizeForBlockCount =
            ((contentLength - 1) / MaxArchivePublicationBlocks) + 1;
        var blockSize = Math.Max(preferredBlockSizeBytes, minimumBlockSizeForBlockCount);
        var blockCount = checked((int)(((contentLength - 1) / blockSize) + 1));
        var blocks = new List<ArchivePublicationBlock>(blockCount);

        for (var index = 0; index < blockCount; index++)
        {
            var offset = index * blockSize;
            var length = Math.Min(blockSize, contentLength - offset);
            var blockId = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(index.ToString("D8", CultureInfo.InvariantCulture)));
            blocks.Add(new ArchivePublicationBlock(blockId, offset, length));
        }

        return blocks;
    }

    internal static Uri CreateArchiveSourceReadSasUri(
        Azure.Storage.Blobs.BlobClient sourceBlobClient,
        DateTimeOffset now)
    {
        if (!sourceBlobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Archive server-side publication requires shared-key Blob Storage credentials capable of generating a source read SAS.");
        }

        var startsOn = now - ArchiveSourceSasClockSkewAllowance;
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = sourceBlobClient.BlobContainerName,
            BlobName = sourceBlobClient.Name,
            Resource = "b",
            StartsOn = startsOn,
            ExpiresOn = startsOn + ArchiveSourceSasValidity
        };
        if (string.Equals(sourceBlobClient.Uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            sasBuilder.Protocol = SasProtocol.Https;

        sasBuilder.SetPermissions(BlobSasPermissions.Read);
        return sourceBlobClient.GenerateSasUri(sasBuilder);
    }

    private static async Task<ArchivePublicationVerification> VerifyPublishedArchiveAsync(
        Azure.Storage.Blobs.BlobClient tempBlobClient,
        Azure.Storage.Blobs.BlobClient finalBlobClient,
        CancellationToken cancellationToken)
    {
        BlobProperties finalProperties;
        try
        {
            finalProperties = (await finalBlobClient.GetPropertiesAsync(
                cancellationToken: cancellationToken)).Value;
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
            return ArchivePublicationVerification.Missing;
        }

        var tempProperties = (await tempBlobClient.GetPropertiesAsync(
            cancellationToken: cancellationToken)).Value;
        if (tempProperties.ContentLength != finalProperties.ContentLength)
            return ArchivePublicationVerification.Different;

        await using var tempStream = await tempBlobClient.OpenReadAsync(
            cancellationToken: cancellationToken);
        await using var finalStream = await finalBlobClient.OpenReadAsync(
            cancellationToken: cancellationToken);

        using var tempHashAlgorithm = SHA256.Create();
        using var finalHashAlgorithm = SHA256.Create();
        var tempHash = await tempHashAlgorithm.ComputeHashAsync(tempStream, cancellationToken);
        var finalHash = await finalHashAlgorithm.ComputeHashAsync(finalStream, cancellationToken);

        return CryptographicOperations.FixedTimeEquals(tempHash, finalHash)
            ? ArchivePublicationVerification.Identical
            : ArchivePublicationVerification.Different;
    }

    internal readonly record struct ArchivePublicationBlock(string BlockId, long Offset, long Length);

    private enum ArchivePublicationVerification
    {
        Missing,
        Identical,
        Different
    }

    private async Task CleanupPartialArchive(ArchiveJobHandle jobHandle)
    {
        if (string.IsNullOrWhiteSpace(jobHandle.TempArchiveBlobKey) || string.IsNullOrWhiteSpace(jobHandle.ContainerName))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var fileService = scope.ServiceProvider.GetRequiredService<FileService>();
        if (!await TryDeleteArchiveObjectAsync(
                fileService, jobHandle.TempArchiveBlobKey, jobHandle.ContainerName, CancellationToken.None))
        {
            _logger.LogWarning(
                "Partial archive {ArchiveKey} for account {AccountId} could not be deleted; the next archive job will retry it",
                jobHandle.TempArchiveBlobKey, jobHandle.AccountId);
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

    internal sealed class ArchivePublicationUncertainException(
        string finalArchiveBlobKey, Exception innerException)
        : IOException($"Archive publication outcome is uncertain for '{finalArchiveBlobKey}'.", innerException);

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
        public string? TempArchiveBlobKey { get; set; }
        public string? FinalArchiveBlobKey { get; set; }
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
