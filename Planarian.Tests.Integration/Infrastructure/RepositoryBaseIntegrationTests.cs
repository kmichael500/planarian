using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Shared.Base;
using Xunit;

namespace Planarian.Tests;

public sealed class RepositoryBaseIntegrationTests(PostgresTestServer fixture) : IClassFixture<PostgresTestServer>
{
    [Fact]
    public async Task RequestUserConstructionAloneActivatesAccountQueryFilters()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(RequestUserConstructionAloneActivatesAccountQueryFilters));
        var first = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'h');
        var second = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'i');
        var firstBatch = NewBatch(first.AccountId, "first-account");
        var secondBatch = NewBatch(second.AccountId, "second-account");

        await using (var seed = database.CreateDbContext("seed-first", first.AccountId))
        {
            seed.CaveImportBatches.Add(firstBatch);
            await seed.SaveChangesAsync();
        }
        await using (var seed = database.CreateDbContext("seed-second", second.AccountId))
        {
            seed.CaveImportBatches.Add(secondBatch);
            await seed.SaveChangesAsync();
        }

        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(database.ConnectionString, provider => provider.UseNetTopologySuite())
            .Options;
        await using var db = new PlanarianDbContext(options);
        var user = new RequestUser(db) { Id = "request-user", AccountId = first.AccountId };

        Assert.Same(user, db.RequestUser);
        var visible = await db.CaveImportBatches.Select(row => row.Id).ToListAsync();
        Assert.Equal([firstBatch.Id], visible);
    }

    [Fact]
    public async Task RequestUserConstructionAloneSuppliesSaveChangesAuditContext()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(RequestUserConstructionAloneSuppliesSaveChangesAuditContext));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'j');
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(database.ConnectionString, provider => provider.UseNetTopologySuite())
            .Options;
        await using var db = new PlanarianDbContext(options);
        var user = new RequestUser(db) { Id = "user-j", AccountId = account.AccountId };
        var batch = NewBatch(account.AccountId, "request-user-audit");

        db.CaveImportBatches.Add(batch);
        await db.SaveChangesAsync();

        Assert.Same(user, db.RequestUser);
        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var persisted = await verify.CaveImportBatches.SingleAsync(row => row.Id == batch.Id);
        Assert.Equal("user-j", persisted.CreatedByUserId);
    }

    [Fact]
    public async Task OwnedTransactionCommitsMultipleSaveChangesAsOneUnit()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(OwnedTransactionCommitsMultipleSaveChangesAsOneUnit));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'a');
        await using var db = database.CreateDbContext("owner", account.AccountId);
        var repository = new TestRepository(db, db.RequestUser);
        var first = NewBatch(account.AccountId, "first");
        var second = NewBatch(account.AccountId, "second");

        await repository.RunOwnedAsync(async token =>
        {
            repository.Add(first);
            await repository.SaveChangesAsync(token);
            repository.Add(second);
            await repository.SaveChangesAsync(token);
        });

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.Equal(2, await verify.CaveImportBatches.CountAsync(row => row.Id == first.Id || row.Id == second.Id));
    }

    [Fact]
    public async Task OwnedTransactionRollsBackAnEarlierSuccessfulSaveWhenLaterWorkFails()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(OwnedTransactionRollsBackAnEarlierSuccessfulSaveWhenLaterWorkFails));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'b');
        await using var db = database.CreateDbContext("owner", account.AccountId);
        var repository = new TestRepository(db, db.RequestUser);
        var persistedBeforeFailure = NewBatch(account.AccountId, "rollback");

        await Assert.ThrowsAsync<TestTransactionException>(() => repository.RunOwnedAsync(async token =>
        {
            repository.Add(persistedBeforeFailure);
            await repository.SaveChangesAsync(token);
            throw new TestTransactionException();
        }));

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.CaveImportBatches.AnyAsync(row => row.Id == persistedBeforeFailure.Id));
    }

    [Fact]
    public async Task JoiningAnExistingTransactionDoesNotCommitTheCallersTransaction()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(JoiningAnExistingTransactionDoesNotCommitTheCallersTransaction));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'c');
        await using var db = database.CreateDbContext("owner", account.AccountId);
        var repository = new TestRepository(db, db.RequestUser);
        var batch = NewBatch(account.AccountId, "joined");
        await using var outer = await db.Database.BeginTransactionAsync();

        await repository.RunJoinedAsync(async token =>
        {
            repository.Add(batch);
            await repository.SaveChangesAsync(token);
        });

        Assert.Same(outer, db.Database.CurrentTransaction);
        Assert.True(await db.CaveImportBatches.AnyAsync(row => row.Id == batch.Id));
        await outer.RollbackAsync();

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.CaveImportBatches.AnyAsync(row => row.Id == batch.Id));
    }

    [Fact]
    public async Task JoinedTransactionFailureLeavesRollbackOwnershipWithTheCaller()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(JoinedTransactionFailureLeavesRollbackOwnershipWithTheCaller));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'k');
        await using var db = database.CreateDbContext("owner", account.AccountId);
        var repository = new TestRepository(db, db.RequestUser);
        var batch = NewBatch(account.AccountId, "joined-failure");
        await using var outer = await db.Database.BeginTransactionAsync();

        await Assert.ThrowsAsync<TestTransactionException>(() => repository.RunJoinedAsync(async token =>
        {
            repository.Add(batch);
            await repository.SaveChangesAsync(token);
            throw new TestTransactionException();
        }));

        Assert.Same(outer, db.Database.CurrentTransaction);
        Assert.True(await db.CaveImportBatches.AnyAsync(row => row.Id == batch.Id));
        await outer.RollbackAsync();

        await using var verify = database.CreateDbContext("verify", account.AccountId);
        Assert.False(await verify.CaveImportBatches.AnyAsync(row => row.Id == batch.Id));
    }

    [Fact]
    public async Task OwnedTransactionRejectsAnExistingTransactionInsteadOfSilentlyChangingOwnership()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(OwnedTransactionRejectsAnExistingTransactionInsteadOfSilentlyChangingOwnership));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'd');
        await using var db = database.CreateDbContext("owner", account.AccountId);
        var repository = new TestRepository(db, db.RequestUser);
        await using var outer = await db.Database.BeginTransactionAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.RunOwnedAsync(_ => Task.CompletedTask));

        Assert.Contains("requires ownership", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Same(outer, db.Database.CurrentTransaction);
        await outer.RollbackAsync();
    }

    [Fact]
    public async Task BatchedInsertUsesBoundedSaveChangesPreservesAuditingAndLeavesNoTrackedBatchState()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(BatchedInsertUsesBoundedSaveChangesPreservesAuditingAndLeavesNoTrackedBatchState));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'e');
        await using var db = database.CreateDbContext("owner", account.AccountId);
        var repository = new TestRepository(db, db.RequestUser);
        var batches = Enumerable.Range(0, 5).Select(index => NewBatch(account.AccountId, $"batch-{index}"))
            .ToList();
        var saveCount = 0;
        db.SavingChanges += (_, _) => saveCount++;

        await repository.AddBatchedAndClearAsync(batches, batchSize: 2);

        Assert.Equal(3, saveCount);
        Assert.Empty(db.ChangeTracker.Entries());
        await using var verify = database.CreateDbContext("verify", account.AccountId);
        var persisted = await verify.CaveImportBatches.Where(row => batches.Select(item => item.Id).Contains(row.Id))
            .OrderBy(row => row.SourceFileName).ToListAsync();
        Assert.Equal(5, persisted.Count);
        Assert.All(persisted, row => Assert.Equal(db.RequestUser.Id, row.CreatedByUserId));
    }

    [Fact]
    public async Task BatchedInsertRefusesToClearUnrelatedTrackedState()
    {
        await using var database = await fixture.CreateDatabaseAsync(nameof(BatchedInsertRefusesToClearUnrelatedTrackedState));
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'f');
        await using var db = database.CreateDbContext("owner", account.AccountId);
        var repository = new TestRepository(db, db.RequestUser);
        var unrelated = NewBatch(account.AccountId, "unrelated");
        repository.Add(unrelated);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.AddBatchedAndClearAsync([NewBatch(account.AccountId, "new")], 1));

        Assert.Contains("tracked", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EntityState.Added, db.Entry(unrelated).State);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task BatchedInsertRejectsInvalidBatchSizes(int batchSize)
    {
        await using var database = await fixture.CreateDatabaseAsync($"{nameof(BatchedInsertRejectsInvalidBatchSizes)}_{batchSize}");
        var account = await AccountTestDataFactory.CreateAccountWithCountyAsync(database, 'g');
        await using var db = database.CreateDbContext("owner", account.AccountId);
        var repository = new TestRepository(db, db.RequestUser);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.AddBatchedAndClearAsync([NewBatch(account.AccountId, "invalid")], batchSize));
    }

    private static CaveImportBatch NewBatch(string accountId, string name) => new()
    {
        Id = IdGenerator.Generate(),
        AccountId = accountId,
        SourceFileName = name,
        Kind = CaveImportKind.CaveCsv
    };

    private sealed class TestRepository : RepositoryBase
    {
        public TestRepository(PlanarianDbContext db, RequestUser user) : base(db, user)
        {
        }

        public Task RunOwnedAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            RunInOwnedTransactionAsync(work, cancellationToken);

        public Task RunJoinedAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken = default) =>
            RunInTransactionAsync(work, cancellationToken);

        public Task AddBatchedAndClearAsync(IEnumerable<CaveImportBatch> entities, int batchSize,
            CancellationToken cancellationToken = default) =>
            AddRangeBatchedAndClearAsync(entities, batchSize, cancellationToken);
    }

    private sealed class TestTransactionException : Exception
    {
    }
}
