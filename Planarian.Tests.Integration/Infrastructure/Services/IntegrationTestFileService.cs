using Planarian.Shared.Options;
using Planarian.Shared.Services;
using FileOptions = Planarian.Shared.Options.FileOptions;

namespace Planarian.Tests.Integration.Infrastructure.Services;

/// <summary>
/// Uses the real Azure transport for chunk/archive operations while replacing only
/// whole-container deletion with the in-memory object boundary used by reset tests.
/// </summary>
internal sealed class IntegrationTestAzureBlobTransportService(
    FileOptions fileOptions,
    TestFileBlobStore blobs)
    : AzureBlobTransportService(fileOptions, blobs)
{
    public override async Task DeleteContainer(string containerName)
    {
        if (blobs.BeforeContainerDeleteAsync is not null)
            await blobs.BeforeContainerDeleteAsync(containerName);
        if (blobs.FailContainerDeletes)
            throw new IOException("The test blob boundary rejected the container delete.");
        blobs.DeleteContainer(containerName);
    }
}
