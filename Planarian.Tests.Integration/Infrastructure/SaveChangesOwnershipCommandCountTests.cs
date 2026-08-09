using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NetTopologySuite.Geometries;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Xunit;

namespace Planarian.Tests;

internal sealed class DbCommandCounter : DbCommandInterceptor
{
    private readonly ConcurrentQueue<string> _commands = new();
    public IReadOnlyCollection<string> Commands => _commands.ToArray();
    public void Reset() { while (_commands.TryDequeue(out _)) { } }
    private void Record(DbCommand command) => _commands.Enqueue(command.CommandText);
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r) { Record(c); return r; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<DbDataReader> r, CancellationToken t = default) { Record(c); return ValueTask.FromResult(r); }
    public override InterceptionResult<int> NonQueryExecuting(DbCommand c, CommandEventData e, InterceptionResult<int> r) { Record(c); return r; }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand c, CommandEventData e, InterceptionResult<int> r, CancellationToken t = default) { Record(c); return ValueTask.FromResult(r); }
}

public sealed class SaveChangesOwnershipCommandCountTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task SixHundredCaveAssociationsUseBoundedOwnershipSelects()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(SixHundredCaveAssociationsUseBoundedOwnershipSelects));
        var tenant = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database, 'a');
        var counter = new DbCommandCounter();
        await using var db = CreateContext(database, tenant.AccountId, counter);
        var tags = Enumerable.Range(0, 600).Select(i => new TagType($"Tag {i}", "geology") { Id = $"t{i:D9}", AccountId = tenant.AccountId, IsDefault = false }).ToList();
        db.TagTypes.AddRange(tags);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        db.GeologyTags.AddRange(tags.Select(tag => new GeologyTag { Id = Guid.NewGuid().ToString("N")[..10], CaveId = tenant.CaveId, TagTypeId = tag.Id }));
        counter.Reset();
        await db.SaveChangesAsync();
        var ownership = counter.Commands.Count(sql => sql.Contains("FROM \"Caves\"", StringComparison.OrdinalIgnoreCase) && sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(ownership, 1, 2);
    }

    [Fact]
    public async Task EntranceAssociationsUseBoundedDistinctEntranceOwnershipSelects()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(EntranceAssociationsUseBoundedDistinctEntranceOwnershipSelects));
        var tenant = await TestDataScenarios.CreatePublishedCaveScenarioAsync(database, 'a');
        string qualityId, statusId;
        await using (var seed = database.CreateDbContext("seed", tenant.AccountId))
        {
            var quality = new TagType("Survey Grade", "location-quality") { AccountId = tenant.AccountId, IsDefault = false };
            var status = new TagType("Open", "entrance-status") { AccountId = tenant.AccountId, IsDefault = false };
            seed.TagTypes.AddRange(quality, status); await seed.SaveChangesAsync();
            qualityId = quality.Id; statusId = status.Id;
            seed.Entrances.AddRange(Enumerable.Range(0,120).Select(i => new Entrance { Id=$"e{i:D9}", CaveId=tenant.CaveId, LocationQualityTagId=qualityId, IsPrimary=i==0, Location=new Point(new CoordinateZ(-86-i/10000d,35+i/10000d,500+i)){SRID=4326} }));
            await seed.SaveChangesAsync();
        }
        var counter = new DbCommandCounter();
        await using var db = CreateContext(database, tenant.AccountId, counter);
        db.EntranceStatusTags.AddRange(Enumerable.Range(0,120).Select(i => new EntranceStatusTag { EntranceId=$"e{i:D9}", TagTypeId=statusId }));
        counter.Reset(); await db.SaveChangesAsync();
        var ownership = counter.Commands.Count(sql => sql.Contains("FROM \"Entrances\"", StringComparison.OrdinalIgnoreCase) && sql.Contains("SELECT", StringComparison.OrdinalIgnoreCase));
        Assert.InRange(ownership, 1, 2);
    }

    private static PlanarianDbContext CreateContext(PostgresTestDatabase database, string accountId, DbCommandCounter counter)
    {
        // Account-scoped writes are audit-stamped by SaveChangesInterceptor, so
        // the command-count context must use a persisted User just like a real
        // request. CreateDbContext performs that idempotent fixture setup.
        using (database.CreateDbContext("counter", accountId)) { }

        var options = new DbContextOptionsBuilder<PlanarianDbContext>().UseNpgsql(database.ConnectionString, o => { o.MigrationsAssembly("Planarian.Migrations"); o.UseNetTopologySuite(); }).AddInterceptors(counter).Options;
        var db = new PlanarianDbContext(options);
        db.RequestUser = new RequestUser(db) { Id="counter", AccountId=accountId, FirstName="Command", LastName="Counter" };
        return db;
    }
}
