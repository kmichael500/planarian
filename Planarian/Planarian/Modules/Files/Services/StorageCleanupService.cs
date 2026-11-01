using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

using Planarian.Modules.Files.Persistence;
namespace Planarian.Modules.Files.Services;

/// <summary>
/// Executes durable post-commit object cleanup. Relational mutations queue targets;
/// this service performs the external deletion only after those mutations commit.
/// </summary>
public sealed class StorageCleanupService : AccountServiceBase
{
    private const int OpportunisticBatchSize = 16;
    private const int ExplicitBatchSize = 500;

    private static readonly StorageCleanupPurpose[] AssetRemovalPurposes =
        [StorageCleanupPurpose.CaveAssetRemoval];
    private static readonly StorageCleanupPurpose[] UnpublishedFilePurposes =
        [StorageCleanupPurpose.UnpublishedFileRemoval];
    private static readonly StorageCleanupPurpose[] ImportSourcePurposes =
        [StorageCleanupPurpose.ImportSourceRemoval];
    private static readonly StorageCleanupPurpose[] OrdinaryPurposes =
        [StorageCleanupPurpose.CaveAssetRemoval, StorageCleanupPurpose.UnpublishedFileRemoval,
         StorageCleanupPurpose.ImportSourceRemoval];

    private readonly StorageCleanupStore _store;
    private readonly IObjectStorage _objectStorage;
    private readonly ILogger<StorageCleanupService> _logger;

    public StorageCleanupService(
        StorageCleanupStore store,
        RequestUser requestUser,
        IObjectStorage objectStorage,
        ILogger<StorageCleanupService> logger) : base(requestUser)
    {
        _store = store;
        _objectStorage = objectStorage;
        _logger = logger;
    }

    public Task CleanupAssetRemovalTargetsAsync(IEnumerable<string>? targetIds = null,
        CancellationToken cancellationToken = default) =>
        CleanupAsync(AssetRemovalPurposes, targetIds, cancellationToken);

    public Task CleanupUnpublishedFileTargetsAsync(IEnumerable<string>? targetIds = null,
        CancellationToken cancellationToken = default) =>
        CleanupAsync(UnpublishedFilePurposes, targetIds, cancellationToken);

    public Task CleanupImportSourceTargetsAsync(IEnumerable<string>? targetIds = null,
        CancellationToken cancellationToken = default) =>
        CleanupAsync(ImportSourcePurposes, targetIds, cancellationToken);

    public Task CleanupOrdinaryTargetsAsync(IEnumerable<string>? targetIds = null,
        CancellationToken cancellationToken = default) =>
        CleanupAsync(OrdinaryPurposes, targetIds, cancellationToken);

    public async Task ReconcileUncertainCreateAsync(
        StorageObjectAddress address,
        StorageCleanupPurpose purpose)
    {
        try
        {
            _store.ForgetTrackedChanges();
            var targets = await _store.QueueAsync(
                purpose,
                [StorageCleanupAddress.Object(address)],
                CancellationToken.None);
            await CleanupAsync([purpose], targets.Select(target => target.Id), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Unable to reconcile uncertain object creation for account {AccountId} at {StorageKey}",
                AccountId, address.Key);
        }
    }

    public async Task<bool> TryDeleteObjectAsync(StorageObjectAddress address)
    {
        try
        {
            await _objectStorage.DeleteIfExistsAsync(address, CancellationToken.None);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task CleanupAsync(
        IReadOnlyCollection<StorageCleanupPurpose> purposes,
        IEnumerable<string>? targetIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = targetIds?.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();

        if (requestedIds is null)
        {
            await CleanupBatchAsync(purposes, null, OpportunisticBatchSize, cancellationToken);
            return;
        }

        foreach (var chunk in requestedIds.Chunk(ExplicitBatchSize))
            await CleanupBatchAsync(purposes, chunk, chunk.Length, cancellationToken);
    }

    private async Task CleanupBatchAsync(
        IReadOnlyCollection<StorageCleanupPurpose> purposes,
        IReadOnlyCollection<string>? targetIds,
        int maxCount,
        CancellationToken cancellationToken)
    {
        List<StorageCleanupTarget> pending;
        try
        {
            pending = await _store.GetTargetsAsync(purposes, targetIds, maxCount, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Unable to load object cleanup debt for account {AccountId}; cleanup will retry later",
                AccountId);
            return;
        }

        HashSet<string> referencedTargetIds;
        try
        {
            referencedTargetIds = await _store.GetTargetIdsReferencedByLiveObjectsAsync(
                pending, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Unable to validate live object references for {CleanupCount} cleanup targets in account {AccountId}; cleanup will retry later",
                pending.Count, AccountId);
            return;
        }

        var completed = new List<string>(pending.Count);
        var failed = new List<string>();
        foreach (var target in pending)
        {
            try
            {
                if (target.IsContainer)
                    continue; // Account Reset owns container deletion semantics.

                if (referencedTargetIds.Contains(target.Id))
                {
                    _logger.LogWarning(
                        "Storage cleanup target {TargetId} ({Purpose}) for account {AccountId} is live again; clearing stale debt without deleting the object",
                        target.Id, target.Purpose, AccountId);
                    completed.Add(target.Id);
                    continue;
                }

                if (await TryDeleteObjectAsync(new StorageObjectAddress(
                        target.StoragePartition, target.StorageKey)))
                    completed.Add(target.Id);
                else
                {
                    failed.Add(target.Id);
                    _logger.LogWarning(
                        "Storage cleanup target {TargetId} ({Purpose}) for account {AccountId} remains pending after object deletion failed",
                        target.Id, target.Purpose, AccountId);
                }
            }
            catch (Exception exception)
            {
                failed.Add(target.Id);
                _logger.LogWarning(exception,
                    "Storage cleanup target {TargetId} ({Purpose}) for account {AccountId} could not be processed and remains pending",
                    target.Id, target.Purpose, AccountId);
            }
        }

        if (failed.Count > 0)
        {
            try
            {
                await _store.DeferFailedAsync(purposes, failed, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception,
                    "Unable to defer {CleanupCount} failed cleanup targets for account {AccountId}; they remain durable",
                    failed.Count, AccountId);
            }
        }

        if (completed.Count == 0) return;
        try
        {
            await _store.CompleteAsync(purposes, completed, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception,
                "Unable to acknowledge {CleanupCount} completed storage cleanup targets for account {AccountId}; idempotent cleanup will retry later",
                completed.Count, AccountId);
        }
    }
}
