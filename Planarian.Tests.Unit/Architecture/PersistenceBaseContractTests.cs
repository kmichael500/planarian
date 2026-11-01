using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Repositories;
using Planarian.Shared.Base;
using Xunit;

namespace Planarian.Tests;

public sealed class PersistenceBaseContractTests
{
    [Fact]
    public void RequestUserConstructionBindsTheRequestDbContext()
    {
        using var db = CreateContext();

        var user = new RequestUser(db);

        Assert.Same(user, db.RequestUser);
    }

    [Fact]
    public void RequestUserConstructionRejectsRebindingTheSameDbContext()
    {
        using var db = CreateContext();
        var first = new RequestUser(db);

        var exception = Assert.Throws<InvalidOperationException>(() => new RequestUser(db));

        Assert.Contains("one RequestUser", exception.Message, StringComparison.Ordinal);
        Assert.Same(first, db.RequestUser);
    }

    [Fact]
    public void DbContextRequestUserBindingIsNotPubliclyMutable()
    {
        var property = typeof(PlanarianDbContextBase).GetProperty(nameof(PlanarianDbContextBase.RequestUser));

        Assert.NotNull(property);
        Assert.NotNull(property!.SetMethod);
        Assert.False(property.SetMethod!.IsPublic);
        Assert.True(property.SetMethod.IsAssembly);
    }

    [Fact]
    public void RepositoryBaseRejectsRequestUserFromAnotherDbContext()
    {
        using var repositoryDb = CreateContext();
        using var userDb = CreateContext();
        var user = new RequestUser(userDb) { Id = "user-a", AccountId = "account-a" };

        var exception = Assert.Throws<InvalidOperationException>(() => new TestAccountScopedRepository(repositoryDb, user));

        Assert.Contains("same DbContext", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CaveRepositoryConstructionDoesNotEagerlyRequireAccountScope()
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { Id = "user-a", AccountId = null };

        var repository = new CaveRepository(db, user);

        Assert.NotNull(repository);
    }

    [Fact]
    public async Task CaveRepositoryAuthoringOperationRequiresAccountScopeLazily()
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { Id = "user-a", AccountId = null };
        var repository = new CaveRepository(db, user);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.AttachAuthoringStagedFilesAsync("cave-a", Array.Empty<string>()));
    }

    [Fact]
    public void GenericAccountScopedRepositoryBaseCapturesImmutableRequestScope()
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { Id = "user-a", AccountId = "account-a" };

        var repository = new TestGenericAccountScopedRepository(db, user);

        Assert.Equal("account-a", repository.AccountIdValue);
        Assert.Equal("user-a", repository.UserIdValue);
        user.AccountId = "account-b";
        user.Id = "user-b";
        Assert.Equal("account-a", repository.AccountIdValue);
        Assert.Equal("user-a", repository.UserIdValue);
    }

    [Fact]
    public void AccountScopedRepositoryBaseCapturesImmutableRequestScope()
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { Id = "user-a", AccountId = "account-a" };

        var repository = new TestAccountScopedRepository(db, user);

        Assert.Equal("account-a", repository.AccountIdValue);
        Assert.Equal("user-a", repository.UserIdValue);
        user.AccountId = "account-b";
        user.Id = "user-b";
        Assert.Equal("account-a", repository.AccountIdValue);
        Assert.Equal("user-a", repository.UserIdValue);
    }

    [Theory]
    [InlineData(null, "user-a")]
    [InlineData("", "user-a")]
    [InlineData("account-a", null)]
    [InlineData("account-a", "")]
    public void AccountScopedRepositoryBaseRejectsIncompleteRequestScope(string? accountId, string? userId)
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { AccountId = accountId, Id = userId! };

        Assert.Throws<InvalidOperationException>(() => new TestAccountScopedRepository(db, user));
    }

    [Theory]
    [InlineData(null, "user-a")]
    [InlineData("", "user-a")]
    [InlineData("account-a", null)]
    [InlineData("account-a", "")]
    public void AccountServiceBaseRejectsIncompleteRequestScope(string? accountId, string? userId)
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { AccountId = accountId, Id = userId! };

        Assert.Throws<InvalidOperationException>(() => new TestAccountService(user));
    }

    [Fact]
    public void RequestScopedEfRepositoriesUseTheSharedRepositoryBase()
    {
        var repositoryTypes = typeof(RepositoryBase).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Where(HasRequestScopedDbContextConstructor)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(repositoryTypes);
        var violations = repositoryTypes.Where(type => !InheritsRepositoryBase(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Request-scoped EF repositories must derive from the shared repository base so context/unit-of-work invariants cannot be skipped: {string.Join(", ", violations)}");
    }

    private static bool InheritsRepositoryBase(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current == typeof(RepositoryBase)) return true;
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(RepositoryBase<>)) return true;
        }

        return false;
    }

    private static bool HasRequestScopedDbContextConstructor(Type type) =>
        type.GetConstructors().Any(constructor =>
        {
            var parameters = constructor.GetParameters().Select(parameter => parameter.ParameterType).ToList();
            return parameters.Contains(typeof(PlanarianDbContext)) && parameters.Contains(typeof(RequestUser));
        });

    private static PlanarianDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<PlanarianDbContext>().Options);

    private sealed class TestAccountScopedRepository : AccountScopedRepositoryBase
    {
        public TestAccountScopedRepository(PlanarianDbContext db, RequestUser user) : base(db, user)
        {
        }

        public string AccountIdValue => AccountId;
        public string UserIdValue => UserId;
    }

    private sealed class TestGenericAccountScopedRepository
        : AccountScopedRepositoryBase<PlanarianDbContext>
    {
        public TestGenericAccountScopedRepository(PlanarianDbContext db, RequestUser user) : base(db, user)
        {
        }

        public string AccountIdValue => AccountId;
        public string UserIdValue => UserId;
    }

    private sealed class TestAccountService : AccountServiceBase
    {
        public TestAccountService(RequestUser user) : base(user)
        {
        }
    }
}
