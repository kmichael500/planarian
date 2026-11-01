using System.ComponentModel.DataAnnotations;
using Microsoft.Net.Http.Headers;
using Planarian.Library.Exceptions;
using Planarian.Library.Helpers;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Models;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Models;
using Planarian.Shared.Services;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;

using Planarian.Modules.Tags.Persistence;
using Planarian.Modules.Files.Persistence;
namespace Planarian.Modules.Files.Services;

public class FileService : ServiceBase<FileRepository>
{
    private const int OpportunisticCleanupBatchSize = 16;

    private readonly TagRepository _tagRepository;
    private readonly RequestThrottleService _requestThrottleService;
    private readonly SettingsRepository _settingsRepository;
    private readonly CaveRepository _caveRepository;
    private readonly CaveMutationCoordinator _caveMutationCoordinator;
    private readonly TagReferenceLocker _tagReferenceLocker;
    private readonly IObjectStorage _objectStorage;
    private readonly AuthoringFileStore _authoringFileStore;
    private readonly StorageCleanupService _storageCleanup;
    private readonly AzureBlobTransportService _blobTransport;
    private readonly TimeProvider _timeProvider;

    public FileService(FileRepository repository, RequestUser requestUser, TagRepository tagRepository,
        SettingsRepository settingsRepository, CaveRepository caveRepository,
        RequestThrottleService requestThrottleService, CaveMutationCoordinator caveMutationCoordinator,
        TagReferenceLocker tagReferenceLocker, IObjectStorage objectStorage,
        AuthoringFileStore authoringFileStore, StorageCleanupService storageCleanup,
        AzureBlobTransportService blobTransport,
        TimeProvider timeProvider) : base(
        repository, requestUser)
    {
        _tagRepository = tagRepository;
        _settingsRepository = settingsRepository;
        _caveRepository = caveRepository;
        _requestThrottleService = requestThrottleService;
        _caveMutationCoordinator = caveMutationCoordinator;
        _tagReferenceLocker = tagReferenceLocker;
        _objectStorage = objectStorage;
        _authoringFileStore = authoringFileStore;
        _storageCleanup = storageCleanup;
        _blobTransport = blobTransport;
        _timeProvider = timeProvider;
    }

    public async Task<FileVm> UploadCaveFile(Stream stream, string caveId, string fileName,
        CancellationToken cancellationToken, string? uuid = null)
    {
        var parsedFileName = FileNamePolicy.Parse(fileName);
        fileName = parsedFileName.CompleteName;
        if (RequestUser.AccountId == null) throw new BadHttpRequestException("Account Id is null");

        // Direct file publication is an authoring entry point too; retry bounded
        // ordinary cleanup debt without waiting for a later staging operation.
        await _storageCleanup.CleanupOrdinaryTargetsAsync(cancellationToken: CancellationToken.None);

        var caveEntity = await _caveRepository.GetCave(caveId);
        if (caveEntity == null) throw ApiExceptionDictionary.NotFound("Cave");
        await RequestUser.HasCavePermission(PermissionKey.Manager, caveId, caveEntity.CountyId, caveEntity.StateId);

        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);
        var autoTagType = allFileTypes.FirstOrDefault(e =>
            fileName.Contains(e.Display, StringComparison.InvariantCultureIgnoreCase));
        var other = await _tagRepository.GetFileTypeTagByName(FileTypeTagName.Other);
        var tagTypeId = !string.IsNullOrWhiteSpace(autoTagType?.Value) ? autoTagType.Value : other?.Id;
        if (tagTypeId == null) throw ApiExceptionDictionary.NotFound("File type");

        var entity = new File
        {
            Id = IdGenerator.Generate(),
            CaveId = caveId,
            Name = parsedFileName.Name,
            Extension = parsedFileName.Extension,
            AccountId = RequestUser.AccountId,
            FileTypeTagId = tagTypeId
        };
        var address = new StorageObjectAddress(RequestUser.AccountContainerName,
            AuthoringFileStoragePath.BuildKey(entity.Id));
        var objectCreationAttempted = false;

