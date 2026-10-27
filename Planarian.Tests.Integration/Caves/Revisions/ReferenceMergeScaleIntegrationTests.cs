using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Revisions;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Tags.Repositories;
using Planarian.Tests.Integration.Infrastructure.Services;
using Xunit;

using Planarian.Tests;

namespace Planarian.Tests.Integration.Caves.Revisions;

[Collection(ScaleIntegrationCollection.Name)]
public sealed class ReferenceMergeScaleIntegrationTests(PostgresTestServer fixture)
    : IClassFixture<PostgresTestServer>
{
    private const int CaveCount = 10_000;
    private const int MultiBatchCount = 1_200;

    [Fact]
    public async Task TenThousandCaveTagMergeRemainsBoundedAndPublishesExactlyOneRevisionPerCave()
    {
        const string accountId = "tagmerge01";
        const string stateId = "tagstate01";
        const string countyId = "tagcounty1";
        const string sourceId = "tagsource1";
        const string destinationId = "tagdest001";
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenThousandCaveTagMergeRemainsBoundedAndPublishesExactlyOneRevisionPerCave));
        await SeedLegacyBiologyCavesAsync(database, CaveCount, accountId, stateId, countyId, sourceId, destinationId);

        await using (var rollout = database.CreateDbContext("rollout-scale", accountId))
        {
            var result = await new CaveRevisionRolloutRepository(rollout).InitializeAsync();
            Assert.Equal(CaveCount, result.InitializedCaveCount);
        }

        var counter = new DbCommandCounter();
        await using (var merge = database.CreateDbContext("manager", accountId, counter))
        {
            counter.Reset();
            await TagRepository(merge).MergeAsync([sourceId], destinationId);
            Assert.InRange(counter.Commands.Count, 1, 500);
            Assert.Empty(merge.ChangeTracker.Entries());
        }

        await using var verify = database.CreateDbContext("verify", accountId);
        Assert.Equal(0, await verify.BiologyTags.AsNoTracking().CountAsync(tag => tag.TagTypeId == sourceId));
        Assert.Equal(CaveCount,
            await verify.BiologyTags.AsNoTracking().CountAsync(tag => tag.TagTypeId == destinationId));
        Assert.Equal(CaveCount * 2, await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == accountId));
        Assert.Equal(CaveCount, await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == accountId &&
                                    revision.Source == CaveRevisionSource.ManagerEdit &&
                                    revision.Operation == CaveRevisionOperation.Update &&
                                    revision.PreviousRevisionId != null));
        Assert.True(await verify.TagTypes.IgnoreQueryFilters().AnyAsync(tag => tag.Id == sourceId));
    }

    [Fact]
    public async Task TenThousandCaveCountyMergeRemainsBoundedPreservesNumbersAndPublishesExactlyOneRevisionPerCave()
    {
        const string accountId = "cntmerge01";
        const string stateId = "cntstate01";
        const string sourceCountyId = "cntsource1";
        const string destinationCountyId = "cntdest001";
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(TenThousandCaveCountyMergeRemainsBoundedPreservesNumbersAndPublishesExactlyOneRevisionPerCave));
        await SeedLegacyCountyCavesAsync(
            database, CaveCount, accountId, stateId, sourceCountyId, destinationCountyId);

        await using (var rollout = database.CreateDbContext("rollout-scale", accountId))
        {
            var result = await new CaveRevisionRolloutRepository(rollout).InitializeAsync();
            Assert.Equal(CaveCount, result.InitializedCaveCount);
        }

        var counter = new DbCommandCounter();
        await using (var merge = database.CreateDbContext("manager", accountId, counter))
        {
            counter.Reset();
            await IntegrationTestServices.For(merge).Account.MergeCounties(
                stateId, [sourceCountyId], destinationCountyId, default);
            Assert.InRange(counter.Commands.Count, 1, 500);
            Assert.Empty(merge.ChangeTracker.Entries());
        }

        await using var verify = database.CreateDbContext("verify", accountId);
        Assert.Equal(CaveCount, await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(cave => cave.AccountId == accountId && cave.CountyId == destinationCountyId));
        Assert.Equal(CaveCount, await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .Where(cave => cave.AccountId == accountId).Select(cave => cave.CountyNumber).Distinct().CountAsync());
        Assert.Equal(CaveCount * 2, await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == accountId));
        Assert.Equal(CaveCount, await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == accountId &&
                                    revision.Source == CaveRevisionSource.ManagerEdit &&
                                    revision.Operation == CaveRevisionOperation.Update &&
                                    revision.PreviousRevisionId != null));
        Assert.True(await verify.Counties.IgnoreQueryFilters().AnyAsync(county => county.Id == sourceCountyId));
    }

    [Fact]
    public async Task FailureAtSecondRevisionPublicationChunkRollsBackAllTagRelationshipsAndRevisionPointers()
    {
        const string accountId = "fltmerge01";
        const string stateId = "fltstate01";
        const string countyId = "faultcnt01";
        const string sourceId = "faultsrc01";
        const string destinationId = "faultdst01";
        await using var database = await fixture.CreateDatabaseAsync(
            nameof(FailureAtSecondRevisionPublicationChunkRollsBackAllTagRelationshipsAndRevisionPointers));
        await SeedLegacyBiologyCavesAsync(
            database, MultiBatchCount, accountId, stateId, countyId, sourceId, destinationId);

        await using (var rollout = database.CreateDbContext("rollout", accountId))
        {
            var result = await new CaveRevisionRolloutRepository(rollout).InitializeAsync();
            Assert.Equal(MultiBatchCount, result.InitializedCaveCount);
        }

        Dictionary<string, string?> baselinePointers;
        await using (var baseline = database.CreateDbContext("baseline-pointers", accountId))
            baselinePointers = await baseline.Caves.IgnoreQueryFilters().AsNoTracking()
                .Where(cave => cave.AccountId == accountId)
                .OrderBy(cave => cave.Id)
                .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId);

        var fault = new FailOnSecondSaveChangesInterceptor();
        await using (var merge = database.CreateDbContext("manager", accountId, fault))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => TagRepository(merge).MergeAsync([sourceId], destinationId));
            Assert.Contains("second revision publication chunk", error.Message, StringComparison.Ordinal);
            Assert.Equal(2, fault.SavingChangesCalls);
        }

        await using var verify = database.CreateDbContext("verify", accountId);
        Assert.Equal(MultiBatchCount,
            await verify.BiologyTags.AsNoTracking().CountAsync(tag => tag.TagTypeId == sourceId));
        Assert.Equal(0,
            await verify.BiologyTags.AsNoTracking().CountAsync(tag => tag.TagTypeId == destinationId));
        Assert.Equal(MultiBatchCount, await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == accountId));
        Assert.Equal(0, await verify.CaveRevisions.IgnoreQueryFilters().AsNoTracking()
            .CountAsync(revision => revision.AccountId == accountId &&
                                    revision.Source == CaveRevisionSource.ManagerEdit));
        var finalPointers = await verify.Caves.IgnoreQueryFilters().AsNoTracking()
            .Where(cave => cave.AccountId == accountId)
            .OrderBy(cave => cave.Id)
            .ToDictionaryAsync(cave => cave.Id, cave => cave.CurrentRevisionId);
        Assert.Equal(baselinePointers.Count, finalPointers.Count);
        foreach (var (caveId, revisionId) in baselinePointers)
            Assert.Equal(revisionId, finalPointers[caveId]);
    }

    private static TagTypeAdministrationRepository TagRepository(
        Planarian.Model.Database.PlanarianDbContext db)
    {
        var snapshots = new CavePublishedSnapshotRepository(db, db.RequestUser);
        return new TagTypeAdministrationRepository(db, db.RequestUser,
            new TagReferenceLockRepository(db, db.RequestUser), snapshots,
            new CaveBulkRevisionRepository(db, db.RequestUser));
    }

    private static async Task SeedLegacyBiologyCavesAsync(PostgresTestDatabase database, int caveCount,
        string accountId, string stateId, string countyId, string sourceId, string destinationId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn")
            values(@state_id,'Tennessee','TN',now());

            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
            values(@account_id,'Tag Merge Scale','-',false,true,now());

            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn")
            values(@county_id,@account_id,@state_id,'A01','Scale County',now());

            insert into "TagTypes"("Id","Name","Key","AccountId","IsDefault","CreatedOn")
            values
                (@source_id,'Source Biology','Biology',@account_id,false,now()),
                (@destination_id,'Destination Biology','Biology',@account_id,false,now());

            insert into "Caves"(
                "Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn")
            select 'c' || lpad(series::text, 9, '0'), @account_id, @state_id, @county_id,
                   'Scale Cave ' || series, '[]', series, false, now()
            from generate_series(1, @cave_count) as series;

            insert into "BiologyTags"("Id","CaveId","TagTypeId","CreatedOn")
            select 'b' || lpad(series::text, 9, '0'), 'c' || lpad(series::text, 9, '0'), @source_id, now()
            from generate_series(1, @cave_count) as series;
            """;
        command.Parameters.AddWithValue("state_id", stateId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("county_id", countyId);
        command.Parameters.AddWithValue("source_id", sourceId);
        command.Parameters.AddWithValue("destination_id", destinationId);
        command.Parameters.AddWithValue("cave_count", caveCount);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SeedLegacyCountyCavesAsync(PostgresTestDatabase database, int caveCount,
        string accountId, string stateId, string sourceCountyId, string destinationCountyId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            insert into "States"("Id","Name","Abbreviation","CreatedOn")
            values(@state_id,'Tennessee','TN',now());

            insert into "Accounts"("Id","Name","CountyIdDelimiter","DefaultViewAccessAllCaves","ExportEnabled","CreatedOn")
            values(@account_id,'County Merge Scale','-',false,true,now());

            insert into "Counties"("Id","AccountId","StateId","DisplayId","Name","CreatedOn")
            values
                (@source_county_id,@account_id,@state_id,'A01','Source County',now()),
                (@destination_county_id,@account_id,@state_id,'A99','Destination County',now());

            insert into "Caves"(
                "Id","AccountId","StateId","CountyId","Name","AlternateNames","CountyNumber","IsArchived","CreatedOn")
            select 'c' || lpad(series::text, 9, '0'), @account_id, @state_id, @source_county_id,
                   'Scale Cave ' || series, '[]', series, false, now()
            from generate_series(1, @cave_count) as series;
            """;
        command.Parameters.AddWithValue("state_id", stateId);
        command.Parameters.AddWithValue("account_id", accountId);
        command.Parameters.AddWithValue("source_county_id", sourceCountyId);
        command.Parameters.AddWithValue("destination_county_id", destinationCountyId);
        command.Parameters.AddWithValue("cave_count", caveCount);
        await command.ExecuteNonQueryAsync();
    }
}

internal sealed class FailOnSecondSaveChangesInterceptor : SaveChangesInterceptor
{
    private int _calls;
    public int SavingChangesCalls => Volatile.Read(ref _calls);

    private void Inspect()
    {
        if (Interlocked.Increment(ref _calls) == 2)
            throw new InvalidOperationException(
                "Injected deterministic failure at the second revision publication chunk.");
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Inspect();
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Inspect();
        return ValueTask.FromResult(result);
    }
}
