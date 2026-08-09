using Xunit;

namespace Planarian.Tests;

[CollectionDefinition(Name)]
public sealed class PostgresIntegrationCollection : ICollectionFixture<PostgresTestServer>
{
    public const string Name = "Postgres integration";
}
