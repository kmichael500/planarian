using Xunit;

namespace Planarian.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ScaleIntegrationCollection
{
    public const string Name = "Scale integration";
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MigrationIntegrationCollection
{
    public const string Name = "Migration integration";
}
