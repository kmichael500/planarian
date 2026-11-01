using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Caves.Repositories;
using Planarian.Modules.Caves.Revisions;
using Planarian.Modules.Files.Persistence;
using Planarian.Shared.Base;
using Xunit;

namespace Planarian.Tests;

public sealed class DataAccessBaseContractTests
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
    public void DataAccessBaseRejectsRequestUserFromAnotherDbContext()
    {
        using var dataAccessDb = CreateContext();
        using var userDb = CreateContext();
        var user = new RequestUser(userDb) { Id = "user-a", AccountId = "account-a" };

        var exception = Assert.Throws<InvalidOperationException>(() => new TestAccountScopedDataAccess(dataAccessDb, user));

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

    [Fact]
    public void GenericAccountScopedDataAccessBaseCapturesImmutableRequestScope()
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { Id = "user-a", AccountId = "account-a" };

        var dataAccess = new TestGenericAccountScopedDataAccess(db, user);

        Assert.Equal("account-a", dataAccess.AccountIdValue);
        Assert.Equal("user-a", dataAccess.UserIdValue);
        user.AccountId = "account-b";
        user.Id = "user-b";
        Assert.Equal("account-a", dataAccess.AccountIdValue);
        Assert.Equal("user-a", dataAccess.UserIdValue);
    }

    [Fact]
    public void AccountScopedDataAccessBaseCapturesImmutableRequestScope()
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { Id = "user-a", AccountId = "account-a" };

        var dataAccess = new TestAccountScopedDataAccess(db, user);

        Assert.Equal("account-a", dataAccess.AccountIdValue);
        Assert.Equal("user-a", dataAccess.UserIdValue);
        user.AccountId = "account-b";
        user.Id = "user-b";
        Assert.Equal("account-a", dataAccess.AccountIdValue);
        Assert.Equal("user-a", dataAccess.UserIdValue);
    }

    [Theory]
    [InlineData(null, "user-a")]
    [InlineData("", "user-a")]
    [InlineData("account-a", null)]
    [InlineData("account-a", "")]
    public void AccountScopedDataAccessBaseRejectsIncompleteRequestScope(string? accountId, string? userId)
    {
        using var db = CreateContext();
        var user = new RequestUser(db) { AccountId = accountId, Id = userId! };

        Assert.Throws<InvalidOperationException>(() => new TestAccountScopedDataAccess(db, user));
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
    public void RequestScopedEfDataAccessUsesTheSharedDataAccessBase()
    {
        var dataAccessTypes = typeof(DataAccessBase).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(HasRequestScopedDbContextConstructor)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(dataAccessTypes);
        var violations = dataAccessTypes.Where(type => !InheritsDataAccessBase(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Request-scoped EF data-access types must derive from the shared DataAccessBase so context/unit-of-work invariants cannot be skipped: {string.Join(", ", violations)}");
    }

    [Fact]
    public void RepositoryNamedRequestScopedTypesUseRepositoryBase()
    {
        var repositoryTypes = typeof(DataAccessBase).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract &&
                           type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .Where(HasRequestScopedDbContextConstructor)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(repositoryTypes);
        var violations = repositoryTypes.Where(type => !InheritsRepositoryBase(type))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Request-scoped types named Repository must derive from RepositoryBase: {string.Join(", ", violations)}");
    }

    [Fact]
    public void NarrowDataAccessCapabilityNamesDoNotUseRepositoryBase()
    {
        string[] suffixes = ["Reader", "Store", "Locker", "Publisher", "Persistence"];
        var capabilityTypes = typeof(DataAccessBase).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract)
            .Where(type => suffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal)))
            .Where(HasRequestScopedDbContextConstructor)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(capabilityTypes);
        var violations = capabilityTypes.Where(InheritsRepositoryBase)
            .Select(type => type.FullName)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Narrow data-access capabilities must use DataAccessBase rather than RepositoryBase: {string.Join(", ", violations)}");
    }

    [Fact]
    public void RepositoryConstructorsDoNotDependOnServices()
    {
        var repositoryTypes = typeof(DataAccessBase).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract &&
                           type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .ToList();
        var violations = repositoryTypes
            .SelectMany(type => type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => parameter.ParameterType.Name.EndsWith("Service", StringComparison.Ordinal))
                .Select(parameter => $"{type.FullName} -> {parameter.ParameterType.FullName}"))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(violations.Count == 0,
            $"Repositories must not depend on service-layer types: {string.Join(", ", violations)}");
    }

    [Fact]
    public void DataAccessConstructorDependenciesAreAcyclic()
    {
        var dataAccessTypes = typeof(DataAccessBase).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && InheritsDataAccessBase(type))
            .ToHashSet();
        var graph = dataAccessTypes.ToDictionary(
            type => type,
            type => type.GetConstructors()
                .SelectMany(constructor => constructor.GetParameters())
                .Select(parameter => parameter.ParameterType)
                .Where(dataAccessTypes.Contains)
                .Distinct()
                .ToList());

        var visiting = new HashSet<Type>();
        var visited = new HashSet<Type>();
        var path = new List<Type>();

        foreach (var type in dataAccessTypes.OrderBy(type => type.FullName, StringComparer.Ordinal))
            Visit(type);

        return;

        void Visit(Type type)
        {
            if (visited.Contains(type)) return;
            if (!visiting.Add(type))
            {
                var cycleStart = path.IndexOf(type);
                var cycle = path.Skip(cycleStart).Append(type).Select(item => item.Name);
                Assert.Fail($"Data-access constructor dependency cycle detected: {string.Join(" -> ", cycle)}");
            }

            path.Add(type);
            foreach (var dependency in graph[type]) Visit(dependency);
            path.RemoveAt(path.Count - 1);
            visiting.Remove(type);
            visited.Add(type);
        }
    }

    [Fact]
    public void CaveChangeRequestRepositoryMakesAuthoringStoreAPublicDependency()
    {
        var publicConstructors = typeof(CaveChangeRequestRepository).GetConstructors();

        var constructor = Assert.Single(publicConstructors);
        Assert.Contains(constructor.GetParameters(), parameter => parameter.ParameterType == typeof(AuthoringFileStore));
    }

    [Fact]
    public void CavePublishedSnapshotReaderRejectsRequestUserFromAnotherDbContext()
    {
        using var readerDb = CreateContext();
        using var userDb = CreateContext();
        var user = new RequestUser(userDb) { Id = "user-a", AccountId = "account-a" };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new CavePublishedSnapshotReader(readerDb, user));

        Assert.Contains("same DbContext", exception.Message, StringComparison.Ordinal);
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

    private static bool InheritsDataAccessBase(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current == typeof(DataAccessBase)) return true;
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(DataAccessBase<>)) return true;
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

    private sealed class TestAccountScopedDataAccess : AccountScopedDataAccessBase
    {
        public TestAccountScopedDataAccess(PlanarianDbContext db, RequestUser user) : base(db, user)
        {
        }

        public string AccountIdValue => AccountId;
        public string UserIdValue => UserId;
    }

    private sealed class TestGenericAccountScopedDataAccess
        : AccountScopedDataAccessBase<PlanarianDbContext>
    {
        public TestGenericAccountScopedDataAccess(PlanarianDbContext db, RequestUser user) : base(db, user)
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
