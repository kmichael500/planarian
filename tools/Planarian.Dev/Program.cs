using System.Security.Cryptography;
using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Generators;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.PlanarianSettings.Models;
using Planarian.Modules.PlanarianSettings.Repositories;
using Planarian.Modules.PlanarianSettings.Services;

const string connectionVariable = "PLANARIAN_DEV_CONNECTION_STRING";
const string storageVariable = "PLANARIAN_DEV_BLOB_CONNECTION_STRING";
const string apiUrlVariable = "PLANARIAN_DEV_API_URL";
const string authSettingsFileVariable = "PLANARIAN_DEV_AUTH_SETTINGS_FILE";

if (args.Length != 2 || args[0] is not ("seed" or "reset-storage" or "verify-api"))
{
    Console.Error.WriteLine("Usage: Planarian.Dev <seed|reset-storage|verify-api> <generic|e2e>");
    return 2;
}

var dataset = DatasetConfig.Parse(args[1]);
if (args[0] == "reset-storage")
{
    await ResetStorage(dataset);
    return 0;
}

var connectionString = RequireEnvironment(connectionVariable);
if (args[0] == "verify-api")
{
    await DevelopmentApiVerifier.VerifyAsync(
        dataset,
        connectionString,
        RequireEnvironment(apiUrlVariable),
        RequireEnvironment(authSettingsFileVariable));
    return 0;
}

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
    // TODO(.NET 10 / EF Core 10+): When Planarian is upgraded, evaluate moving this custom dev-data
    // initialization to UseSeeding/UseAsyncSeeding so it participates in EF's migration locking. Keep both
    // sync and async seed hooks because EF tooling currently relies on UseSeeding even for async applications.
    var options = new DbContextOptionsBuilder<PlanarianDbContext>()
        .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
        .Options;

    await using var db = new DevelopmentSeedDbContext(options);
    await new DataGenerator(db).AddOrUpdateDefaultData();
    await EnsureUsers(db, dataset);
    await db.SaveChangesAsync();
    await RemoveStaleBootstrapPlanarianAdminPermission(options, dataset);

    var stateId = await ResolveStateId(db);
    var accountId = await DevelopmentDatasetLocator.FindAccountIdAsync(db, dataset)
        ?? await CreateAccountThroughService(connectionString, dataset, stateId);

    await EnsureDatasetDetails(db, dataset, accountId, stateId);
    if (dataset.Name == "e2e")
        await EnsureE2ESecondaryAccount(connectionString, dataset, stateId);
    await VerifyBaseline(db, dataset, accountId, stateId);
}

static async Task<string> ResolveStateId(PlanarianDbContext db) =>
    await db.States
        .AsNoTracking()
        .Where(state => state.Abbreviation == DevelopmentDatasetBaseline.StateAbbreviation)
        .Select(state => state.Id)
        .SingleAsync();

static async Task RemoveStaleBootstrapPlanarianAdminPermission(
    DbContextOptions<PlanarianDbContext> options,
    DatasetConfig dataset)
{
    await using var db = new DevelopmentSeedDbContext(options);
    var bootstrapPermissionId = DevelopmentDatasetBaseline.BootstrapPlanarianAdminPermissionId(dataset);
    var staleBootstrapPermission = await db.UserPermissions
        .SingleOrDefaultAsync(row => row.Id == bootstrapPermissionId);
    if (staleBootstrapPermission == null)
        return;

    db.UserPermissions.Remove(staleBootstrapPermission);
    await db.SaveChangesAsync();
}

static async Task EnsureUsers(PlanarianDbContext db, DatasetConfig dataset)
{
    foreach (var userSpec in DevelopmentDatasetBaseline.Users(dataset))
    {
        if (await db.Users.AnyAsync(user => user.Id == userSpec.Id)) continue;

        db.Users.Add(new User(userSpec.FirstName, userSpec.LastName, userSpec.EmailAddress)
        {
            Id = userSpec.Id,
            HashedPassword = PasswordService.Hash(DevelopmentDatasetBaseline.DefaultPassword),
            EmailConfirmedOn = DevelopmentDatasetBaseline.CreatedOn,
            CreatedOn = DevelopmentDatasetBaseline.CreatedOn
        });
    }
}

