using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Planarian.Modules.Account.Archive.Services;
using Xunit;

namespace Planarian.Tests.Unit.Account;

public sealed class ArchiveRuntimePolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(8, 4)]
    public void RetentionKeepsAtMostFourPriorArchivesBecauseCurrentArchiveIsTheFifth(
        int priorArchiveCount, int expectedPrunedCount)
    {
        var now = new DateTimeOffset(2026, 10, 27, 18, 0, 0, TimeSpan.Zero);
        var archives = Enumerable.Range(0, priorArchiveCount)
            .Select(index => ($"archives/{index:D2}.tar.gz", now.AddMinutes(-index)))
            .ToArray();

        var toPrune = ArchiveJobCoordinator.SelectArchivesToPrune(archives);

        Assert.Equal(expectedPrunedCount, toPrune.Count);
        Assert.Equal(
            Enumerable.Range(4, expectedPrunedCount).Select(index => $"archives/{index:D2}.tar.gz"),
            toPrune);
    }

    [Fact]
    public void RetentionDeduplicatesCandidateKeysBeforeCountingSurvivors()
    {
        var now = new DateTimeOffset(2026, 10, 27, 18, 0, 0, TimeSpan.Zero);
        var archives = new[]
        {
            ("archives/a.tar.gz", now.AddMinutes(-10)),
            ("archives/a.tar.gz", now),
            ("archives/b.tar.gz", now.AddMinutes(-1)),
            ("archives/c.tar.gz", now.AddMinutes(-2)),
            ("archives/d.tar.gz", now.AddMinutes(-3)),
            ("archives/e.tar.gz", now.AddMinutes(-4))
        };

        var toPrune = ArchiveJobCoordinator.SelectArchivesToPrune(archives);

        Assert.Equal(["archives/e.tar.gz"], toPrune);
    }

    [Fact]
    public void RetentionUsesBlobKeyAsDeterministicTieBreaker()
    {
        var timestamp = new DateTimeOffset(2026, 10, 27, 18, 0, 0, TimeSpan.Zero);
        var archives = new[] { "a", "f", "c", "e", "b", "d" }
            .Select(name => ($"archives/{name}.tar.gz", timestamp))
            .ToArray();

        var toPrune = ArchiveJobCoordinator.SelectArchivesToPrune(archives);

        Assert.Equal(["archives/b.tar.gz", "archives/a.tar.gz"], toPrune);
    }

    [Fact]
    public void AbandonedTempBoundaryIsInclusiveAndNeverDeletesCurrentOrRecentWork()
    {
        var now = new DateTimeOffset(2026, 10, 27, 18, 0, 0, TimeSpan.Zero);
        var boundary = now - ArchiveJobCoordinator.AbandonedTempArchiveMinimumAge;
        var current = "archives/temp/current/archive.tar.gz";
        var tempArchives = new[]
        {
            ("archives/temp/stale.tar.gz", boundary.AddTicks(-1)),
            ("archives/temp/boundary.tar.gz", boundary),
            ("archives/temp/recent.tar.gz", boundary.AddTicks(1)),
            (current, boundary.AddDays(-1)),
            ("archives/temp/stale.tar.gz", boundary.AddDays(-2))
        };

        var abandoned = ArchiveJobCoordinator.SelectAbandonedTempArchives(tempArchives, current, now);

        Assert.Equal(
            ["archives/temp/boundary.tar.gz", "archives/temp/stale.tar.gz"],
            abandoned);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData("pending", null, true)]
    [InlineData("success", "copy-1", true)]
    [InlineData("pending", "copy-1", false)]
    [InlineData("failed", "copy-1", false)]
    [InlineData("aborted", "copy-1", false)]
    [InlineData(null, "copy-1", false)]
    public void OnlyOrdinaryUploadsOrSuccessfullyCopiedArchivesAreReady(
        string? status,
        string? copyId,
        bool expected)
    {
        CopyStatus? copyStatus = status switch
        {
            null => null,
            "success" => CopyStatus.Success,
            "pending" => CopyStatus.Pending,
            "failed" => CopyStatus.Failed,
            "aborted" => CopyStatus.Aborted,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        Assert.Equal(expected, ArchiveJobCoordinator.IsArchiveReady(copyStatus, copyId));
    }

    [Theory]
    [InlineData("pending", "copy-1", true)]
    [InlineData("success", "copy-1", false)]
    [InlineData("failed", "copy-1", false)]
    [InlineData("aborted", "copy-1", false)]
    [InlineData("pending", null, false)]
    public void OnlyGenuinePendingCopyOperationsBlockTemporaryArchiveCleanup(
        string status,
        string? copyId,
        bool expected)
    {
        var copyStatus = status switch
        {
            "success" => CopyStatus.Success,
            "pending" => CopyStatus.Pending,
            "failed" => CopyStatus.Failed,
            "aborted" => CopyStatus.Aborted,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        Assert.Equal(expected, ArchiveJobCoordinator.IsArchiveCopyInProgress(copyStatus, copyId));
    }

    [Theory]
    [InlineData("failed", "copy-1", true)]
    [InlineData("aborted", "copy-1", true)]
    [InlineData("pending", "copy-1", false)]
    [InlineData("success", "copy-1", false)]
    [InlineData("failed", null, false)]
    public void OnlyFailedOrAbortedGenuineCopyDestinationsAreInvalidLegacyArchives(
        string status,
        string? copyId,
        bool expected)
    {
        var copyStatus = status switch
        {
            "success" => CopyStatus.Success,
            "pending" => CopyStatus.Pending,
            "failed" => CopyStatus.Failed,
            "aborted" => CopyStatus.Aborted,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

        Assert.Equal(expected, ArchiveJobCoordinator.IsArchiveCopyFailed(copyStatus, copyId));
    }

    [Fact]
    public void ArchivePublicationPlanCoversArchivesBeyondSinglePutBlobFromUrlLimitWithOrderedBlocks()
    {
        const long sixGiB = 6L * 1024 * 1024 * 1024;

        var blocks = ArchiveJobCoordinator.BuildArchivePublicationBlocks(sixGiB);

        Assert.Equal(24, blocks.Count);
        Assert.Equal(0, blocks[0].Offset);
        Assert.Equal(sixGiB, blocks.Sum(block => block.Length));
        Assert.All(blocks, block =>
            Assert.InRange(block.Length, 1, ArchiveJobCoordinator.PreferredArchivePublicationBlockBytes));
        Assert.Equal(
            Enumerable.Range(0, blocks.Count).Select(index => (long)index * ArchiveJobCoordinator.PreferredArchivePublicationBlockBytes),
            blocks.Select(block => block.Offset));
        Assert.Equal(blocks.Count, blocks.Select(block => block.BlockId).Distinct(StringComparer.Ordinal).Count());
        Assert.Single(blocks.Select(block => Convert.FromBase64String(block.BlockId).Length).Distinct());
    }

    [Fact]
    public void ArchivePublicationPlanExpandsBlockSizeAtAzureBlockCountBoundary()
    {
        var maximumContentLength =
            ArchiveJobCoordinator.MaxArchivePublicationBlockBytes *
            ArchiveJobCoordinator.MaxArchivePublicationBlocks;

        var blocks = ArchiveJobCoordinator.BuildArchivePublicationBlocks(maximumContentLength);

        Assert.Equal(ArchiveJobCoordinator.MaxArchivePublicationBlocks, blocks.Count);
        Assert.All(blocks, block =>
            Assert.Equal(ArchiveJobCoordinator.MaxArchivePublicationBlockBytes, block.Length));
        Assert.Equal(maximumContentLength, blocks[^1].Offset + blocks[^1].Length);
    }

    [Fact]
    public void ArchivePublicationPlanRejectsContentBeyondAzureBlockBlobCapacity()
    {
        var maximumContentLength =
            ArchiveJobCoordinator.MaxArchivePublicationBlockBytes *
            ArchiveJobCoordinator.MaxArchivePublicationBlocks;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ArchiveJobCoordinator.BuildArchivePublicationBlocks(maximumContentLength + 1));

        Assert.Contains("exceeds Azure block-blob publication capacity", exception.Message);
    }

    [Fact]
    public void ArchivePublicationSourceSasRequiresSharedKeyAndRestrictsHttpsSourcesToHttps()
    {
        var credential = new StorageSharedKeyCredential(
            "archiveaccount",
            Convert.ToBase64String(new byte[32]));
        var source = new BlobClient(
            new Uri("https://archiveaccount.blob.core.windows.net/account/archive.tar.gz"),
            credential);

        var sasUri = ArchiveJobCoordinator.CreateArchiveSourceReadSasUri(
            source,
            new DateTimeOffset(2026, 10, 27, 18, 0, 0, TimeSpan.Zero));

        Assert.Contains("sp=r", sasUri.Query, StringComparison.Ordinal);
        Assert.Contains("sr=b", sasUri.Query, StringComparison.Ordinal);
        Assert.Contains("spr=https", sasUri.Query, StringComparison.Ordinal);

        var anonymousSource = new BlobClient(
            new Uri("https://archiveaccount.blob.core.windows.net/account/archive.tar.gz"));
        Assert.Throws<InvalidOperationException>(() =>
            ArchiveJobCoordinator.CreateArchiveSourceReadSasUri(
                anonymousSource,
                new DateTimeOffset(2026, 10, 27, 18, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void CurrentGenerationUsesJobScopedKeysThatCannotCollideWithLegacyTimestampKeys()
    {
        const string fileName = "Example Account Archive 20261027181600.tar.gz";
        var operationId = Guid.Parse("98e50954-b44d-4b38-97bb-48de49cdb641");

        var keys = ArchiveJobCoordinator.BuildArchiveBlobKeys(fileName, operationId);

        Assert.Equal(
            "archives/temp/98e50954b44d4b3897bb48de49cdb641/Example Account Archive 20261027181600.tar.gz",
            keys.TempBlobKey);
        Assert.Equal(
            "archives/98e50954b44d4b3897bb48de49cdb641/Example Account Archive 20261027181600.tar.gz",
            keys.FinalBlobKey);
        Assert.NotEqual($"archives/temp/{fileName}", keys.TempBlobKey);
        Assert.NotEqual($"archives/{fileName}", keys.FinalBlobKey);
        Assert.Equal(fileName, Path.GetFileName(keys.FinalBlobKey));
    }

    [Theory]
    [InlineData("archives/temp/legacy.tar.gz", "archives/legacy.tar.gz")]
    [InlineData("archives/temp/abc123/archive.tar.gz", "archives/abc123/archive.tar.gz")]
    [InlineData("archives/temp/", null)]
    [InlineData("archives/not-temp/archive.tar.gz", null)]
    public void TempArchiveKeyMapsToItsPublicationKey(string tempKey, string? expectedFinal)
    {
        Assert.Equal(expectedFinal, ArchiveJobCoordinator.TryGetFinalArchiveBlobKey(tempKey));
    }

    [Fact]
    public async Task BestEffortCleanupDeduplicatesAttemptsContinuesAfterFailuresAndReportsOnlyFailures()
    {
        var attempted = new List<string>();

        var failures = await ArchiveJobCoordinator.DeleteArchiveKeysBestEffortAsync(
            ["archives/a.tar.gz", "archives/b.tar.gz", "archives/a.tar.gz", "archives/c.tar.gz"],
            key =>
            {
                attempted.Add(key);
                if (key == "archives/b.tar.gz")
                    throw new IOException("simulated provider failure");
                return Task.CompletedTask;
            });

        Assert.Equal(["archives/a.tar.gz", "archives/b.tar.gz", "archives/c.tar.gz"], attempted);
        Assert.Equal(["archives/b.tar.gz"], failures);
    }
}
