
using System.ComponentModel.DataAnnotations;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Base;
using Planarian.Shared.Exceptions;
using SendGrid.Helpers.Errors.Model;
using File = Planarian.Model.Database.Entities.RidgeWalker.File;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Modules.Files.Services;

public class FileService : ServiceBase<FileRepository>
{
    private readonly TagRepository _tagRepository;
    private readonly FileOptions _fileOptions;
    private readonly SettingsRepository _settingsRepository;

    public FileService(FileRepository repository, RequestUser requestUser, TagRepository tagRepository,
        FileOptions fileOptions, SettingsRepository settingsRepository) : base(repository, requestUser)
    {
        _tagRepository = tagRepository;
        _fileOptions = fileOptions;
        _settingsRepository = settingsRepository;
    }

    public async Task<FileVm> UploadCaveFile(Stream stream, string caveId, string fileName,
        CancellationToken cancellationToken, string? uuid = null)
    {
        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        if (RequestUser.AccountId == null)
        {
            throw new BadRequestException("Account Id is null");
        }

        var allFileTypes = await _settingsRepository.GetFileTags();
        
        // check if tag type name exists in the file name
        var autoTagType =
            allFileTypes.FirstOrDefault(e => fileName.Contains(e.Display, StringComparison.InvariantCultureIgnoreCase));
        
        var other = await _tagRepository.GetFileTypeTagByName(FileTypeTagName.Other, RequestUser.AccountId);
        
        var tagTypeId = !string.IsNullOrWhiteSpace(autoTagType?.Value) ?  autoTagType.Value : other?.Id;

        if (tagTypeId == null)
            throw ApiExceptionDictionary.NotFound("File type");
                

        //fileName without extension
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var entity = new File()
        {
            CaveId = caveId,
            FileName = fileName,
            DisplayName = fileNameWithoutExtension,
            AccountId = RequestUser.AccountId,
            FileTypeTagId = tagTypeId
        };

        Repository.Add(entity);
        await Repository.SaveChangesAsync();


        var fileExtension = Path.GetExtension(fileName);
        var blobKey = $"caves/{caveId}/files/{entity.Id}{fileExtension}";

        await AddToBlobStorage(stream, blobKey, RequestUser.AccountContainerName, cancellationToken);

        entity.BlobKey = blobKey;
        entity.BlobContainer = RequestUser.AccountContainerName;
        await Repository.SaveChangesAsync();
        await transaction.CommitAsync();

        var fileInformation = new FileVm
        {
            Id = entity.Id,
            FileName = entity.FileName,
            DisplayName = entity.DisplayName,
            FileTypeTagId = entity.FileTypeTagId,
            Uuid = uuid,
        };
        return fileInformation;
    }

    public async Task<FileVm> AddTemporaryAccountFile(Stream stream, string fileName, string fileTypeTagName,
        CancellationToken cancellationToken,
        string? uuid = null)
    {
        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        if (RequestUser.AccountId == null)
        {
            throw new BadRequestException("Account Id is null");
        }

        await RemoveExpiredFiles();

        var tempCaveImportTagType =
            await _tagRepository.GetFileTypeTagByName(fileTypeTagName, RequestUser.AccountId);

        var tagTypeId = tempCaveImportTagType?.Id;

        if (tagTypeId == null)
            throw ApiExceptionDictionary.NotFound("File type");


        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var entity = new File()
        {
            FileName = fileName,
            DisplayName = fileNameWithoutExtension,
            AccountId = RequestUser.AccountId,
            FileTypeTagId = tagTypeId,
            ExpiresOn = DateTime.UtcNow.AddDays(10)
        };
        var fileExtension = Path.GetExtension(fileName);
        var blobKey = $"temp/import/caves/{entity.Id}{fileExtension}";

        await AddToBlobStorage(stream, blobKey, RequestUser.AccountContainerName, cancellationToken);

        entity.BlobKey = blobKey;
        entity.BlobContainer = RequestUser.AccountContainerName;


        Repository.Add(entity);
        await Repository.SaveChangesAsync();
        await transaction.CommitAsync(cancellationToken);
        
        var fileInformation = new FileVm
        {
            Id = entity.Id,
            FileName = entity.FileName,
            DisplayName = entity.DisplayName,
            FileTypeTagId = entity.FileTypeTagId,
            Uuid = uuid,
        };
        return fileInformation;
    }

    private async Task RemoveExpiredFiles()
    {
        var expiredFiles = await Repository.GetExpiredFiles();
        foreach (var expiredFile in expiredFiles)
        {
            await DeleteFile(expiredFile.BlobKey, expiredFile.BlobContainer);
            Repository.Delete(expiredFile);
        }

        await Repository.SaveChangesAsync();
    }

    #region Blob Storage