static async Task<string> CreateAccountThroughService(
    string connectionString,
    DatasetConfig dataset,
    string stateId,
    string? accountName = null,
    string? countyId = null,
    string? countyName = null,
    string? countyDisplayId = null,
    string? countyIdDelimiter = null)
{
    var options = new DbContextOptionsBuilder<PlanarianDbContext>()
        .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
        .Options;
    var bootstrapPermissionId = DevelopmentDatasetBaseline.BootstrapPlanarianAdminPermissionId(dataset);
    var removeBootstrapPermission = false;

    await using (var bootstrapDb = new DevelopmentSeedDbContext(options))
    {
        var planarianAdminPermissionId = await bootstrapDb.Permissions
            .Where(permission => permission.Key == PermissionKey.PlanarianAdmin)
            .Select(permission => permission.Id)
            .SingleAsync();
        var hasPlanarianAdmin = await bootstrapDb.UserPermissions.AnyAsync(row =>
            row.UserId == dataset.AdminUserId &&
            row.AccountId == null &&
            row.PermissionId == planarianAdminPermissionId);

        if (!hasPlanarianAdmin)
        {
            bootstrapDb.UserPermissions.Add(new UserPermission
            {
                Id = bootstrapPermissionId,
                UserId = dataset.AdminUserId,
                PermissionId = planarianAdminPermissionId,
                CreatedOn = DevelopmentDatasetBaseline.CreatedOn
            });
            await bootstrapDb.SaveChangesAsync();
            removeBootstrapPermission = true;
        }
    }

    try
    {
        await using var db = new PlanarianDbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync();

        var requestUser = new RequestUser(db);
        db.RequestUser = requestUser;
        await requestUser.Initialize(null, dataset.AdminUserId);
        if (!requestUser.IsAuthenticated)
            throw new InvalidOperationException("Seeded development admin could not be initialized for account creation.");

        var repository = new PlanarianSettingsRepository(db, requestUser);
        var service = new PlanarianSettingsService(repository, requestUser);
        var accountId = await service.CreateAccount(new CreateAccountVm
        {
            Name = accountName ?? DevelopmentDatasetBaseline.AccountName,
            CountyIdDelimiter = countyIdDelimiter ?? DevelopmentDatasetBaseline.CountyIdDelimiter,
            DefaultViewAccessAllCaves = DevelopmentDatasetBaseline.DefaultViewAccessAllCaves,
            ExportEnabled = DevelopmentDatasetBaseline.ExportEnabled,
            StateIds = [stateId]
        }, CancellationToken.None);

        await requestUser.Initialize(accountId, dataset.AdminUserId);
        db.Counties.Add(new County
        {
            Id = countyId ?? dataset.CountyId,
            AccountId = accountId,
            StateId = stateId,
            Name = countyName ?? DevelopmentDatasetBaseline.CountyName,
            DisplayId = countyDisplayId ?? DevelopmentDatasetBaseline.CountyDisplayId
        });
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
        Console.WriteLine(
            $"Created '{accountName ?? DevelopmentDatasetBaseline.AccountName}' account through PlanarianSettingsService.CreateAccount ({accountId}).");
        return accountId;
    }
    finally
    {
        if (removeBootstrapPermission)
        {
            await using var cleanupDb = new DevelopmentSeedDbContext(options);
            var bootstrapPermission = await cleanupDb.UserPermissions
                .SingleOrDefaultAsync(row => row.Id == bootstrapPermissionId);
            if (bootstrapPermission != null)
            {
                cleanupDb.UserPermissions.Remove(bootstrapPermission);
                await cleanupDb.SaveChangesAsync();
            }
        }
    }
}

