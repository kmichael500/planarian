using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Models;
using Planarian.Shared.Options;

internal static class DevelopmentApiVerifier
{
    public static async Task VerifyAsync(
        DatasetConfig dataset,
        string connectionString,
        string apiUrl,
        string authSettingsFile)
    {
        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var baseAddress))
            throw new InvalidOperationException($"Invalid Planarian development API URL '{apiUrl}'.");

        var authOptions = await LoadAuthOptions(authSettingsFile);
        var accountId = await LoadAccountId(connectionString, dataset);
        var users = await LoadUsers(connectionString, dataset);

        using var adminClient = CreateClient(
            baseAddress,
            BuildToken(authOptions, users[dataset.AdminUserId], accountId),
            accountId);
        using var managerClient = CreateClient(
            baseAddress,
            BuildToken(authOptions, users[dataset.ManagerUserId], accountId),
            accountId);
        using var viewerClient = CreateClient(
            baseAddress,
            BuildToken(authOptions, users[dataset.ViewerUserId], accountId),
            accountId);
        using var wrongAccountClient = CreateClient(
            baseAddress,
            BuildToken(authOptions, users[dataset.AdminUserId], accountId),
            DevelopmentDatasetBaseline.StableId($"{dataset.Name}:invalid-account"));

        Console.WriteLine($"Verifying '{dataset.Name}' through the running Planarian API...");

        var stateId = await VerifyStateReferenceData(adminClient);
        await VerifyAccountSettings(adminClient, stateId);
        await VerifyCounty(adminClient, dataset, stateId);
        await VerifyFeatureSettings(adminClient);
        await VerifyTags(adminClient, dataset);
        await VerifyUsers(adminClient, dataset);
        await VerifyPermissions(adminClient, managerClient, viewerClient, dataset);
        await VerifyAccountIsolation(wrongAccountClient);
        if (dataset.Name == "e2e")
            await VerifyE2ESecondaryAccount(baseAddress, authOptions, users, connectionString, dataset);

        Console.WriteLine($"Planarian API verification passed for '{dataset.Name}'.");
    }

    private static async Task<string> VerifyStateReferenceData(HttpClient client)
    {
        var states = await GetJson<List<SelectListItem<string>>>(client, "/api/account/states");
        var matches = states
            .Where(state => string.Equals(
                state.Display,
                DevelopmentDatasetBaseline.StateAbbreviation,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        Require(matches.Count == 1,
            $"Expected exactly one '{DevelopmentDatasetBaseline.StateAbbreviation}' state through AccountService; found {matches.Count}.");
        Pass("state reference data");
        return matches[0].Value;
    }

    private static async Task VerifyAccountSettings(HttpClient client, string stateId)
    {
        var settings = await GetJson<MiscAccountSettingsVm>(client, "/api/account/settings");
        Require(settings.StateIds.Contains(stateId),
            "AccountService did not expose the seeded account-state relationship.");
        Pass("account settings + account scope");
    }

    private static async Task VerifyCounty(HttpClient client, DatasetConfig dataset, string stateId)
    {
        var counties = await GetJson<List<TagTypeTableCountyVm>>(
            client, $"/api/account/counties-table/{stateId}");
        Require(counties.Any(county => county.TagTypeId == dataset.CountyId),
            $"AccountService did not expose seeded county '{dataset.CountyId}'.");
        Pass("account county");
    }

    private static async Task VerifyFeatureSettings(HttpClient client)
    {
        var features = await GetJson<List<FeatureSettingVm>>(client, "/api/account/feature-settings");
        var accountFeatures = features.Where(feature => !feature.IsDefault).ToList();

        foreach (var key in Enum.GetValues<FeatureKey>())
        {
            var count = accountFeatures.Count(feature => feature.Key == key);
            Require(count == 1,
                $"AccountService exposed {count} account rows for feature '{key}'; expected exactly one.");
        }

        Pass("account feature settings");
    }

    private static async Task VerifyTags(HttpClient client, DatasetConfig dataset)
    {
        foreach (var group in DevelopmentDatasetBaseline.Tags.GroupBy(tag => tag.Key))
        {
            var tags = await GetJson<List<TagTypeTableVm>>(
                client, $"/api/account/tags-table/{Uri.EscapeDataString(group.Key)}");
            var visibleIds = tags.Select(tag => tag.TagTypeId).ToHashSet(StringComparer.Ordinal);
            var missingIds = group
                .Select(tag => tag.Id(dataset))
                .Where(id => !visibleIds.Contains(id))
                .ToArray();

            Require(missingIds.Length == 0,
                $"AccountService tag key '{group.Key}' is missing seeded tag(s): {string.Join(", ", missingIds)}.");
        }

        Pass("account tags");
    }

    private static async Task VerifyUsers(HttpClient client, DatasetConfig dataset)
    {
        var users = await GetJson<List<UserManagerGridVm>>(client, "/api/account/user-manager");
        var visibleIds = users.Select(user => user.UserId).ToHashSet(StringComparer.Ordinal);
        var missingIds = DevelopmentDatasetBaseline.Users(dataset)
            .Select(user => user.Id)
            .Where(id => !visibleIds.Contains(id))
            .ToArray();

        Require(missingIds.Length == 0,
            $"AccountUserManagerService is missing seeded user(s): {string.Join(", ", missingIds)}.");
        Pass("account users");
    }

    private static async Task VerifyPermissions(
        HttpClient adminClient,
        HttpClient managerClient,
        HttpClient viewerClient,
        DatasetConfig dataset)
    {
        var adminPermissions = await GetJson<List<UserPermissionVm>>(
            adminClient, $"/api/account/user-manager/{dataset.AdminUserId}/user-permissions");
        Require(adminPermissions.Any(permission => permission.PermissionKey == PermissionKey.Admin),
            "AccountUserManagerService did not expose the seeded Admin permission.");

        var managerPermissions = await GetJson<CavePermissionManagementVm>(
            adminClient,
            $"/api/account/user-manager/{dataset.ManagerUserId}/cave-permissions/{PermissionKey.Manager}");
        Require(managerPermissions.HasAllLocations,
            "AccountUserManagerService did not expose the seeded broad Manager cave permission.");

        var viewerPermissions = await GetJson<CavePermissionManagementVm>(
            adminClient,
            $"/api/account/user-manager/{dataset.ViewerUserId}/cave-permissions/{PermissionKey.View}");
        Require(viewerPermissions.HasAllLocations,
            "AccountUserManagerService did not expose the seeded broad View cave permission.");

        await GetJson<List<UserManagerGridVm>>(managerClient, "/api/account/user-manager");
        await GetJson<MiscAccountSettingsVm>(viewerClient, "/api/account/settings");

        using var forbidden = await viewerClient.GetAsync("/api/account/user-manager");
        Require(forbidden.StatusCode == HttpStatusCode.Forbidden,
            $"Expected Viewer to be forbidden from Manager endpoint; received {(int)forbidden.StatusCode} {forbidden.StatusCode}.");
        Pass("seeded permissions + authorization policies");
    }

    private static async Task VerifyAccountIsolation(HttpClient wrongAccountClient)
    {
        using var response = await wrongAccountClient.GetAsync("/api/account/settings");
        Require(response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected a valid user with an invalid account scope to receive 401; received {(int)response.StatusCode} {response.StatusCode}.");
        Pass("invalid account scope rejected");
    }

    private static async Task VerifyE2ESecondaryAccount(
        Uri baseAddress,
        AuthOptions authOptions,
        IReadOnlyDictionary<string, ApiUserIdentity> users,
        string connectionString,
        DatasetConfig dataset)
    {
        var accountId = await LoadSecondaryAccountId(connectionString);
        using var adminClient = CreateClient(
            baseAddress,
            BuildToken(authOptions, users[dataset.AdminUserId], accountId),
            accountId);
        using var managerClient = CreateClient(
            baseAddress,
            BuildToken(authOptions, users[dataset.ManagerUserId], accountId),
            accountId);

        var settings = await GetJson<MiscAccountSettingsVm>(adminClient, "/api/account/settings");
        Require(settings.AccountName == DevelopmentDatasetBaseline.E2ESecondaryAccountName,
            "Secondary E2E account name did not round-trip through AccountService.");
        Require(settings.CountyIdDelimiter == DevelopmentDatasetBaseline.E2ESecondaryCountyIdDelimiter,
            "Secondary E2E account delimiter did not round-trip through AccountService.");

        var features = await GetJson<List<FeatureSettingVm>>(adminClient, "/api/account/feature-settings");
        Require(features.Any(feature =>
                !feature.IsDefault &&
                feature.Key == FeatureKey.EnabledFieldCaveAlternateNames &&
                !feature.IsEnabled),
            "Secondary E2E account did not expose its intentionally disabled authoring field.");

        var geology = await GetJson<List<TagTypeTableVm>>(adminClient, "/api/account/tags-table/Geology");
        Require(geology.Any(tag => tag.Name == "Copper Ridge Limestone"),
            "Secondary E2E account is missing its account-specific geology tag.");
        Require(geology.All(tag => tag.Name != "Bangor Limestone"),
            "Secondary E2E account leaked the primary account's geology tag.");

        var states = await GetJson<List<SelectListItem<string>>>(adminClient, "/api/account/states");
        var stateId = states.Single(state => state.Display == DevelopmentDatasetBaseline.StateAbbreviation).Value;
        var counties = await GetJson<List<TagTypeTableCountyVm>>(
            adminClient, $"/api/account/counties-table/{stateId}");
        Require(counties.Any(county => county.TagTypeId == DevelopmentDatasetBaseline.E2ESecondaryCountyId),
            "Secondary E2E account is missing its account-specific County.");
        Require(counties.All(county => county.TagTypeId != dataset.CountyId),
            "Secondary E2E account leaked the primary account's County.");

        using var forbidden = await managerClient.GetAsync("/api/account/user-manager");
        Require(forbidden.StatusCode == HttpStatusCode.Forbidden,
            $"Expected secondary-account manager fixture to have View-only access; received {(int)forbidden.StatusCode} {forbidden.StatusCode}.");
        await GetJson<MiscAccountSettingsVm>(managerClient, "/api/account/settings");
        Pass("secondary account feature/tag/county/permission isolation");
    }

    private static async Task<string> LoadSecondaryAccountId(string connectionString)
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
            .Options;
        await using var db = new PlanarianDbContext(options);
        return await db.Counties
            .AsNoTracking()
            .Where(county => county.Id == DevelopmentDatasetBaseline.E2ESecondaryCountyId)
            .Select(county => county.AccountId)
            .SingleAsync();
    }

    private static HttpClient CreateClient(Uri baseAddress, string token, string accountId)
    {
        var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(15)
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("x-account", accountId);
        return client;
    }

    private static string BuildToken(AuthOptions options, ApiUserIdentity user, string accountId) =>
        new TokenService(options).BuildToken(
            new UserToken(user.FullName, user.Id, accountId, user.SessionVersion));

    private static async Task<string> LoadAccountId(string connectionString, DatasetConfig dataset)
    {
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
            .Options;
        await using var db = new PlanarianDbContext(options);
        return await DevelopmentDatasetLocator.RequireAccountIdAsync(db, dataset);
    }

    private static async Task<Dictionary<string, ApiUserIdentity>> LoadUsers(
        string connectionString,
        DatasetConfig dataset)
    {
        var expectedIds = new[] { dataset.AdminUserId, dataset.ManagerUserId, dataset.ViewerUserId };
        var options = new DbContextOptionsBuilder<PlanarianDbContext>()
            .UseNpgsql(connectionString, postgres => postgres.UseNetTopologySuite())
            .Options;

        await using var db = new PlanarianDbContext(options);
        var users = await db.Users
            .AsNoTracking()
            .Where(user => expectedIds.Contains(user.Id))
            .Select(user => new ApiUserIdentity(
                user.Id,
                user.FirstName + " " + user.LastName,
                user.SessionVersion))
            .ToDictionaryAsync(user => user.Id);

        var missingIds = expectedIds.Where(id => !users.ContainsKey(id)).ToArray();
        if (missingIds.Length > 0)
            throw new InvalidOperationException(
                $"Cannot build development API identity; seeded user(s) are missing: {string.Join(", ", missingIds)}.");

        return users;
    }

    private static async Task<AuthOptions> LoadAuthOptions(string path)
    {
        if (!System.IO.File.Exists(path))
            throw new InvalidOperationException($"Development auth settings file was not found: {path}");

        await using var stream = System.IO.File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        if (!document.RootElement.TryGetProperty(AuthOptions.Key, out var auth))
            throw new InvalidOperationException($"Development settings file '{path}' does not contain '{AuthOptions.Key}'.");

        var secret = auth.GetProperty(nameof(AuthOptions.JwtSecret)).GetString();
        var issuer = auth.GetProperty(nameof(AuthOptions.JwtIssuer)).GetString();
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(issuer))
            throw new InvalidOperationException($"Development settings file '{path}' has incomplete Auth settings.");

        return new AuthOptions
        {
            JwtSecret = secret,
            JwtIssuer = issuer,
            JwtExpiryDurationSeconds = 60
        };
    }

    private static async Task<T> GetJson<T>(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            var preview = body.Length <= 800 ? body : body[..800] + "...";
            throw new InvalidOperationException(
                $"GET {path} returned {(int)response.StatusCode} {response.StatusCode}: {preview}");
        }

        var value = JsonSerializer.Deserialize<T>(body, PlanarianJsonOptions.Default);
        return value ?? throw new InvalidOperationException($"GET {path} returned an empty JSON result.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Development API verification failed: {message}");
    }

    private static void Pass(string check) => Console.WriteLine($"  ✓ {check}");

    private sealed record ApiUserIdentity(string Id, string FullName, int SessionVersion);
}
