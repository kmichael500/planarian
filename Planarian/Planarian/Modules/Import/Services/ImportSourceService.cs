using Planarian.Library.Exceptions;
using Planarian.Library.Helpers;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Import.Data;
using Planarian.Modules.Import.Models;
using Planarian.Shared.Base;
using Planarian.Shared.Services;

namespace Planarian.Modules.Import.Services;

/// <summary>
/// Cave/Entrance CSV source upload and read workflow. Import sources are provenance,
/// not Cave File assets, and deliberately have no FileType or Cave lifecycle.
/// </summary>
public sealed class ImportSourceService : AccountServiceBase
{
    private const int OpportunisticCleanupBatchSize = 16;
    private static readonly TimeSpan UploadLifetime = TimeSpan.FromDays(10);

    private readonly ImportSourceStore _store;
    private readonly StorageCleanupService _cleanup;
    private readonly IObjectStorage _objectStorage;
    private readonly TimeProvider _timeProvider;

    public ImportSourceService(
        ImportSourceStore store,
        StorageCleanupService cleanup,
        RequestUser requestUser,
        IObjectStorage objectStorage,
        TimeProvider timeProvider) : base(requestUser)
    {
        _store = store;
        _cleanup = cleanup;
        _objectStorage = objectStorage;
        _timeProvider = timeProvider;
    }

    public async Task<ImportSourceUploadVm> UploadAsync(
        Stream stream,
        string fileName,
        string? uuid,
        CancellationToken cancellationToken)
    {
        var accountId = AccountId;
        var parsed = FileNamePolicy.Parse(fileName);

        await _cleanup.CleanupImportSourceTargetsAsync(cancellationToken: CancellationToken.None);
        var expiredTargetIds = await _store.RemoveExpiredAsync(
            OpportunisticCleanupBatchSize, cancellationToken);
        await _cleanup.CleanupImportSourceTargetsAsync(expiredTargetIds, CancellationToken.None);

        var source = new CaveImportSource
        {
            // Normal File IDs are alphanumeric. Reserve '_' for import-source IDs so a
            // source created after upgrade can always be reconstructed into v32 Files on downgrade.
            Id = "_" + IdGenerator.Generate(9),
            AccountId = accountId,
            FileName = parsed.CompleteName,
            ExpiresOn = _timeProvider.GetUtcNow().UtcDateTime.Add(UploadLifetime)
        };
        var address = new StorageObjectAddress(
            RequestUser.BuildAccountContainerName(AccountId),
            ImportSourceStoragePath.BuildCaveEntranceCsvKey(source.Id));

        var createAttempted = false;
        try
        {
            if (stream.CanSeek) stream.Position = 0;
            createAttempted = true;
            await _objectStorage.CreateAsync(
                address,
                stream,
                MimeTypes.GetMimeType(parsed.Extension),
                cancellationToken);

            source.BlobContainer = address.Partition;
            source.BlobKey = address.Key;
            _store.Add(source);
            await _store.SaveChangesAsync(cancellationToken);
        }
        catch (StorageObjectAlreadyExistsException)
        {
            throw;
        }
        catch
        {
            if (createAttempted)
                await _cleanup.ReconcileUncertainCreateAsync(
                    address, StorageCleanupPurpose.ImportSourceRemoval);
            throw;
        }

        return new ImportSourceUploadVm
        {
            Id = source.Id,
            FileName = source.FileName,
            Uuid = uuid
        };
    }

    public async Task<Stream> OpenReadAsync(string sourceId, CancellationToken cancellationToken)
    {
        var source = await _store.GetAsync(sourceId, cancellationToken);
        if (source is null)
            throw ApiExceptionDictionary.NotFound("Import source");

        if (source.ExpiresOn is { } expiration &&
            expiration <= _timeProvider.GetUtcNow().UtcDateTime)
            throw ApiExceptionDictionary.BadRequest(
                "The import source has expired. Upload it again before planning or applying the import.");

        RequireActiveImportStorage(source);

        var stored = await _objectStorage.OpenReadAsync(
            new StorageObjectAddress(source.BlobContainer, source.BlobKey), cancellationToken);
        if (stored is null)
            throw ApiExceptionDictionary.NotFound("Import source");

        try
        {
            return await stored.OpenReadStreamAsync(cancellationToken);
        }
        catch (FileNotFoundException)
        {
            throw ApiExceptionDictionary.NotFound("Import source");
        }
    }

    public async Task<(Stream Stream, string FileName)> OpenDownloadAsync(
        string sourceId,
        CancellationToken cancellationToken)
    {
        var source = await _store.GetAsync(sourceId, cancellationToken);
        if (source is null || source.ExpiresOn is not null)
            throw ApiExceptionDictionary.NotFound("Import source");

        RequireActiveImportStorage(source);

        var stored = await _objectStorage.OpenReadAsync(
            new StorageObjectAddress(source.BlobContainer, source.BlobKey), cancellationToken);
        if (stored is null)
            throw ApiExceptionDictionary.NotFound("Import source");

        try
        {
            return (await stored.OpenReadStreamAsync(cancellationToken), source.FileName);
        }
        catch (FileNotFoundException)
        {
            // The object may disappear between metadata retrieval and stream open.
            throw ApiExceptionDictionary.NotFound("Import source");
        }
    }

    private void RequireActiveImportStorage(ImportSourceAccessInfo source)
    {
        if (!string.Equals(source.BlobContainer, RequestUser.BuildAccountContainerName(AccountId),
                StringComparison.OrdinalIgnoreCase) ||
            !ImportSourceStoragePath.IsCaveEntranceCsvKey(source.BlobKey))
            throw ApiExceptionDictionary.BadRequest(
                "The selected source is not stored in the active account import-source namespace.");
    }
}