static async Task EnsureDatasetDetails(
    PlanarianDbContext db, DatasetConfig dataset, string accountId, string stateId)
{
    var createdOn = DevelopmentDatasetBaseline.CreatedOn;
    if (!await db.AccountStates.AnyAsync(row => row.AccountId == accountId && row.StateId == stateId))
    {
        db.AccountStates.Add(new AccountState
        {
            Id = DevelopmentDatasetBaseline.AccountStateId(dataset),
            AccountId = accountId,
            StateId = stateId,
            CreatedOn = createdOn
        });
    }

    if (!await db.Counties.AnyAsync(row => row.Id == dataset.CountyId))
    {
        db.Counties.Add(new County
        {
            Id = dataset.CountyId,
            AccountId = accountId,
            StateId = stateId,
            Name = DevelopmentDatasetBaseline.CountyName,
            DisplayId = DevelopmentDatasetBaseline.CountyDisplayId,
            CreatedOn = createdOn
        });
    }

    var existingFeatureKeys = (await db.FeatureSettings
        .Where(setting => setting.AccountId == accountId)
        .Select(setting => setting.Key)
        .ToListAsync())
        .ToHashSet();
    foreach (var key in Enum.GetValues<FeatureKey>())
    {
        if (existingFeatureKeys.Contains(key)) continue;
        db.FeatureSettings.Add(new FeatureSetting
        {
            Id = DevelopmentDatasetBaseline.FeatureId(dataset, key),
            AccountId = accountId,
            Key = key,
            IsEnabled = AccountFeatureSettingDefaults.IsEnabled(key),
            IsDefault = false,
            CreatedOn = createdOn
        });
    }

    foreach (var userSpec in DevelopmentDatasetBaseline.Users(dataset))
    {
        if (await db.AccountUsers.AnyAsync(row => row.AccountId == accountId && row.UserId == userSpec.Id)) continue;
        db.AccountUsers.Add(new AccountUser
        {
            Id = DevelopmentDatasetBaseline.AccountUserId(dataset, userSpec.Id),
            AccountId = accountId,
            UserId = userSpec.Id,
            InvitationAcceptedOn = createdOn,
            CreatedOn = createdOn
        });
    }

    var permissions = await db.Permissions
        .Where(permission => permission.Key == PermissionKey.Admin ||
                             permission.Key == PermissionKey.Manager ||
                             permission.Key == PermissionKey.View)
        .ToDictionaryAsync(permission => permission.Key);
    var adminPermission = permissions[PermissionKey.Admin];
    if (!await db.UserPermissions.AnyAsync(row =>
            row.AccountId == accountId && row.UserId == dataset.AdminUserId &&
            row.PermissionId == adminPermission.Id))
    {
        db.UserPermissions.Add(new UserPermission
        {
            Id = DevelopmentDatasetBaseline.AdminPermissionId(dataset),
            AccountId = accountId,
            UserId = dataset.AdminUserId,
            PermissionId = adminPermission.Id,
            CreatedOn = createdOn
        });
    }

    var broadPermissions = new[]
    {
        BroadCavePermission(dataset, accountId, dataset.ManagerUserId, permissions[PermissionKey.Manager].Id, createdOn),
        BroadCavePermission(dataset, accountId, dataset.ViewerUserId, permissions[PermissionKey.View].Id, createdOn)
    };
    foreach (var permission in broadPermissions)
    {
        if (!await HasBroadCavePermission(db, permission.UserId, accountId, permission.PermissionId))
            db.CavePermissions.Add(permission);
    }

    foreach (var tag in DatasetTags(dataset, accountId, createdOn))
    {
        if (!await db.TagTypes.AnyAsync(row => row.Id == tag.Id))
            db.TagTypes.Add(tag);
    }

    await db.SaveChangesAsync();
}

