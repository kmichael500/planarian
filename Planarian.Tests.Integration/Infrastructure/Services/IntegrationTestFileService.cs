using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Repositories;
using Planarian.Modules.Files.Services;
using Planarian.Modules.Settings.Repositories;
using Planarian.Modules.Tags.Repositories;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
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
    TagReferenceLockRepository tagReferenceLocks,
    TestFileBlobStore blobs)
    : FileService(repository, requestUser, tagRepository, fileOptions, settingsRepository, caveRepository,
        requestThrottleService, caveMutationCoordinator, tagReferenceLocks, blobs)
{
    protected override Task AddToBlobStorage(Stream stream, string key, string containerName,
        CancellationToken cancellationToken) => blobs.WriteAsync(stream, containerName, key, cancellationToken);

    public override async Task DeleteContainer(string containerName)
    {
        if (blobs.BeforeContainerDeleteAsync is not null)
            await blobs.BeforeContainerDeleteAsync(containerName);
        if (blobs.FailContainerDeletes)
            throw new IOException("The test blob boundary rejected the container delete.");
        blobs.DeleteContainer(containerName);
    }


}
