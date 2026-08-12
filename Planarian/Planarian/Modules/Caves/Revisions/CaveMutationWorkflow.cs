using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;

namespace Planarian.Modules.Caves.Revisions;

/// <summary>Application coordination boundary; persistence and transaction ownership live in the repository.</summary>
public sealed class CaveMutationCoordinator
{
    private readonly CaveMutationRepository _repository;

    public CaveMutationCoordinator(CaveMutationRepository repository) => _repository = repository;

    public Task<string> EnsureBaselineAsync(string caveId, CancellationToken cancellationToken = default) =>
        _repository.EnsureBaselineAsync(caveId, cancellationToken);

    public Task<CaveMutationResult> PublishExistingAsync(string caveId, string? expectedRevisionId,
        CaveRevisionSource source, CaveRevisionOperation operation, Action<Cave> write,
        string? changeRequestId = null, string? importBatchId = null, CancellationToken cancellationToken = default) =>
        _repository.PublishExistingAsync(caveId, expectedRevisionId, source, operation, write, changeRequestId,
            importBatchId, cancellationToken);

    public Task<CaveMutationResult> PublishNewAsync(Cave cave, CaveRevisionSource source,
        CaveRevisionOperation operation = CaveRevisionOperation.Create, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default) =>
        _repository.PublishNewAsync(cave, source, operation, changeRequestId, importBatchId, cancellationToken);

    public Task<CaveMutationPreparation> PrepareExistingAsync(string caveId, string? expectedRevisionId = null,
        CancellationToken cancellationToken = default) =>
        _repository.PrepareExistingAsync(caveId, expectedRevisionId, cancellationToken);

    public Task<CaveMutationResult> PublishPreparedAsync(CaveMutationPreparation preparation,
        CaveRevisionSource source, CaveRevisionOperation operation, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default) =>
        _repository.PublishPreparedAsync(preparation, source, operation, changeRequestId, importBatchId,
            cancellationToken);

    public Task<CaveMutationResult> PublishPersistedNewAsync(string caveId, CaveRevisionSource source,
        CaveRevisionOperation operation = CaveRevisionOperation.Create, string? changeRequestId = null,
        string? importBatchId = null, CancellationToken cancellationToken = default) =>
        _repository.PublishPersistedNewAsync(caveId, source, operation, changeRequestId, importBatchId,
            cancellationToken);

    public Task<CaveMutationResult> PublishPreparedDeleteAsync(CaveMutationPreparation preparation,
        CaveRevisionSource source, string? changeRequestId = null, string? importBatchId = null,
        CancellationToken cancellationToken = default) =>
        _repository.PublishPreparedDeleteAsync(preparation, source, changeRequestId, importBatchId,
            cancellationToken);
}
