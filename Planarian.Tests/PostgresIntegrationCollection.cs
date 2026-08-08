using Xunit;

namespace Planarian.Tests;

[CollectionDefinition(Name)]
public sealed class PostgresIntegrationCollection : ICollectionFixture<PostgresIntegrationFixture>
{
    public const string Name = "Postgres integration";
}
