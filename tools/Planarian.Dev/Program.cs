using System.Security.Cryptography;
using System.Text;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Generators;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;

const string connectionVariable = "PLANARIAN_DEV_CONNECTION_STRING";
const string storageVariable = "PLANARIAN_DEV_BLOB_CONNECTION_STRING";
const string defaultPassword = "planarian";

if (args.Length != 2 || args[0] is not ("seed" or "reset-storage"))
{
    Console.Error.WriteLine("Usage: Planarian.Dev <seed|reset-storage> <generic|e2e>");
    return 2;
}

var dataset = DatasetConfig.Parse(args[1]);
if (args[0] == "reset-storage")
{
    await ResetStorage(dataset);
    return 0;
}

var connectionString = RequireEnvironment(connectionVariable);
await ApplyMigrations(connectionString);
await SeedDatabase(connectionString, dataset);
return 0;
static async Task ApplyMigrations(string connectionString)
{
    var options = new DbContextOptionsBuilder<PlanarianDbContext>()
        .UseNpgsql(connectionString, postgres =>
        {
            postgres.MigrationsAssembly("Planarian.Migrations");
            postgres.UseNetTopologySuite();
        })
        .Options;

    await using var db = new PlanarianDbContext(options);
    await db.Database.MigrateAsync();
}

static async Task SeedDatabase(string connectionString, DatasetConfig dataset)
{
    var options = new DbContextOptionsBuilder<PlanarianDbContext>()
        .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
        .Options;

    await using var db = new DevelopmentSeedDbContext(options);
    await new DataGenerator(db).AddOrUpdateDefaultData();
    await EnsureDatasetBaseline(db, dataset);
    await VerifyBaseline(db, dataset);
}
static async Task EnsureDatasetBaseline(PlanarianDbContext db, DatasetConfig dataset)
{
    var createdOn = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    if (!await db.Accounts.AnyAsync(account => account.Id == dataset.AccountId))
    {
        db.Accounts.Add(new Account
        {
            Id = dataset.AccountId,
            Name = "Planarian Local Development",
            CountyIdDelimiter = "-",
            DefaultViewAccessAllCaves = true,
            ExportEnabled = true,
            CreatedOn = createdOn
        });
    }
    var accountStateId = StableId($"{dataset.Name}:account-state:tn");
    if (!await db.AccountStates.AnyAsync(row => row.Id == accountStateId))
    {
        db.AccountStates.Add(new AccountState
        {
            Id = accountStateId,
            AccountId = dataset.AccountId,
            StateId = "X6Y7Z8a9B1",
            CreatedOn = createdOn
        });
    }

    if (!await db.Counties.AnyAsync(row => row.Id == dataset.CountyId))
    {
        db.Counties.Add(new County
        {
            Id = dataset.CountyId,
            AccountId = dataset.AccountId,
            StateId = "X6Y7Z8a9B1",
            Name = "Demo County",
            DisplayId = "DEMO",
            CreatedOn = createdOn
        });
    }
    var existingFeatureKeys = (await db.FeatureSettings
        .Where(setting => setting.AccountId == dataset.AccountId)
        .Select(setting => setting.Key)
        .ToListAsync())
        .ToHashSet();
    foreach (var key in Enum.GetValues<FeatureKey>())
    {
        if (existingFeatureKeys.Contains(key)) continue;

        db.FeatureSettings.Add(new FeatureSetting
        {
            Id = StableId($"{dataset.Name}:feature:{key}"),
            AccountId = dataset.AccountId,
            Key = key,
            IsEnabled = key != FeatureKey.EnabledFieldCaveGeologicAgeTags,
            IsDefault = false,
            CreatedOn = createdOn
        });
    }

    var users = new[]
    {
        (dataset.AdminUserId, "Local", "Admin", "admin@planarian.local"),
        (dataset.ManagerUserId, "Local", "Manager", "manager@planarian.local"),
        (dataset.ViewerUserId, "Local", "Viewer", "viewer@planarian.local")
    };

    foreach (var (id, firstName, lastName, email) in users)
    {
        if (!await db.Users.AnyAsync(user => user.Id == id))
        {
            db.Users.Add(new User(firstName, lastName, email)
            {
                Id = id,
                HashedPassword = PasswordService.Hash(defaultPassword),
                EmailConfirmedOn = createdOn,
                CreatedOn = createdOn
            });
        }

        var accountUserId = StableId($"{dataset.Name}:account-user:{id}");
        if (!await db.AccountUsers.AnyAsync(row => row.Id == accountUserId))
        {
            db.AccountUsers.Add(new AccountUser
            {
                Id = accountUserId,
                AccountId = dataset.AccountId,
                UserId = id,
                InvitationAcceptedOn = createdOn,
                CreatedOn = createdOn
            });
        }
    }
    var permissions = await db.Permissions
        .Where(permission => permission.Key == PermissionKey.Admin ||
                             permission.Key == PermissionKey.Manager ||
                             permission.Key == PermissionKey.View)
        .ToDictionaryAsync(permission => permission.Key);

    var adminPermissionId = StableId($"{dataset.Name}:admin-permission");
    if (!await db.UserPermissions.AnyAsync(row => row.Id == adminPermissionId))
    {
        db.UserPermissions.Add(new UserPermission
        {
            Id = adminPermissionId,
            AccountId = dataset.AccountId,
            UserId = dataset.AdminUserId,
            PermissionId = permissions[PermissionKey.Admin].Id,
            CreatedOn = createdOn
        });
    }

    var broadPermissions = new[]
    {
        BroadCavePermission(dataset, dataset.ManagerUserId, permissions[PermissionKey.Manager].Id, createdOn),
        BroadCavePermission(dataset, dataset.ViewerUserId, permissions[PermissionKey.View].Id, createdOn)
    };
    foreach (var permission in broadPermissions)
    {
        if (!await db.CavePermissions.AnyAsync(row => row.Id == permission.Id))
            db.CavePermissions.Add(permission);
    }

    foreach (var tag in DatasetTags(dataset, createdOn))
    {
        if (!await db.TagTypes.AnyAsync(row => row.Id == tag.Id))
            db.TagTypes.Add(tag);
    }

    await db.SaveChangesAsync();
}