static async Task EnsureE2ESecondaryAccount(
    string connectionString, DatasetConfig dataset, string stateId)
{
    var options = new DbContextOptionsBuilder<PlanarianDbContext>()
        .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
        .Options;

    await using var lookup = new DevelopmentSeedDbContext(options);
    var accountId = await lookup.Counties
        .AsNoTracking()
        .Where(county => county.Id == DevelopmentDatasetBaseline.E2ESecondaryCountyId)
        .Select(county => county.AccountId)
        .SingleOrDefaultAsync();

    if (accountId == null)
    {
        accountId = await CreateAccountThroughService(
            connectionString,
            dataset,
            stateId,
            DevelopmentDatasetBaseline.E2ESecondaryAccountName,
            DevelopmentDatasetBaseline.E2ESecondaryCountyId,
            DevelopmentDatasetBaseline.E2ESecondaryCountyName,
            DevelopmentDatasetBaseline.E2ESecondaryCountyDisplayId,
            DevelopmentDatasetBaseline.E2ESecondaryCountyIdDelimiter);
    }

    await using var db = new DevelopmentSeedDbContext(options);
    var createdOn = DevelopmentDatasetBaseline.CreatedOn;

    if (!await db.AccountStates.AnyAsync(row => row.AccountId == accountId && row.StateId == stateId))
    {
        db.AccountStates.Add(new AccountState
        {
            Id = DevelopmentDatasetBaseline.E2ESecondaryAccountStateId(dataset),
            AccountId = accountId,
            StateId = stateId,
            CreatedOn = createdOn
        });
    }

    foreach (var userSpec in DevelopmentDatasetBaseline.Users(dataset))
    {
        if (await db.AccountUsers.AnyAsync(row => row.AccountId == accountId && row.UserId == userSpec.Id))
            continue;

        db.AccountUsers.Add(new AccountUser
        {
            Id = DevelopmentDatasetBaseline.E2ESecondaryAccountUserId(dataset, userSpec.Id),
            AccountId = accountId,
            UserId = userSpec.Id,
            InvitationAcceptedOn = createdOn,
            CreatedOn = createdOn
        });
    }

    var existingFeatureKeys = (await db.FeatureSettings
        .Where(setting => setting.AccountId == accountId)
        .Select(setting => setting.Key)
        .ToListAsync())
        .ToHashSet();
    foreach (var key in Enum.GetValues<FeatureKey>())
    {
        if (existingFeatureKeys.Contains(key)) continue;
        db.FeatureSettings.Add(new FeatureSetting
        {
            Id = DevelopmentDatasetBaseline.E2ESecondaryFeatureId(dataset, key),
            AccountId = accountId,
            Key = key,
            IsEnabled = AccountFeatureSettingDefaults.IsEnabled(key),
            IsDefault = false,
            CreatedOn = createdOn
        });
    }

    await db.SaveChangesAsync();

    var alternateNames = await db.FeatureSettings.SingleAsync(setting =>
        setting.AccountId == accountId &&
        setting.Key == FeatureKey.EnabledFieldCaveAlternateNames &&
        !setting.IsDefault);
    alternateNames.IsEnabled = false;

    var viewPermissionId = await db.Permissions
        .Where(permission => permission.Key == PermissionKey.View)
        .Select(permission => permission.Id)
        .SingleAsync();
    foreach (var userId in new[] { dataset.ManagerUserId, dataset.ViewerUserId })
    {
        if (await HasBroadCavePermission(db, userId, accountId, viewPermissionId)) continue;
        db.CavePermissions.Add(new CavePermission
        {
            Id = DevelopmentDatasetBaseline.E2ESecondaryCavePermissionId(dataset, userId, viewPermissionId),
            AccountId = accountId,
            UserId = userId,
            PermissionId = viewPermissionId,
            CreatedOn = createdOn
        });
    }

    foreach (var spec in DevelopmentDatasetBaseline.E2ESecondaryTags)
    {
        var tagId = spec.Id(dataset);
        if (await db.TagTypes.AnyAsync(row => row.Id == tagId)) continue;
        db.TagTypes.Add(new TagType(spec.Name, spec.Key)
        {
            Id = tagId,
            AccountId = accountId,
            IsDefault = false,
            CreatedOn = createdOn
        });
    }

    await db.SaveChangesAsync();
}

static CavePermission BroadCavePermission(
    DatasetConfig dataset, string accountId, string userId, string permissionId, DateTime createdOn) => new()
{
    Id = DevelopmentDatasetBaseline.CavePermissionId(dataset, userId, permissionId),
    AccountId = accountId,
    UserId = userId,
    PermissionId = permissionId,
    CreatedOn = createdOn
};

static Task<bool> HasBroadCavePermission(
    PlanarianDbContext db, string userId, string accountId, string permissionId) =>
    db.CavePermissions.AnyAsync(row =>
        row.UserId == userId &&
        row.AccountId == accountId &&
        row.PermissionId == permissionId &&
        row.StateId == null &&
        row.CountyId == null &&
        row.CaveId == null);

static IEnumerable<TagType> DatasetTags(
    DatasetConfig dataset, string accountId, DateTime createdOn) =>
    DevelopmentDatasetBaseline.Tags.Select(spec => new TagType(spec.Name, spec.Key)
    {
        Id = spec.Id(dataset),
        AccountId = accountId,
        IsDefault = false,
        CreatedOn = createdOn
    });

