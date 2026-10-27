using Azure;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using Azure.Storage.Blobs;
using Microsoft.Net.Http.Headers;
using Planarian.Library.Exceptions;
using Planarian.Library.Helpers;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Models;
using Planarian.Shared.Services;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Modules.Files.Services;

public class FileService : ServiceBase<FileRepository>
{
    private static readonly ConcurrentDictionary<string, Task> ContainerInitializationTasks =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> InlinePreviewExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".avif",
        ".bmp",
        ".gif",
        ".jpeg",
        ".jpg",
        ".pdf",
        ".png",
        ".tif",
        ".tiff",
        ".webp"
    };

    private readonly TagRepository _tagRepository;
    private readonly FileOptions _fileOptions;
    private readonly RequestThrottleService _requestThrottleService;
    private readonly SettingsRepository _settingsRepository;
    private readonly CaveRepository _caveRepository;
    private readonly CaveMutationCoordinator _caveMutationCoordinator;
    private readonly TagReferenceLockRepository _tagReferenceLocks;
    private readonly IObjectStorage _objectStorage;

    public FileService(FileRepository repository, RequestUser requestUser, TagRepository tagRepository,
        FileOptions fileOptions, SettingsRepository settingsRepository, CaveRepository caveRepository,
        RequestThrottleService requestThrottleService, CaveMutationCoordinator caveMutationCoordinator,
        TagReferenceLockRepository tagReferenceLocks, IObjectStorage objectStorage) : base(
        repository, requestUser)
    {
        _tagRepository = tagRepository;
        _fileOptions = fileOptions;
        _settingsRepository = settingsRepository;
        _caveRepository = caveRepository;
        _requestThrottleService = requestThrottleService;
        _caveMutationCoordinator = caveMutationCoordinator;
        _tagReferenceLocks = tagReferenceLocks;
        _objectStorage = objectStorage;
    }

    public async Task<FileVm> UploadCaveFile(Stream stream, string caveId, string fileName,
        CancellationToken cancellationToken, string? uuid = null)
    {
        var parsedFileName = FileNamePolicy.Parse(fileName);
        fileName = parsedFileName.CompleteName;
        if (RequestUser.AccountId == null) throw new BadHttpRequestException("Account Id is null");

        var caveEntity = await _caveRepository.GetCave(caveId);
        if (caveEntity == null) throw ApiExceptionDictionary.NotFound("Cave");
        await RequestUser.HasCavePermission(PermissionKey.Manager, caveId, caveEntity.CountyId, caveEntity.StateId);

        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);
        var autoTagType = allFileTypes.FirstOrDefault(e =>
            fileName.Contains(e.Display, StringComparison.InvariantCultureIgnoreCase));
        var other = await _tagRepository.GetFileTypeTagByName(FileTypeTagName.Other, RequestUser.AccountId);
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
        var address = new StorageObjectAddress(RequestUser.AccountContainerName, $"objects/files/{entity.Id}");

        try
        {
            if (stream.CanSeek) stream.Position = 0;
            await _objectStorage.PutAsync(address, stream, MimeTypes.GetMimeType(parsedFileName.Extension),
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
        catch
        {
            // The object address is unique to this File. If relational publication fails,
            // compensate the already-written object without falsifying the database outcome.
            await BestEffortDeleteObjectAsync(address);
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

        var caveEntity = await _caveRepository.GetCave(caveId);
        if (caveEntity == null) throw ApiExceptionDictionary.NotFound("Cave");
        await RequestUser.HasCavePermission(PermissionKey.Manager, caveId, caveEntity.CountyId, caveEntity.StateId);

        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);
        var autoTagType = allFileTypes.FirstOrDefault(e =>
            fileName.Contains(e.Display, StringComparison.InvariantCultureIgnoreCase));
        var other = await _tagRepository.GetFileTypeTagByName(FileTypeTagName.Other, RequestUser.AccountId);
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
        var address = new StorageObjectAddress(RequestUser.AccountContainerName, $"objects/files/{entity.Id}");

        try
        {
            // Chunked import staging remains an Azure transport concern, but the durable
            // object write happens before PostgreSQL publication and is compensated on failure.
            var sourceContainer = await GetBlobContainerClient(RequestUser.AccountContainerName,
                createIfNotExists: false);
            await using var source = await OpenBlobReadStream(sourceContainer, sourceBlobKey, cancellationToken);
            await _objectStorage.PutAsync(address, source, MimeTypes.GetMimeType(parsedFileName.Extension),
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
        catch
        {
            await BestEffortDeleteObjectAsync(address);
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

    public async Task<FileVm> AddTemporaryAccountFile(Stream stream, string fileName, string fileTypeTagName,
        CancellationToken cancellationToken,
        string? uuid = null)
    {
        var parsedFileName = FileNamePolicy.Parse(fileName);
        fileName = parsedFileName.CompleteName;
        if (RequestUser.AccountId == null) throw new BadHttpRequestException("Account Id is null");

        var expiredObjects = await RemoveExpiredFiles(cancellationToken);
        foreach (var expiredObject in expiredObjects)
            await BestEffortDeleteObjectAsync(expiredObject);

        var tempCaveImportTagType =
            await _tagRepository.GetFileTypeTagByName(fileTypeTagName, RequestUser.AccountId);

        var tagTypeId = tempCaveImportTagType?.Id;

        if (tagTypeId == null)
            throw ApiExceptionDictionary.NotFound("File type");

        var entity = new File()
        {
            Name = parsedFileName.Name,
            Extension = parsedFileName.Extension,
            AccountId = RequestUser.AccountId,
            FileTypeTagId = tagTypeId,
            ExpiresOn = DateTime.UtcNow.AddDays(10)
        };
        var fileExtension = parsedFileName.Extension;
        var blobKey = $"temp/import/caves/{entity.Id}{fileExtension}";

        try
        {
            await AddToBlobStorage(stream, blobKey, RequestUser.AccountContainerName, cancellationToken);

            entity.BlobKey = blobKey;
            entity.BlobContainer = RequestUser.AccountContainerName;

            // The transport object is written before the relational row. Do not hold a database
            // transaction open across external storage; compensate the object if persistence fails.
            Repository.Add(entity);
            await Repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await BestEffortDeleteObjectAsync(new StorageObjectAddress(RequestUser.AccountContainerName, blobKey));
            throw;
        }

        var fileInformation = new FileVm
        {
            Id = entity.Id,
            Name = entity.Name,
            Extension = entity.Extension,
            FileTypeTagId = entity.FileTypeTagId,
            Uuid = uuid
        };
        await stream.DisposeAsync();
        return fileInformation;
    }

    public async Task<FileVm> StageAuthoringFile(Stream stream, string fileName,
        CancellationToken cancellationToken, string? uuid = null)
    {
        if (RequestUser.AccountId == null) throw new BadHttpRequestException("Account Id is null");
        var parsedFileName = FileNamePolicy.Parse(fileName);
        fileName = parsedFileName.CompleteName;

        var expiredObjects = await RemoveExpiredFiles(cancellationToken);
        foreach (var expiredObject in expiredObjects)
            await BestEffortDeleteObjectAsync(expiredObject);

        var allFileTypes = await _settingsRepository.GetTags(TagTypeKeyConstant.File);
        var autoTagType = allFileTypes.FirstOrDefault(tag =>
            fileName.Contains(tag.Display, StringComparison.InvariantCultureIgnoreCase));
        var other = await _tagRepository.GetFileTypeTagByName(FileTypeTagName.Other, RequestUser.AccountId);
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
            ExpiresOn = DateTime.UtcNow.AddDays(10)
        };
        var address = new StorageObjectAddress(RequestUser.AccountContainerName, $"objects/files/{entity.Id}");

        try
        {
            if (stream.CanSeek) stream.Position = 0;
            await _objectStorage.PutAsync(address, stream, MimeTypes.GetMimeType(parsedFileName.Extension),
                cancellationToken);
            entity.BlobKey = address.Key;
            entity.BlobContainer = address.Partition;

            // Do not hold a database transaction open while a potentially large
            // object is uploaded. The object has a unique immutable address and
            // is compensated if the short relational transaction cannot commit.
            Repository.Add(entity);
            await Repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await BestEffortDeleteObjectAsync(address);
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

    public async Task DeleteUnpublishedFileAsync(string fileId, CancellationToken cancellationToken)
    {
        var entity = await Repository.GetDeletableUnpublishedFileByIdAsync(fileId, cancellationToken);
        if (entity is null) return;
        var blobKey = entity.BlobKey;
        var blobContainer = entity.BlobContainer;
        Repository.Delete(entity);
        await Repository.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(blobKey) && !string.IsNullOrWhiteSpace(blobContainer))
            await BestEffortDeleteObjectAsync(new StorageObjectAddress(blobContainer, blobKey));
    }

    public Task DeleteObjectBestEffortAsync(StorageObjectAddress address) => BestEffortDeleteObjectAsync(address);

    // Reset Account needs to report post-commit storage failures truthfully.
    // Ordinary File cleanup retains its best-effort, non-signaling contract.
    public async Task<bool> TryDeleteObjectAsync(StorageObjectAddress address)
    {
        try
        {
            await _objectStorage.DeleteIfExistsAsync(address, CancellationToken.None);
            return true; // An already-missing immutable object is safely cleaned up.
        }
        catch
        {
            return false;
        }
    }

    public async Task RequireAuthoringStagedObjectsAvailableAsync(IEnumerable<string> fileIds,
        CancellationToken cancellationToken)
    {
        var requestedIds = fileIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToList();
        if (requestedIds.Count == 0) return;

        var staged = await Repository.GetAuthoringStagedObjectsAsync(requestedIds, cancellationToken);
        if (staged.Count != requestedIds.Count)
            throw ApiExceptionDictionary.NotFound("Staged file");

        var now = DateTime.UtcNow;
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
        var file = await Repository.GetAuthoringStagedFileAccessInfoAsync(fileId, cancellationToken);
        if (file is null || string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.ContainerName))
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

    private async Task<List<StorageObjectAddress>> RemoveExpiredFiles(CancellationToken cancellationToken)
    {
        var expiredFiles = await Repository.GetExpiredFiles();
        var objects = expiredFiles
            .Where(file => !string.IsNullOrWhiteSpace(file.BlobKey) && !string.IsNullOrWhiteSpace(file.BlobContainer))
            .Select(file => new StorageObjectAddress(file.BlobContainer!, file.BlobKey!))
            .Distinct()
            .ToList();

        foreach (var expiredFile in expiredFiles)
            Repository.Delete(expiredFile);

        await Repository.SaveChangesAsync(cancellationToken);
        return objects;
    }

    private async Task BestEffortDeleteObjectAsync(StorageObjectAddress address)
    {
        try
        {
            await _objectStorage.DeleteIfExistsAsync(address, CancellationToken.None);
        }
        catch
        {
            // External cleanup must never replace or falsify the durable relational outcome.
        }
    }

    #region Blob Storage

    protected virtual async Task AddToBlobStorage(Stream stream, string key, string containerName,
        CancellationToken cancellationToken)
    {
        if (RequestUser.AccountId == null) throw ApiExceptionDictionary.BadRequest("Account Id is null");

        var client = await GetBlobContainerClient(containerName);
        var blobClient = client.GetBlobClient(key);

        await blobClient.UploadAsync(stream, true, cancellationToken);
    }

    public async Task<BlobContainerClient> GetBlobContainerClient(string containerName, bool createIfNotExists = true)
    {
        var containerClient = new BlobContainerClient(_fileOptions.ConnectionString, containerName.ToLowerInvariant());
        if (createIfNotExists)
        {
            await EnsureContainerExists(containerClient);
        }

        return containerClient;
    }

    private static async Task EnsureContainerExists(BlobContainerClient containerClient)
    {
        var containerName = containerClient.Name;
        // Azure may still be deleting the previous container immediately after
        // Reset Account. This legacy BlobService path needs the same recreation
        // retry as the provider-neutral upload path.
        for (var attempt = 0; ; attempt++)
        {
            var initializationTask = ContainerInitializationTasks.GetOrAdd(
                containerName, _ => containerClient.CreateIfNotExistsAsync());
            try
            {
                await initializationTask;
                return;
            }
            catch (RequestFailedException ex) when (ex.Status == 409 &&
                ex.ErrorCode == "ContainerBeingDeleted" && attempt < 9)
            {
                ContainerInitializationTasks.TryRemove(containerName, out _);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(2 * (attempt + 1), 8)));
            }
            catch
            {
                ContainerInitializationTasks.TryRemove(containerName, out _);
                throw;
            }
        }
    }

    public async Task<Stream> OpenBlobReadStream(BlobContainerClient containerClient, string blobKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobKey))
            throw ApiExceptionDictionary.NotFound("File");

        var blobClient = containerClient.GetBlobClient(blobKey);
        return await blobClient.OpenReadAsync(cancellationToken: cancellationToken);
    }

    public async Task<Stream> OpenBlobWriteStream(BlobContainerClient containerClient, string blobKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobKey))
            throw ApiExceptionDictionary.NotFound("File");

        var blobClient = containerClient.GetBlobClient(blobKey);
        return await blobClient.OpenWriteAsync(overwrite: true, cancellationToken: cancellationToken);
    }

    #endregion

    private async Task LockPublishedFileTypeAsync(string tagTypeId, CancellationToken cancellationToken)
    {
        var locked = await _tagReferenceLocks.LockForReferenceAsync([tagTypeId], cancellationToken);
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

    public async Task<AuthenticatedFileResponse> CreateBlobResponse(
        string blobKey,
        string containerName,
        string fileName,
        bool isDownload,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(blobKey) || string.IsNullOrWhiteSpace(containerName))
            throw ApiExceptionDictionary.NotFound("File");

        var client = await GetBlobContainerClient(containerName, createIfNotExists: false);
        var blobClient = client.GetBlobClient(blobKey);
        return await BlobService.CreateBlobResponse(
            blobClient,
            fileName,
            isDownload,
            cancellationToken,
            MimeTypes.GetMimeType(Path.GetExtension(fileName)));
    }

    private async Task EnsureFileViewAccess(string fileId, string? caveId, string? countyId, string? stateId)
    {
        if (!string.IsNullOrWhiteSpace(caveId) || !string.IsNullOrWhiteSpace(countyId))
        {
            await RequestUser.HasCavePermission(PermissionKey.View, caveId, countyId, stateId);
            return;
        }

        var fileContext = await Repository.GetFileAuthorizationContext(fileId);
        if (fileContext == null)
            throw ApiExceptionDictionary.NotFound("File");

        await RequestUser.HasCavePermission(PermissionKey.Manager);
    }

    public async Task DeleteFile(string? blobKey, string? blobContainer)
    {
        if (string.IsNullOrWhiteSpace(blobKey) || string.IsNullOrWhiteSpace(blobContainer))
            throw ApiExceptionDictionary.NotFound("File");

        var client = await GetBlobContainerClient(blobContainer);
        var blobClient = client.GetBlobClient(blobKey);
        await blobClient.DeleteIfExistsAsync();
    }

    public virtual async Task DeleteContainer(string containerName)
    {
        var normalizedContainerName = containerName.ToLowerInvariant();

        // Create a container client using your configured connection string
        var containerClient = new BlobContainerClient(
            _fileOptions.ConnectionString,
            normalizedContainerName);

        try
        {
            // Attempt to delete the entire container
            await containerClient.DeleteIfExistsAsync();
        }
        finally
        {
            // A successful initialization is cached to avoid repeated container-existence checks.
            // Invalidate it even when deletion fails so a later upload can re-establish the container safely.
            ContainerInitializationTasks.TryRemove(containerClient.Name, out _);
            _objectStorage.InvalidateContainer(containerClient.Name);
        }
    }

    public async Task<Stream> GetFileStream(string fileId)
    {
        var file = await Repository.GetFileVm(fileId);
        var blobProperties = await Repository.GetFileBlobProperties(fileId);
        if (file == null || blobProperties == null || string.IsNullOrWhiteSpace(blobProperties.ContainerName) ||
            string.IsNullOrWhiteSpace(blobProperties.BlobKey))
            throw ApiExceptionDictionary.NotFound("File");

        var stored = await _objectStorage.OpenReadAsync(
            new StorageObjectAddress(blobProperties.ContainerName, blobProperties.BlobKey), CancellationToken.None);
        if (stored is null) throw ApiExceptionDictionary.NotFound("File");
        try
        {
            return await stored.OpenReadStreamAsync(CancellationToken.None);
        }
        catch (FileNotFoundException)
        {
            throw ApiExceptionDictionary.NotFound("File");
        }
    }
}

public class FileTypeTagName
{
    public const string Other = "Other";
}

public class FileVm
{
    [MaxLength(PropertyLength.FileName)] public string Name { get; set; } = null!;
    [MaxLength(PropertyLength.FileName)] public string Extension { get; set; } = string.Empty;
    [MaxLength(PropertyLength.Id)] public string Id { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string FileTypeTagId { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string FileTypeKey { get; set; } = null!;
    public string? Uuid { get; set; }
}
