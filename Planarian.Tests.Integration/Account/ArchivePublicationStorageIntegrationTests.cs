using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Planarian.Modules.Account.Archive.Services;
using Xunit;

namespace Planarian.Tests.Integration.AccountArchive;

public sealed class ArchivePublicationStorageIntegrationTests(AzuriteTestServer azurite)
    : IClassFixture<AzuriteTestServer>
{
    [Fact]
    public async Task PublicationCopiesExactBytesWithoutOverwritingAnExistingFinalObject()
    {
        var container = await CreateContainerAsync();
        const string tempKey = "archives/temp/job-one/archive.tar.gz";
        const string finalKey = "archives/job-one/archive.tar.gz";
        var payload = Enumerable.Range(0, 100_000).Select(index => (byte)(index * 17)).ToArray();

        await container.GetBlobClient(tempKey).UploadAsync(new BinaryData(payload), overwrite: true);
        await ArchiveJobCoordinator.PublishArchive(
            container,
            tempKey,
            finalKey,
            CancellationToken.None,
            preferredBlockSizeBytes: 16 * 1024);

        Assert.Equal(payload, await DownloadAsync(container, finalKey));
        Assert.True((await container.GetBlobClient(tempKey).ExistsAsync()).Value);
        var committedBlocks = await container.GetBlockBlobClient(finalKey)
            .GetBlockListAsync(BlockListTypes.Committed);
        Assert.Equal(7, committedBlocks.Value.CommittedBlocks.Count());

        // A retry after a lost response is idempotent: the conditional create gets a
        // conflict, but identical temp/final bytes reconcile as a successful publication.
        await ArchiveJobCoordinator.PublishArchive(
            container, tempKey, finalKey, CancellationToken.None);
        Assert.Equal(payload, await DownloadAsync(container, finalKey));

        var replacement = new byte[] { 1, 2, 3, 4 };
        await container.GetBlobClient(tempKey).UploadAsync(new BinaryData(replacement), overwrite: true);

        var conflict = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ArchiveJobCoordinator.PublishArchive(container, tempKey, finalKey, CancellationToken.None));
        Assert.Contains("different content", conflict.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(payload, await DownloadAsync(container, finalKey));
        Assert.Equal(replacement, await DownloadAsync(container, tempKey));

        const string retryFinalKey = "archives/job-two/archive.tar.gz";
        await ArchiveJobCoordinator.PublishArchive(
            container, tempKey, retryFinalKey, CancellationToken.None);
        Assert.Equal(replacement, await DownloadAsync(container, retryFinalKey));
    }

    [Fact]
    public async Task PublicationSourceSasIsBlobScopedReadOnlyAndShortLived()
    {
        var container = await CreateContainerAsync();
        const string sourceKey = "archives/temp/sas-source/archive.tar.gz";
        const string neighborKey = "archives/temp/sas-neighbor/archive.tar.gz";
        var payload = new byte[] { 11, 22, 33, 44 };

        await container.GetBlobClient(sourceKey).UploadAsync(new BinaryData(payload), overwrite: true);
        await container.GetBlobClient(neighborKey).UploadAsync(new BinaryData(new byte[] { 99 }), overwrite: true);

        var now = DateTimeOffset.UtcNow;
        var sasUri = ArchiveJobCoordinator.CreateArchiveSourceReadSasUri(
            container.GetBlobClient(sourceKey),
            now);
        var sasClient = new BlobClient(sasUri);

        Assert.Equal(payload, (await sasClient.DownloadContentAsync()).Value.Content.ToArray());
        Assert.Equal("r", GetQueryParameter(sasUri, "sp"));
        Assert.Equal("b", GetQueryParameter(sasUri, "sr"));

        var startsOn = DateTimeOffset.Parse(
            GetQueryParameter(sasUri, "st"),
            CultureInfo.InvariantCulture);
        var expiresOn = DateTimeOffset.Parse(
            GetQueryParameter(sasUri, "se"),
            CultureInfo.InvariantCulture);
        Assert.True(startsOn <= now);
        Assert.True(expiresOn > now);
        Assert.True(expiresOn - startsOn <= ArchiveJobCoordinator.ArchiveSourceSasValidity);

        var writeFailure = await Assert.ThrowsAsync<RequestFailedException>(() =>
            sasClient.UploadAsync(new BinaryData(new byte[] { 1 }), overwrite: true));
        Assert.Equal(403, writeFailure.Status);

        var neighborUri = new UriBuilder(container.GetBlobClient(neighborKey).Uri)
        {
            Query = sasUri.Query.TrimStart('?')
        }.Uri;
        var scopeFailure = await Assert.ThrowsAsync<RequestFailedException>(() =>
            new BlobClient(neighborUri).DownloadContentAsync());
        Assert.Equal(403, scopeFailure.Status);
    }

    [Fact]
    public async Task CurrentPublisherCanRunConcurrentlyWithPinnedHistoricalTimestampPublisher()
    {
        var container = await CreateContainerAsync();
        const string fileName = "Account A Archive 20261027181600.tar.gz";

        // Historical implementation pinned from 78dfce40^:
        // temp = archives/temp/{fileName}; final = archives/{fileName};
        // StartCopyFromUriAsync(temp.Uri); WaitForCompletionAsync().
        var historicalTemp = $"archives/temp/{fileName}";
        var historicalFinal = $"archives/{fileName}";
        var currentKeys = ArchiveJobCoordinator.BuildArchiveBlobKeys(
            fileName,
            Guid.Parse("4aa6d9bc-8da2-46ac-912f-231501b211a1"));

        var oldPayload = Enumerable.Repeat((byte)0x4f, 8192).ToArray();
        var newPayload = Enumerable.Repeat((byte)0x4e, 8192).ToArray();
        await container.GetBlobClient(historicalTemp).UploadAsync(new BinaryData(oldPayload), overwrite: true);
        await container.GetBlobClient(currentKeys.TempBlobKey).UploadAsync(new BinaryData(newPayload), overwrite: true);

        await Task.WhenAll(
            PublishExactlyAsHistoricalVersionAsync(container, historicalTemp, historicalFinal),
            ArchiveJobCoordinator.PublishArchive(
                container, currentKeys.TempBlobKey, currentKeys.FinalBlobKey, CancellationToken.None));

        Assert.NotEqual(historicalFinal, currentKeys.FinalBlobKey);
        Assert.NotEqual(historicalTemp, currentKeys.TempBlobKey);
        Assert.Equal(oldPayload, await DownloadAsync(container, historicalFinal));
        Assert.Equal(newPayload, await DownloadAsync(container, currentKeys.FinalBlobKey));
        Assert.Equal(oldPayload, await DownloadAsync(container, historicalTemp));
        Assert.Equal(newPayload, await DownloadAsync(container, currentKeys.TempBlobKey));
    }

    [Fact]
    public async Task RetentionSelectionAppliedToRealStorageKeepsFourPriorArchivesPlusCurrent()
    {
        var container = await CreateContainerAsync();
        var prior = new List<(string BlobKey, DateTimeOffset LastModified)>();

        for (var index = 0; index < 7; index++)
        {
            var key = $"archives/prior-{index:D2}.tar.gz";
            await container.GetBlobClient(key).UploadAsync(new BinaryData(new byte[] { (byte)index }), overwrite: true);
            var properties = await container.GetBlobClient(key).GetPropertiesAsync();
            prior.Add((key, properties.Value.LastModified));
            await Task.Delay(2);
        }

        const string current = "archives/current/current.tar.gz";
        await container.GetBlobClient(current).UploadAsync(new BinaryData(new byte[] { 99 }), overwrite: true);

        var toPrune = ArchiveJobCoordinator.SelectArchivesToPrune(prior);
        foreach (var key in toPrune)
            await container.DeleteBlobIfExistsAsync(key);

        var survivors = new List<string>();
        await foreach (var blob in container.GetBlobsAsync(prefix: ArchiveJobCoordinator.ArchivePrefix))
        {
            if (!blob.Name.StartsWith(ArchiveJobCoordinator.TempArchivePrefix, StringComparison.OrdinalIgnoreCase))
                survivors.Add(blob.Name);
        }

        Assert.Contains(current, survivors);
        Assert.Equal(5, survivors.Count);
        Assert.Equal(
            prior.OrderByDescending(item => item.LastModified)
                .ThenByDescending(item => item.BlobKey, StringComparer.Ordinal)
                .Take(4)
                .Select(item => item.BlobKey)
                .Append(current)
                .Order(StringComparer.Ordinal),
            survivors.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task AbandonedTempCleanupCandidatesAppliedToRealStorageNeverDeleteRecentOrActiveTemp()
    {
        var container = await CreateContainerAsync();
        var now = DateTimeOffset.UtcNow;
        var active = "archives/temp/active/archive.tar.gz";
        var stale = "archives/temp/stale/archive.tar.gz";
        var recent = "archives/temp/recent/archive.tar.gz";

        foreach (var key in new[] { active, stale, recent })
            await container.GetBlobClient(key).UploadAsync(new BinaryData(new byte[] { 1 }), overwrite: true);

        // Blob LastModified cannot be backdated in Azure. Feed the production selector with
        // the real keys, then execute the selected deletes against real storage.
        var candidates = ArchiveJobCoordinator.SelectAbandonedTempArchives(
            new[]
            {
                (active, now - TimeSpan.FromDays(30)),
                (stale, now - TimeSpan.FromDays(8)),
                (recent, now - TimeSpan.FromDays(6))
            },
            active,
            now);

        foreach (var key in candidates)
            await container.DeleteBlobIfExistsAsync(key);

        Assert.True((await container.GetBlobClient(active).ExistsAsync()).Value);
        Assert.False((await container.GetBlobClient(stale).ExistsAsync()).Value);
        Assert.True((await container.GetBlobClient(recent).ExistsAsync()).Value);
    }

    private async Task<BlobContainerClient> CreateContainerAsync()
    {
        var client = new BlobServiceClient(azurite.ConnectionString)
            .GetBlobContainerClient($"archive-{Guid.NewGuid():N}");
        await client.CreateAsync();
        return client;
    }

    private static async Task PublishExactlyAsHistoricalVersionAsync(
        BlobContainerClient container,
        string tempKey,
        string finalKey)
    {
        var operation = await container.GetBlobClient(finalKey)
            .StartCopyFromUriAsync(container.GetBlobClient(tempKey).Uri);
        await operation.WaitForCompletionAsync();
    }

    private static async Task<byte[]> DownloadAsync(BlobContainerClient container, string key)
    {
        var content = await container.GetBlobClient(key).DownloadContentAsync();
        return content.Value.Content.ToArray();
    }

    private static string GetQueryParameter(Uri uri, string name)
    {
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], name, StringComparison.Ordinal))
                return Uri.UnescapeDataString(parts[1]);
        }

        throw new InvalidOperationException($"Query parameter '{name}' was not present.");
    }
}