static CavePermission BroadCavePermission(
    DatasetConfig dataset, string userId, string permissionId, DateTime createdOn) => new()
{
    Id = StableId($"{dataset.Name}:cave-permission:{userId}:{permissionId}"),
    AccountId = dataset.AccountId,
    UserId = userId,
    PermissionId = permissionId,
    CreatedOn = createdOn
};
static IEnumerable<TagType> DatasetTags(DatasetConfig dataset, DateTime createdOn)
{
    yield return Tag(dataset, "geology:bangor", "Bangor Limestone", TagTypeKeyConstant.Geology, createdOn);
    yield return Tag(dataset, "geology:monteagle", "Monteagle Limestone", TagTypeKeyConstant.Geology, createdOn);
    yield return Tag(dataset, "people:surveyor", "Dev Surveyor", TagTypeKeyConstant.People, createdOn);
    yield return Tag(dataset, "other:training", "Training Data", TagTypeKeyConstant.CaveOther, createdOn);
}

static TagType Tag(DatasetConfig dataset, string identity, string name, string key, DateTime createdOn) => new(name, key)
{
    Id = StableId($"{dataset.Name}:tag:{identity}"),
    AccountId = dataset.AccountId,
    IsDefault = false,
    CreatedOn = createdOn
};

static async Task VerifyBaseline(PlanarianDbContext db, DatasetConfig dataset)
{
    var featureKeys = Enum.GetValues<FeatureKey>();
    var expectedFeatureIds = featureKeys
        .Select(key => StableId($"{dataset.Name}:feature:{key}"))
        .ToArray();
    var expectedUserIds = new[] { dataset.AdminUserId, dataset.ManagerUserId, dataset.ViewerUserId };
    var expectedTagIds = DatasetTags(dataset, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
        .Select(tag => tag.Id)
        .ToArray();
    var expectedAccountStateId = StableId($"{dataset.Name}:account-state:tn");
    var expectedAdminPermissionId = StableId($"{dataset.Name}:admin-permission");

    var stateCount = await db.States.CountAsync();
    var permissionCount = await db.Permissions.CountAsync();
    var userCount = await db.AccountUsers.CountAsync(row =>
        row.AccountId == dataset.AccountId && expectedUserIds.Contains(row.UserId));
    var customTagCount = await db.TagTypes.CountAsync(tag =>
        tag.AccountId == dataset.AccountId && expectedTagIds.Contains(tag.Id));
    var actualFeatureCount = await db.FeatureSettings.CountAsync(setting =>
        setting.AccountId == dataset.AccountId && expectedFeatureIds.Contains(setting.Id));
    var hasAccountState = await db.AccountStates.AnyAsync(row =>
        row.Id == expectedAccountStateId && row.AccountId == dataset.AccountId);
    var hasCounty = await db.Counties.AnyAsync(row =>
        row.Id == dataset.CountyId && row.AccountId == dataset.AccountId);
    var hasAdminPermission = await db.UserPermissions.AnyAsync(row =>
        row.Id == expectedAdminPermissionId &&
        row.AccountId == dataset.AccountId && row.UserId == dataset.AdminUserId);
    var cavePermissionIds = await db.Permissions
        .Where(permission => permission.Key == PermissionKey.Manager || permission.Key == PermissionKey.View)
        .ToDictionaryAsync(permission => permission.Key, permission => permission.Id);
    var expectedBroadPermissionIds = new[]
    {
        StableId($"{dataset.Name}:cave-permission:{dataset.ManagerUserId}:{cavePermissionIds[PermissionKey.Manager]}"),
        StableId($"{dataset.Name}:cave-permission:{dataset.ViewerUserId}:{cavePermissionIds[PermissionKey.View]}")
    };
    var broadCavePermissionCount = await db.CavePermissions.CountAsync(row =>
        row.AccountId == dataset.AccountId && expectedBroadPermissionIds.Contains(row.Id));

    if (stateCount != 50 || permissionCount < 4 || userCount != expectedUserIds.Length ||
        customTagCount != expectedTagIds.Length || actualFeatureCount != featureKeys.Length ||
        !hasAccountState || !hasCounty || !hasAdminPermission ||
        broadCavePermissionCount != expectedBroadPermissionIds.Length)
    {
        throw new InvalidOperationException(
            $"Seed verification failed for '{dataset.Name}'. Run './dev reset {dataset.Name}' to rebuild the local dataset. " +
            $"states={stateCount}, permissions={permissionCount}, users={userCount}/{expectedUserIds.Length}, " +
            $"customTags={customTagCount}/{expectedTagIds.Length}, features={actualFeatureCount}/{featureKeys.Length}, " +
            $"accountState={hasAccountState}, county={hasCounty}, adminPermission={hasAdminPermission}, " +
            $"broadCavePermissions={broadCavePermissionCount}/2."
        );
    }

    Console.WriteLine($"Dataset '{dataset.Name}' seeded and verified.");
}
static async Task ResetStorage(DatasetConfig dataset)
{
    var connectionString = RequireEnvironment(connectionVariable);
    var options = new DbContextOptionsBuilder<PlanarianDbContext>()
        .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
        .Options;

    await using var db = new PlanarianDbContext(options);
    var accountIds = await db.Accounts.AsNoTracking().Select(account => account.Id).ToListAsync();

    var containerNames = accountIds
        .Select(accountId => $"account-{accountId.ToLowerInvariant()}")
        .Append(dataset.BlobContainer)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var service = new BlobServiceClient(RequireEnvironment(storageVariable));
    foreach (var name in containerNames)
    {
        await service.GetBlobContainerClient(name).DeleteIfExistsAsync();
    }

    Console.WriteLine($"Reset {containerNames.Count} local blob container(s) for '{dataset.Name}'.");
}

static string RequireEnvironment(string name) =>
    Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException($"Required environment variable '{name}' is not set.");

static string StableId(string logicalName)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(logicalName));
    return Convert.ToHexString(hash).ToLowerInvariant()[..10];
}

sealed record DatasetConfig(
    string Name,
    string AccountId,
    string CountyId,
    string AdminUserId,
    string ManagerUserId,
    string ViewerUserId,
    string BlobContainer)
{
    public static DatasetConfig Parse(string value) => value switch
    {
        "generic" => new DatasetConfig(
            "generic",
            "genacct001",
            "gencounty1",
            "genadmin01",
            "genmanage1",
            "genviewer1",
            "planarian-generic"),
        "e2e" => new DatasetConfig(
            "e2e",
            "e2eacct001",
            "e2ecounty1",
            "e2eadmin01",
            "e2emanage1",
            "e2eviewer1",
            "planarian-e2e"),
        _ => throw new ArgumentException("Dataset must be 'generic' or 'e2e'.", nameof(value))
    };
}


sealed class DevelopmentSeedDbContext(DbContextOptions<PlanarianDbContext> options)
    : PlanarianDbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.EnableSensitiveDataLogging();
    }
}
