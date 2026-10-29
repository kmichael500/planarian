using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Planarian.Library.Options;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Model;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Services;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Email.Services;
using Planarian.Shared.Options;
using Planarian.Shared.Services;
using Planarian.Tests.Integration.Infrastructure.Actors;
using Southport.Messaging.Email.MailGun;

namespace Planarian.Tests.Integration.Infrastructure;

internal static class CavePermissionManagementTestSupport
{
    internal static CreateUserCavePermissionsVm PermissionVmForScope(
        string scope,
        CavePermissionMatrixTestSupport.PermissionCave target) =>
        scope switch
        {
            "cave" => new CreateUserCavePermissionsVm { CaveIds = [target.CaveId] },
            "county" => new CreateUserCavePermissionsVm { CountyIds = [target.CountyId] },
            "state" => new CreateUserCavePermissionsVm { StateIds = [target.StateId] },
            "account" => new CreateUserCavePermissionsVm { HasAllLocations = true },
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };

    internal static AccountUserManagerService CreateService(Planarian.Model.Database.PlanarianDbContext db)
    {
        var userRepository = new UserRepository(db, db.RequestUser);
        var emailOptions = new EmailOptions
        {
            Domain = "mg.integration.test",
            MjmlApplicationId = "integration-test",
            MjmlSecretKey = "integration-test"
        };
        var emailService = new EmailService(
            new MessageTypeRepository(
                db,
                db.RequestUser,
                new MjmlService(new HttpClient(new NoExternalHttpHandler()), emailOptions)),
            db.RequestUser,
            new MailGunMessageFactory(
                new HttpClient(new NoExternalHttpHandler()),
                Options.Create(new MailGunOptions
                {
                    ApiKey = "integration-test",
                    Domain = emailOptions.Domain
                })),
            new ClientUrlBuilder(new FixedClientRequestOrigin()),
            new MessageLogRepository(db, db.RequestUser),
            emailOptions,
            new TestHostEnvironment(),
            NullLogger<EmailService>.Instance);

        return new AccountUserManagerService(
            userRepository,
            db.RequestUser,
            emailService,
            new ServerOptions(),
            new AccountRepository(db, db.RequestUser),
            new PermissionRepository(db, db.RequestUser),
            userRepository,
            NullLogger<AccountUserManagerService>.Instance);
    }

    internal static async Task EnsureAccountUserAsync(
        PostgresTestDatabase database,
        string accountId,
        string userId)
    {
        await using var db = database.CreateDbContext(userId, accountId);
        await CavePermissions.EnsureAccountUserAsync(db, accountId);
    }

    internal static Task EnsureViewPermissionAsync(PostgresTestDatabase database) =>
        GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "vIeWPz9a00",
            Key = PermissionKey.View,
            Name = PermissionKey.View,
            Description = "View Caves",
            PermissionType = PermissionType.Cave
        });

    internal static Task EnsureAccountAdminPermissionAsync(PostgresTestDatabase database) =>
        GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "AdMiNz9a00",
            Key = PermissionKey.Admin,
            Name = PermissionKey.Admin,
            Description = "Account administration",
            PermissionType = PermissionType.User
        });

    internal static Task EnsurePlanarianAdminPermissionAsync(PostgresTestDatabase database) =>
        GlobalSystemTestData.EnsurePermissionAsync(database, new Permission
        {
            Id = "PlAnArIaNz",
            Key = PermissionKey.PlanarianAdmin,
            Name = PermissionKey.PlanarianAdmin,
            Description = "Platform administration",
            PermissionType = PermissionType.User
        });

    internal static async Task GrantAccountAdminAsync(
        PostgresTestDatabase database,
        string accountId,
        string userId)
    {
        await using var db = database.CreateDbContext(userId, accountId);
        db.UserPermissions.Add(new UserPermission
        {
            UserId = db.RequestUser.Id,
            AccountId = accountId,
            PermissionId = "AdMiNz9a00"
        });
        await db.SaveChangesAsync();
    }

    private sealed class NoExternalHttpHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                $"Permission-management integration tests must not call external HTTP: {request.RequestUri}");
    }

    private sealed class FixedClientRequestOrigin : IClientRequestOrigin
    {
        public string GetOrigin() => "https://planarian.integration.test";
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "IntegrationTest";
        public string ApplicationName { get; set; } = "Planarian.Tests.Integration";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
