using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Planarian.Tests.EmailDelivery.TestInfrastructure;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Migrations;

public sealed class MessageTypeRemovalMigrationTests
{
    [Fact]
    public async Task MigrationRemovesTemplateTableAndKeepsDeliveryLogTables()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await using var context = IsolatedPostgresDatabase.NewContext(connectionString);

            Assert.True(await TableExists(context, "MessageLogs"));
            Assert.True(await TableExists(context, "MessageLogEvents"));
            Assert.False(await TableExists(context, "MessageTypes"));

            var previousVersion = Assert.Single(context.Database.GetMigrations()
                .Where(migration => migration.EndsWith("_v32", StringComparison.Ordinal)));
            await context.Database.GetService<IMigrator>().MigrateAsync(previousVersion);

            // Rolling back restores the old schema, but not the retired template data.
            Assert.True(await TableExists(context, "MessageTypes"));
            Assert.True(await TableExists(context, "MessageLogs"));
            Assert.True(await TableExists(context, "MessageLogEvents"));
        });
    }

    private static Task<bool> TableExists(Microsoft.EntityFrameworkCore.DbContext context, string tableName)
    {
        return context.Database.SqlQueryRaw<bool>(
                "SELECT to_regclass({0}) IS NOT NULL AS \"Value\"", "public.\"" + tableName + "\"")
            .SingleAsync();
    }
}
