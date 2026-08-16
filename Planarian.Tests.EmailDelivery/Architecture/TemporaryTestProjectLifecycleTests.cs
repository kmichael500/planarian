using Xunit;

namespace Planarian.Tests.EmailDelivery.Architecture;

public sealed class TemporaryTestProjectLifecycleTests
{
    [Fact]
    public void TemporaryProjectMustBeMigratedWhenCanonicalBackendTestProjectsExist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var canonicalProjects = new[]
        {
            "Planarian.Tests.Unit/Planarian.Tests.Unit.csproj",
            "Planarian.Tests.Integration/Planarian.Tests.Integration.csproj"
        };
        var existing = canonicalProjects
            .Where(relativePath => File.Exists(Path.Combine(repositoryRoot, relativePath)))
            .ToArray();

        Assert.True(existing.Length == 0,
            "Planarian.Tests.EmailDelivery is temporary. Canonical backend test infrastructure now exists (" +
            string.Join(", ", existing) +
            "). Move these tests into the canonical projects, implement the deferred PostgreSQL tests from " +
            "Planarian.Tests.EmailDelivery/README.md, then delete this project and its temporary workflow.");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the repository root for the temporary test-project guard.");
    }
}
