using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;

namespace Planarian.Modules.Caves.Revisions;

/// <summary>Application coordination boundary; database mutation and transaction ownership live in the persistence component.</summary>
public sealed class CaveMutationCoordinator
{
    private readonly CaveMutationPersistence _persistence;

    public CaveMutationCoordinator(CaveMutationPersistence persistence) => _persistence = persistence;

    public Task<T> ExecuteTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default) =>
        _persistence.ExecuteTransactionAsync(work, cancellationToken);

    public Task ExecuteOwnedTransactionAsync(Func<CancellationToken, Task> work,
        CancellationToken cancellationToken = default) =>
        _persistence.ExecuteOwnedTransactionAsync(work, cancellationToken);

    public Task<T> ExecuteOwnedTransactionAsync<T>(Func<CancellationToken, Task<T>> work,
        CancellationToken cancellationToken = default) =>
        _persistence.ExecuteOwnedTransactionAsync(work, cancellationToken);

    public Task<CaveMutationResult> PublishExistingAsync(string caveId, string? expectedRevisionId,
        CaveRevisionSource source, CaveRevisionOperation operation, Action<Cave> write,
        string? changeRequestId = null, string? importBatchId = null, CancellationToken cancellationToken = default) =>
        _persistence.PublishExistingAsync(caveId, expectedRevisionId, source, operation, write, changeRequestId,
            importBatchId, cancellationToken);

    public Task<CaveMutationPreparation> PrepareExistingAsync(string caveId, string? expectedRevisionId = null,
        CancellationToken cancellationToken = default) =>
        _persistence.PrepareExistingAsync(caveId, expectedRevisionId, cancellationToken);

    public Task<CaveMutationResult> PublishPreparedAsync(CaveMutationPreparation preparation,
        CaveRevisionSource source, CaveRevisionOperation operation, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default) =>
        _persistence.PublishPreparedAsync(preparation, source, operation, changeRequestId, importBatchId,
            cancellationToken);

    public Task<CaveMutationResult> PublishPersistedNewAsync(string caveId, CaveRevisionSource source,
        CaveRevisionOperation operation = CaveRevisionOperation.Create, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default) =>
        _persistence.PublishPersistedNewAsync(caveId, source, operation, changeRequestId, importBatchId,
            cancellationToken);

    public Task<CaveMutationResult> PublishPreparedDeleteAsync(CaveMutationPreparation preparation,
        CaveRevisionSource source, string? changeRequestId = null, string? importBatchId = null,
        CancellationToken cancellationToken = default) =>
        _persistence.PublishPreparedDeleteAsync(preparation, source, changeRequestId, importBatchId,
            cancellationToken);
}