        try
        {
            if (stream.CanSeek) stream.Position = 0;
            objectCreationAttempted = true;
            await _objectStorage.CreateAsync(address, stream, MimeTypes.GetMimeType(parsedFileName.Extension),
                cancellationToken);
            entity.BlobKey = address.Key;
            entity.BlobContainer = address.Partition;

            await _caveMutationCoordinator.ExecuteOwnedTransactionAsync(async token =>
            {
                await LockPublishedFileTypeAsync(tagTypeId, token);
                var revisionPreparation = await _caveMutationCoordinator.PrepareExistingAsync(
                    caveId, cancellationToken: token);
                Repository.Add(entity);
                await Repository.SaveChangesAsync(token);
                await _caveMutationCoordinator.PublishPreparedAsync(
                    revisionPreparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                    cancellationToken: token);
            }, cancellationToken);
        }
        catch (StorageObjectAlreadyExistsException)
        {
            // A failed create-only precondition never grants ownership of existing bytes.
            throw;
        }
        catch
        {
            // Object creation or the following database commit may have succeeded despite
            // a lost acknowledgement. Never directly delete a potentially live File object.
            if (objectCreationAttempted)
                await _storageCleanup.ReconcileUncertainCreateAsync(
                    address, StorageCleanupPurpose.UnpublishedFileRemoval);
            throw;
        }