static async Task VerifyBaseline(
    PlanarianDbContext db, DatasetConfig dataset, string accountId, string stateId)
{
    var failures = new List<string>();
    if (!await db.Accounts.AnyAsync(row => row.Id == accountId))
        failures.Add($"account '{accountId}' is missing");

    if (!await db.AccountStates.AnyAsync(row => row.AccountId == accountId && row.StateId == stateId))
        failures.Add($"account is not associated with {DevelopmentDatasetBaseline.StateAbbreviation}");

    if (!await db.Counties.AnyAsync(row =>
            row.Id == dataset.CountyId && row.AccountId == accountId && row.StateId == stateId))
        failures.Add($"county '{dataset.CountyId}' is missing or not associated with the seeded account/state");

    var bootstrapPermissionId = DevelopmentDatasetBaseline.BootstrapPlanarianAdminPermissionId(dataset);
    if (await db.UserPermissions.AnyAsync(row => row.Id == bootstrapPermissionId))
        failures.Add("temporary Planarian-admin bootstrap permission was not cleaned up");

    var accountFeatureRows = await db.FeatureSettings
        .AsNoTracking()
        .Where(setting => setting.AccountId == accountId && !setting.IsDefault)
        .Select(setting => setting.Key)
        .ToListAsync();
    foreach (var key in Enum.GetValues<FeatureKey>())
    {
        var count = accountFeatureRows.Count(rowKey => rowKey == key);
        if (count != 1)
            failures.Add($"feature '{key}' has {count} account rows; expected exactly one");
    }

    foreach (var user in DevelopmentDatasetBaseline.Users(dataset))
    {
        if (!await db.AccountUsers.AnyAsync(row => row.AccountId == accountId && row.UserId == user.Id))
            failures.Add($"user '{user.Id}' is not associated with account '{accountId}'");
    }

    foreach (var tag in DevelopmentDatasetBaseline.Tags)
    {
        var tagId = tag.Id(dataset);
        if (!await db.TagTypes.AnyAsync(row =>
                row.Id == tagId && row.AccountId == accountId && row.Key == tag.Key))
            failures.Add($"tag '{tagId}' ({tag.Key}) is missing from the seeded account");
    }

    var requiredPermissionKeys = new[] { PermissionKey.Admin, PermissionKey.Manager, PermissionKey.View };
    var permissionRows = await db.Permissions
        .AsNoTracking()
        .Where(permission => requiredPermissionKeys.Contains(permission.Key))
        .Select(permission => new { permission.Id, permission.Key })
        .ToListAsync();
    var permissionIds = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var permissionKey in requiredPermissionKeys)
    {
        var matches = permissionRows.Where(permission => permission.Key == permissionKey).ToList();
        if (matches.Count == 1)
            permissionIds[permissionKey] = matches[0].Id;
        else
            failures.Add($"permission key '{permissionKey}' has {matches.Count} rows; expected exactly one");
    }

    if (permissionIds.TryGetValue(PermissionKey.Admin, out var adminPermissionId) &&
        !await db.UserPermissions.AnyAsync(row =>
            row.AccountId == accountId && row.UserId == dataset.AdminUserId && row.PermissionId == adminPermissionId))
        failures.Add("seeded admin does not have the Admin permission");

    if (permissionIds.TryGetValue(PermissionKey.Manager, out var managerPermissionId) &&
        !await HasBroadCavePermission(db, dataset.ManagerUserId, accountId, managerPermissionId))
        failures.Add("seeded manager does not have broad Manager permission");

    if (permissionIds.TryGetValue(PermissionKey.View, out var viewPermissionId) &&
        !await HasBroadCavePermission(db, dataset.ViewerUserId, accountId, viewPermissionId))
        failures.Add("seeded viewer does not have broad View permission");

    if (failures.Count > 0)
    {
        throw new InvalidOperationException(
            $"Seed verification failed for '{dataset.Name}'. Run './dev reset {dataset.Name}' to rebuild the local dataset. " +
            string.Join("; ", failures));
    }

    Console.WriteLine($"Dataset '{dataset.Name}' seeded and verified using account '{accountId}'.");
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

sealed class DevelopmentSeedDbContext(DbContextOptions<PlanarianDbContext> options)
    : PlanarianDbContext(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Intentionally do not call PlanarianDbContext.OnConfiguring: this context seeds
        // global reference data before an account/request user exists, so the normal
        // tenant-aware SaveChangesInterceptor must not be installed here.
        optionsBuilder.EnableSensitiveDataLogging();
    }
}
