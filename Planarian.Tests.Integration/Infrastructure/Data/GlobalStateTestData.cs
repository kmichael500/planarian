using Microsoft.EntityFrameworkCore;

namespace Planarian.Tests;

/// <summary>
/// The sole ordinary-fixture SQL escape hatch. Production SaveChanges
/// protection forbids every mutation of the global State lookup, so tests use
/// this parameterized, State-only operation before returning to typed EF setup.
/// </summary>
internal static class GlobalStateTestData
{
    public static async Task AddAsync(PostgresTestDatabase database, string id, string name, string abbreviation)
    {
        await using var db = database.CreateDbContext("state-seed", accountId: null);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            insert into "States" ("Id", "Name", "Abbreviation", "CreatedOn")
            values ({id}, {name}, {abbreviation}, now())
            """);
    }
}