    private async Task AddToBlobStorage(Stream stream, string key, string containerName,
        CancellationToken cancellationToken)
    {
        if (RequestUser.AccountId == null)
        {
            throw ApiExceptionDictionary.BadRequest("Account Id is null");
        }

        var client = await GetBlobContainerClient(containerName);
        var blobClient = client.GetBlobClient(key);
        
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: cancellationToken);
    }

    private async Task<BlobContainerClient> GetBlobContainerClient(string containerName)
    {
        if (RequestUser.AccountId == null)
        {
            throw ApiExceptionDictionary.BadRequest("Account Id is null");
        }

        var containerClient = new BlobContainerClient(_fileOptions.ConnectionString, containerName.ToLowerInvariant());
        await containerClient.CreateIfNotExistsAsync();
        
        return containerClient;
    }
    #endregion

    public async Task UpdateFilesMetadata(IEnumerable<EditFileMetadataVm> values, CancellationToken cancellationToken)
    {
        await using var transaction = await Repository.BeginTransactionAsync(cancellationToken);
        foreach (var value in values)
        {
            var file = await Repository.GetFileById(value.Id);
            if (file == null)
            {
                throw ApiExceptionDictionary.NotFound("File");
            }

            if (!string.IsNullOrWhiteSpace(value.DisplayName))
            {
                file.DisplayName = value.DisplayName;
                file.FileName = $"{value.DisplayName}{Path.GetExtension(file.FileName)}";
            }
            
            file.FileTypeTagId = value.FileTypeTagId;
            
            await Repository.SaveChangesAsync();
        }
        await transaction.CommitAsync();
    }

    public async Task<FileVm> GetFile(string id)
    {
        var file = await Repository.GetFileVm(id);
        var blobProperties = await Repository.GetFileBlobProperties(id);
        if (file == null || blobProperties == null || string.IsNullOrWhiteSpace(blobProperties.ContainerName) ||
            string.IsNullOrWhiteSpace(blobProperties.BlobKey))
        {
            throw ApiExceptionDictionary.NotFound("File");
        }

        var client = await GetBlobContainerClient(blobProperties.ContainerName);
        var blobClient = client.GetBlobClient(blobProperties.BlobKey);

        var sasLinkDownload = GetSasLink(blobClient, file.FileName, true);
        var sasLinkEmbed = GetSasLink(blobClient, file.FileName, true);
        file.EmbedUrl = sasLinkEmbed;
        file.DownloadUrl = sasLinkDownload;

        return file;
    }

    // private async Task<string> GetSasLink(BlobClient blobClient, string fileName)
    // {
    //     // Generate a SAS token for the blob with read permissions that expires in 1 hour
    //     BlobSasBuilder sasBuilder = new BlobSasBuilder()
    //     {
    //         BlobContainerName = blobClient.BlobContainerName,
    //         BlobName = blobClient.Name,
    //         Resource = "b", // "b" for blob
    //         StartsOn = DateTimeOffset.UtcNow,
    //         ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
    //         ContentDisposition = "attachment; filename=\"" + $"{fileName}" + "\"; filename*=UTF-8''" + Uri.EscapeDataString($"{fileName}")
    //     };
    //     sasBuilder.SetPermissions(BlobSasPermissions.Read);
    //
    //     var sasUri = blobClient.GenerateSasUri(sasBuilder);
    //
    //     var sasUrl = $"{sasUri}&metadata=filename={Uri.EscapeDataString(fileName)}";
    //
    //     return sasUrl;
    // }
    private string GetSasLink(BlobClient blobClient, string fileName, bool download = false)
    {
        // Get the file extension
        var fileExtension = Path.GetExtension(fileName);

        // Get the MIME type of the file
        var mimeType = MimeTypes.GetMimeType(fileExtension);

        // Generate a SAS token for the blob with read permissions that expires in 1 hour
        var sasBuilder = new BlobSasBuilder()
        {
            BlobContainerName = blobClient.BlobContainerName,
            BlobName = blobClient.Name,
            Resource = "b", // "b" for blob
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(1),
            ContentDisposition = download
                ? $"attachment; filename=\"{fileName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}"
                : $"inline; filename=\"{fileName}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}",
            ContentType = mimeType
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        var sasUrl = $"{sasUri}&metadata=filename={Uri.EscapeDataString(fileName)}";

        return sasUrl;
    }

    public async Task<string> GetLink(string blobKey, string containerName, string fileName, bool isDownload = false)
    {
        var client = await GetBlobContainerClient(containerName);
        var blobClient = client.GetBlobClient(blobKey);
        
        var sasLink = GetSasLink(blobClient, fileName, isDownload);
        return sasLink;
    }

    public async Task DeleteFile(string? blobKey, string? blobContainer)
    {
        if (string.IsNullOrWhiteSpace(blobKey) || string.IsNullOrWhiteSpace(blobContainer))
        {
            throw ApiExceptionDictionary.NotFound("File");
        }

        var client = await GetBlobContainerClient(blobContainer);
        var blobClient = client.GetBlobClient(blobKey);
        await blobClient.DeleteIfExistsAsync();
    }

    public async Task<Stream> GetFileStream(string fileId)
    {
        var file = await Repository.GetFileVm(fileId);
        var blobProperties = await Repository.GetFileBlobProperties(fileId);
        if (file == null || blobProperties == null || string.IsNullOrWhiteSpace(blobProperties.ContainerName) ||
            string.IsNullOrWhiteSpace(blobProperties.BlobKey))
        {
            throw ApiExceptionDictionary.NotFound("File");
        }

        var client = await GetBlobContainerClient(blobProperties.ContainerName);
        var blobClient = client.GetBlobClient(blobProperties.BlobKey);
        
        var stream = new MemoryStream();
        await blobClient.DownloadToAsync(stream);
        stream.Position = 0;
        return stream;
    }
}

    public class FileTypeTagName
    {
        public const string Other = "Other";
    }

    public class FileVm
{
    [MaxLength(PropertyLength.FileName)] public string FileName { get; set; } = null!;
    [MaxLength(PropertyLength.Name)] public string? DisplayName { get; set; }
    [MaxLength(PropertyLength.Id)] public string Id { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string FileTypeTagId { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string FileTypeKey { get; set; } = null!;
    public string? Uuid { get; set; }
    public string? EmbedUrl { get; set; }
    public string? DownloadUrl { get; set; }
}