
using System.ComponentModel.DataAnnotations;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Files.Repositories;
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

    public FileService(FileRepository repository, RequestUser requestUser, TagRepository tagRepository, FileOptions fileOptions) : base(repository, requestUser)
    {
        _tagRepository = tagRepository;
        _fileOptions = fileOptions;
    }

    public async Task<FileVm> UploadCaveFile(Stream stream, string caveId, string fileName, string? uuid = null)
    {
        await using var transaction = await Repository.BeginTransactionAsync();
        if (RequestUser.AccountId == null)
        {
            throw new BadRequestException("Account Id is null");
        }

        var other = await _tagRepository.GetFileTypeTagByName(FileTypeTagName.Other, RequestUser.AccountId);

        //fileName without extension
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        var entity = new File()
        {
            CaveId = caveId,
            FileName = fileName,
            DisplayName = fileNameWithoutExtension,
            AccountId = RequestUser.AccountId,
            FileTypeTag = other ?? throw new InvalidOperationException()
        };

        Repository.Add(entity);
        await Repository.SaveChangesAsync();


        var fileExtension = Path.GetExtension(fileName);
        var blobKey = $"caves/{caveId}/files/{entity.Id}{fileExtension}";

        await AddToBlobStorage(stream, blobKey, RequestUser.AccountContainerName);

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

    #region Blob Storage

    private async Task AddToBlobStorage(Stream stream, string key, string containerName)
    {
        if (RequestUser.AccountId == null)
        {
            throw ApiExceptionDictionary.BadRequest("Account Id is null");
        }

        var client = await GetBlobContainerClient(containerName);
        var blobClient = client.GetBlobClient(key);

        await blobClient.UploadAsync(stream, overwrite: true);
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
}