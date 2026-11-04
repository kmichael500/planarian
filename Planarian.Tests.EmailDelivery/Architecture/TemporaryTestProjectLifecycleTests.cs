using Planarian.Tests.EmailDelivery.TestInfrastructure;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Architecture;

public sealed class TemporaryTestProjectLifecycleTests
{
    [Fact]
    public void TemporaryProjectMustBeMigratedWhenCanonicalBackendTestProjectsExist()
    {
        var repositoryRoot = RepositoryPaths.Root;
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

}
