using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Options;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Tests.Integration.Infrastructure.Services;

/// <summary>Keeps production FileService behavior while replacing only its Azure boundary.</summary>
internal sealed class IntegrationTestFileService(
    FileRepository repository,
    RequestUser requestUser,
    TagRepository tagRepository,
    FileOptions fileOptions,
    SettingsRepository settingsRepository,
    CaveRepository caveRepository,
    RequestThrottleService requestThrottleService,
    CaveMutationCoordinator caveMutationCoordinator,
    TestFileBlobStore blobs)
    : FileService(repository, requestUser, tagRepository, fileOptions, settingsRepository, caveRepository,
        requestThrottleService, caveMutationCoordinator)
{
    protected override Task AddToBlobStorage(Stream stream, string key, string containerName,
        CancellationToken cancellationToken) => blobs.WriteAsync(stream, containerName, key, cancellationToken);

    protected override Task BestEffortDeleteBlobAsync(string blobKey, string blobContainer)
    {
        blobs.Delete(blobContainer, blobKey);
        return Task.CompletedTask;
    }

    public override async Task<(Stream Stream, string FileName)> OpenUnpublishedFileAsync(string fileId,
        CancellationToken cancellationToken)
    {
        var file = await Repository.GetFileAccessInfo(fileId);
        if (file is null || !string.IsNullOrWhiteSpace(file.CaveId) ||
            string.IsNullOrWhiteSpace(file.BlobKey) || string.IsNullOrWhiteSpace(file.ContainerName))
            throw Planarian.Library.Exceptions.ApiExceptionDictionary.NotFound("Staged file");
        return (blobs.OpenRead(file.ContainerName, file.BlobKey), file.FileName);
    }
}