        return new FileVm
        {
            Id = entity.Id,
            Name = entity.Name,
            Extension = entity.Extension,
            FileTypeTagId = entity.FileTypeTagId,
            Uuid = uuid
        };
    }

    public async Task<FileVm> PublishStagedCaveFile(string sourceBlobKey, string caveId, string fileName,
        CancellationToken cancellationToken, string? uuid = null)
    {
        var parsedFileName = FileNamePolicy.Parse(fileName);
        fileName = parsedFileName.CompleteName;
        if (RequestUser.AccountId == null) throw new BadHttpRequestException("Account Id is null");

        // Direct file publication is an authoring entry point too; retry bounded
        // ordinary cleanup debt without waiting for a later staging operation.
        await _storageCleanup.CleanupOrdinaryTargetsAsync(cancellationToken: CancellationToken.None);

        var caveEntity = await _caveRepository.GetCave(caveId);
        if (caveEntity == null) throw ApiExceptionDictionary.NotFound("Cave");
        await RequestUser.HasCavePermission(PermissionKey.Manager, caveId, caveEntity.CountyId, caveEntity.StateId);

        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);
        var autoTagType = allFileTypes.FirstOrDefault(e =>
            fileName.Contains(e.Display, StringComparison.InvariantCultureIgnoreCase));
        var other = await _tagRepository.GetFileTypeTagByName(FileTypeTagName.Other);
        var tagTypeId = !string.IsNullOrWhiteSpace(autoTagType?.Value) ? autoTagType.Value : other?.Id;
        if (tagTypeId == null) throw ApiExceptionDictionary.NotFound("File type");

        var entity = new File
        {
            Id = IdGenerator.Generate(),
            CaveId = caveId,
            Name = parsedFileName.Name,
            Extension = parsedFileName.Extension,
            AccountId = RequestUser.AccountId,
            FileTypeTagId = tagTypeId
        };
        var address = new StorageObjectAddress(RequestUser.AccountContainerName,
            AuthoringFileStoragePath.BuildKey(entity.Id));
        var objectCreationAttempted = false;

        try
        {
            // Chunked import staging remains an Azure transport concern, but the durable
            // object write happens before PostgreSQL publication. Any uncertain outcome is
            // reconciled through live references and durable cleanup responsibility below.
            var sourceContainer = await _blobTransport.GetBlobContainerClient(RequestUser.AccountContainerName,
                createIfNotExists: false);
            await using var source = await _blobTransport.OpenBlobReadStream(sourceContainer, sourceBlobKey, cancellationToken);
            objectCreationAttempted = true;
            await _objectStorage.CreateAsync(address, source, MimeTypes.GetMimeType(parsedFileName.Extension),
                cancellationToken);
            entity.BlobKey = address.Key;
            entity.BlobContainer = address.Partition;

            await _caveMutationCoordinator.ExecuteOwnedTransactionAsync(async token =>
            {
                await LockPublishedFileTypeAsync(tagTypeId, token);
                var revisionPreparation = await _caveMutationCoordinator.PrepareExistingAsync(
                    caveId, cancellationToken: token);
                Repository.Add(entity);
                await Repository.SaveChangesAsync(token);
                await _caveMutationCoordinator.PublishPreparedAsync(
                    revisionPreparation, CaveRevisionSource.ManagerEdit, CaveRevisionOperation.Update,
                    cancellationToken: token);
            }, cancellationToken);
        }
        catch (StorageObjectAlreadyExistsException)
        {
            throw;
        }
        catch
        {
            if (objectCreationAttempted)
                await _storageCleanup.ReconcileUncertainCreateAsync(
                    address, StorageCleanupPurpose.UnpublishedFileRemoval);
            throw;
        }

        return new FileVm
        {
            Id = entity.Id,
            Name = entity.Name,
            Extension = entity.Extension,
            FileTypeTagId = entity.FileTypeTagId,
            Uuid = uuid
        };
    }

    public async Task<FileVm> StageAuthoringFile(Stream stream, string fileName,
        CancellationToken cancellationToken, string? uuid = null)
    {
        if (RequestUser.AccountId == null) throw new BadHttpRequestException("Account Id is null");
        await _storageCleanup.CleanupOrdinaryTargetsAsync(cancellationToken: CancellationToken.None);
        var parsedFileName = FileNamePolicy.Parse(fileName);
        fileName = parsedFileName.CompleteName;

        var expiredCleanupTargetIds = await RemoveExpiredFiles(cancellationToken);
        await _storageCleanup.CleanupUnpublishedFileTargetsAsync(expiredCleanupTargetIds, CancellationToken.None);

        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);
        var autoTagType = allFileTypes.FirstOrDefault(tag =>
            fileName.Contains(tag.Display, StringComparison.InvariantCultureIgnoreCase));
        var other = await _tagRepository.GetFileTypeTagByName(FileTypeTagName.Other);
        var tagTypeId = !string.IsNullOrWhiteSpace(autoTagType?.Value) ? autoTagType.Value : other?.Id;
        if (tagTypeId == null) throw ApiExceptionDictionary.NotFound("File type");
        var fileTypeKey = !string.IsNullOrWhiteSpace(autoTagType?.Display) ? autoTagType.Display : other?.Name;
        if (string.IsNullOrWhiteSpace(fileTypeKey)) throw ApiExceptionDictionary.NotFound("File type");

        var entity = new File
        {
            Id = IdGenerator.Generate(),
            Name = parsedFileName.Name,
            Extension = parsedFileName.Extension,
            AccountId = RequestUser.AccountId,
            FileTypeTagId = tagTypeId,
            ExpiresOn = _timeProvider.GetUtcNow().UtcDateTime.AddDays(10)
        };
        var address = new StorageObjectAddress(RequestUser.AccountContainerName,
            AuthoringFileStoragePath.BuildKey(entity.Id));
        var objectCreationAttempted = false;

        try
        {
            if (stream.CanSeek) stream.Position = 0;
            objectCreationAttempted = true;
            await _objectStorage.CreateAsync(address, stream, MimeTypes.GetMimeType(parsedFileName.Extension),
                cancellationToken);
            entity.BlobKey = address.Key;
            entity.BlobContainer = address.Partition;

            // Do not hold a database transaction open while a potentially large
            // object is uploaded. The object has a unique immutable address; an uncertain
            // relational outcome is reconciled without assuming ownership of pre-existing bytes.
            Repository.Add(entity);
            await Repository.SaveChangesAsync(cancellationToken);
        }
        catch (StorageObjectAlreadyExistsException)
        {
            throw;
        }
        catch
        {
            if (objectCreationAttempted)
                await _storageCleanup.ReconcileUncertainCreateAsync(
                    address, StorageCleanupPurpose.UnpublishedFileRemoval);
            throw;
        }

        return new FileVm
        {
            Id = entity.Id,
            Name = entity.Name,
            Extension = entity.Extension,
            FileTypeTagId = entity.FileTypeTagId,
            FileTypeKey = fileTypeKey,
            Uuid = uuid
        };
    }

    public async Task DeleteAuthoringStagedFileAsync(string fileId, CancellationToken cancellationToken)
    {
        var cleanupTargetIds = await _authoringFileStore.RemoveUnclaimedAsync(fileId, cancellationToken);
        await _storageCleanup.CleanupUnpublishedFileTargetsAsync(cleanupTargetIds, CancellationToken.None);
    }

    public async Task RequireAuthoringStagedObjectsAvailableAsync(IEnumerable<string> fileIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = fileIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToList();
        if (requestedIds.Count == 0) return;

        var staged = await _authoringFileStore.GetUnclaimedObjectsAsync(requestedIds, cancellationToken);
        if (staged.Count != requestedIds.Count)
            throw ApiExceptionDictionary.NotFound("Staged file");

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (staged.Any(file => file.ExpiresOn <= now))
            throw ApiExceptionDictionary.BadRequest(
                "A staged file has expired. Upload it again before publishing this Cave change.");
        await RequireStoredObjectsAvailableAsync(staged.Select(file =>
            new StorageObjectAddress(file.StoragePartition, file.StorageKey)), cancellationToken);
    }

    public async Task RequireStoredObjectsAvailableAsync(IEnumerable<StorageObjectAddress> addresses,
        CancellationToken cancellationToken)
    {
        foreach (var address in addresses.Distinct())
        {
            if (await _objectStorage.OpenReadAsync(address, cancellationToken) is null)
                throw ApiExceptionDictionary.BadRequest(
                    "A staged file is no longer available. Upload it again before publishing this Cave change.");
        }
    }

    public async Task<(Stream Stream, string FileName)> OpenAuthoringStagedFileAsync(string fileId,
        CancellationToken cancellationToken)
    {
        var file = await _authoringFileStore.GetUnclaimedAccessAsync(fileId, cancellationToken);
        if (file is null)
            throw ApiExceptionDictionary.NotFound("Staged file");
        var stored = await _objectStorage.OpenReadAsync(
            new StorageObjectAddress(file.BlobContainer, file.BlobKey), cancellationToken);
        if (stored is null) throw ApiExceptionDictionary.NotFound("Staged file");
        try
        {
            return (await stored.OpenReadStreamAsync(cancellationToken),
                FileNamePolicy.Compose(file.Name, file.Extension));
        }
        catch (FileNotFoundException)
        {
            throw ApiExceptionDictionary.NotFound("Staged file");
        }
    }

    public async Task<(Stream Stream, string FileName)> OpenUnpublishedFileAsync(string fileId,
        CancellationToken cancellationToken)
    {
        var file = await Repository.GetFileAccessInfo(fileId);
        if (file is null || !string.IsNullOrWhiteSpace(file.CaveId) ||
            string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.ContainerName))
            throw ApiExceptionDictionary.NotFound("Staged file");
        var stored = await _objectStorage.OpenReadAsync(
            new StorageObjectAddress(file.ContainerName, file.BlobKey), cancellationToken);
        if (stored is null) throw ApiExceptionDictionary.NotFound("Staged file");
        try
        {
            return (await stored.OpenReadStreamAsync(cancellationToken),
                FileNamePolicy.Compose(file.Name, file.Extension));
        }
        catch (FileNotFoundException)
        {
            throw ApiExceptionDictionary.NotFound("Staged file");
        }
    }

    private async Task<List<string>> RemoveExpiredFiles(CancellationToken cancellationToken) =>
        (await _authoringFileStore.RemoveExpiredAsync(
            OpportunisticCleanupBatchSize, cancellationToken)).ToList();

    private async Task LockPublishedFileTypeAsync(string tagTypeId, CancellationToken cancellationToken)
    {
        var locked = await _tagReferenceLocker.LockForReferenceAsync([tagTypeId], cancellationToken);
        if (locked.Count != 1 || locked[0].Key != TagTypeKeyConstant.File)
            throw ApiExceptionDictionary.BadRequest(
                "The selected file type changed or disappeared. Retry with current data.");
    }

    public async Task<AuthenticatedFileResponse> CreateFileResponse(string fileId, bool isDownload,
        CancellationToken cancellationToken)
    {
        await _requestThrottleService.CountFileAccessAttempt(fileId);

        var file = await Repository.GetFileAccessInfo(fileId);
        if (file == null || string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.ContainerName))
            throw ApiExceptionDictionary.NotFound("File");

        await EnsureFileViewAccess(file.Id, file.CaveId, file.CountyId, file.StateId);
        var stored = await _objectStorage.OpenReadAsync(
            new StorageObjectAddress(file.ContainerName, file.BlobKey), cancellationToken);
        if (stored is null) throw ApiExceptionDictionary.NotFound("File");
        var fallbackContentType = MimeTypes.GetMimeType(file.Extension);
        var (contentType, forceDownload) = FileResponsePolicy.ResolveExtension(file.Extension,
            string.IsNullOrWhiteSpace(stored.ContentType) ? fallbackContentType : stored.ContentType, isDownload);
        return new AuthenticatedFileResponse
        {
            OpenReadStreamAsync = async readCancellationToken =>
            {
                try
                {
                    return await stored.OpenReadStreamAsync(readCancellationToken);
                }
                catch (FileNotFoundException)
                {
                    throw ApiExceptionDictionary.NotFound("File");
                }
            },
            ContentType = contentType,
            FileName = FileNamePolicy.Compose(file.Name, file.Extension),
            Download = forceDownload,
            EntityTag = string.IsNullOrWhiteSpace(stored.EntityTag) ? null : EntityTagHeaderValue.Parse(stored.EntityTag),
            LastModified = stored.LastModified
        };
    }

    private async Task EnsureFileViewAccess(string fileId, string? caveId, string? countyId, string? stateId)
    {
        if (string.IsNullOrWhiteSpace(caveId))
            throw ApiExceptionDictionary.NotFound("File");

        await RequestUser.HasCavePermission(PermissionKey.View, caveId, countyId, stateId);
    }


}
