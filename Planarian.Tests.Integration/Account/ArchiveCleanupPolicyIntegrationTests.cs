using Planarian.Modules.Account.Archive.Services;
using Xunit;

namespace Planarian.Tests.Integration.ArchiveCleanup;

public sealed class ArchiveCleanupPolicyIntegrationTests
{
    [Fact]
    public void RetentionPolicyKeepsFourPriorArchivesBecauseCurrentArchiveIsTheFifth()
    {
        var now = DateTimeOffset.UtcNow;
        var archives = new[]
        {
            ("archives/a.tar.gz", now.AddHours(-1)),
            ("archives/b.tar.gz", now.AddHours(-2)),
            ("archives/c.tar.gz", now.AddHours(-3)),
            ("archives/d.tar.gz", now.AddHours(-4)),
            ("archives/e.tar.gz", now.AddHours(-5)),
            ("archives/f.tar.gz", now.AddHours(-6))
        };

        var toPrune = ArchiveJobCoordinator.SelectArchivesToPrune(archives);

        Assert.Equal(["archives/e.tar.gz", "archives/f.tar.gz"], toPrune);
    }

    [Fact]
    public void AbandonedTempPolicyDeletesOnlyStaleNonCurrentArchives()
    {
        var now = new DateTimeOffset(2026, 10, 27, 20, 0, 0, TimeSpan.Zero);
        var stale = now - ArchiveJobCoordinator.AbandonedTempArchiveMinimumAge - TimeSpan.FromMinutes(1);
        var recent = now - ArchiveJobCoordinator.AbandonedTempArchiveMinimumAge + TimeSpan.FromMinutes(1);
        var tempArchives = new[]
        {
            ("archives/temp/stale-b.tar.gz", stale),
            ("archives/temp/current.tar.gz", stale),
            ("archives/temp/stale-a.tar.gz", stale),
            ("archives/temp/recent-old-version-job.tar.gz", recent),
            ("archives/temp/stale-b.tar.gz", stale)
        };

        var abandoned = ArchiveJobCoordinator.SelectAbandonedTempArchives(
            tempArchives, "archives/temp/current.tar.gz", now);

        Assert.Equal(
            ["archives/temp/stale-a.tar.gz", "archives/temp/stale-b.tar.gz"],
            abandoned);
        Assert.DoesNotContain("archives/temp/current.tar.gz", abandoned);
        Assert.DoesNotContain("archives/temp/recent-old-version-job.tar.gz", abandoned);
    }

    [Fact]
    public async Task BestEffortArchiveCleanupContinuesPastFailureAndReportsOnlyFailedKeys()
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

        Assert.Equal(
            ["archives/a.tar.gz", "archives/b.tar.gz", "archives/c.tar.gz"],
            attempted);
        Assert.Equal(["archives/b.tar.gz"], failures);
    }
}
