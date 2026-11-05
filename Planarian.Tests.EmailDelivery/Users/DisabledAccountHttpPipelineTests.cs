using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using Planarian.Library.Exceptions;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Controller;
using Planarian.Modules.Account.Repositories;
using Planarian.Modules.Account.Services;
using Planarian.Modules.App.Repositories;
using Planarian.Modules.Caves.Controllers;
using Planarian.Modules.Files.Controllers;
using Planarian.Modules.Map.Controllers;
using Planarian.Modules.Authentication.Models;
using Planarian.Modules.Authentication.Services;
using Planarian.Modules.Users.Repositories;
using Planarian.Shared.Services;
using Planarian.Shared.Routing;
using Planarian.Tests.EmailDelivery.TestInfrastructure;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Users;

public sealed class DisabledAccountHttpPipelineTests
{
    private const string AccountA = "account001";
    private const string AccountB = "account002";
    private const string OwnerId = "owner00001";
    private const string SharedUserId = "member0001";
    private const string SingleAccountUserId = "member0002";
    private const string AdminPermissionId = "admperm001";

    [Fact]
    public async Task PreviouslyValidCredentialsCannotUseDisabledAccountButOtherAccountAndRestorationWork()
    {
        await IsolatedPostgresDatabase.Run(async connectionString =>
        {
            await SeedAccounts(connectionString);
            var tokenService = new TokenService(new AuthOptions
            {
                JwtSecret = new string('t', 64),
                JwtIssuer = "planarian-http-security-tests",
                JwtExpiryDurationSeconds = 3600
            });
            var sharedToken = tokenService.BuildToken(new UserToken(
                "Shared Member", SharedUserId, AccountA));
            var singleAccountToken = tokenService.BuildToken(new UserToken(
                "Single Member", SingleAccountUserId, AccountA));

            // Use production middleware in a narrow HTTP host without starting unrelated services.
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
            builder.Services.AddSingleton(tokenService);
            builder.Services.AddSingleton(new ServerOptions());
            builder.Services.AddDbContext<PlanarianDbContext>(options =>
                options.UseNpgsql(connectionString, database =>
                {
                    database.MigrationsAssembly(typeof(Planarian.Migrations.Migrations.v33).Assembly.FullName);
                    database.UseNetTopologySuite();
                }));
            builder.Services.AddScoped<RequestUser>(services =>
            {
                var context = services.GetRequiredService<PlanarianDbContext>();
                var user = new RequestUser(context);
                context.RequestUser = user;
                return user;
            });
            builder.Services.AddScoped<AppRepository>();
            builder.Services.AddControllers().AddApplicationPart(typeof(CaveController).Assembly);
            builder.Services.Configure<RouteOptions>(options =>
                options.ConstraintMap[UserInvitationRoutes.CodeConstraint] = typeof(InvitationCodeRouteConstraint));
            builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = tokenService.GetTokenValidationParameters();
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var authorization = context.Request.Headers[HeaderNames.Authorization].ToString();
                            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                                context.Token = authorization["Bearer ".Length..].Trim();
                            else
                            {
                                var cookie = context.Request.Cookies[AuthCookieService.AuthCookieName];
                                if (!string.IsNullOrWhiteSpace(cookie))
                                    context.Token = cookie;
                            }
                            return Task.CompletedTask;
                        }
                    };
                });
            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(PermissionPolicyKey.View, policy =>
                    policy.Requirements.Add(new PermissionRequirement([
                        PermissionKey.PlanarianAdmin, PermissionKey.Admin,
                        PermissionKey.Manager, PermissionKey.View
                    ])));
                options.AddPolicy(PermissionPolicyKey.Admin, policy =>
                    policy.Requirements.Add(new PermissionRequirement([
                        PermissionKey.PlanarianAdmin, PermissionKey.Admin
                    ])));
            });

            await using var app = builder.Build();
            var callsToProtectedEndpoints = 0;
            var matchedActions = new ConcurrentDictionary<string, (Type Controller, string Action)>();
            app.UseRouting();
            app.Use(async (context, next) =>
            {
                var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
                if (action != null)
                    matchedActions[context.Request.Path] = (action.ControllerTypeInfo.AsType(), action.ActionName);
                await next(context);
            });
            app.UseAuthentication();
            app.UseMiddleware<HttpResponseExceptionMiddleware>();
            app.UseMiddleware<RequestUserMiddleware>();
            app.UseAuthorization();
            app.MapControllers();

            app.MapGet("/probe/caves", (RequestUser user) =>
            {
                Interlocked.Increment(ref callsToProtectedEndpoints);
                return Results.Ok(user.AccountId);
            }).RequireAuthorization(PermissionPolicyKey.View);
            app.MapPost("/probe/mutate", () =>
            {
                Interlocked.Increment(ref callsToProtectedEndpoints);
                return Results.Ok();
            }).RequireAuthorization(PermissionPolicyKey.Admin);
            app.MapGet("/probe/available-accounts", async (AppRepository repository) =>
                Results.Ok(await repository.GetAccountIds())).RequireAuthorization();
            app.MapGet("/api/app/initialize/security-probe", (RequestUser user) =>
                Results.Ok(new { user.IsAuthenticated, user.AccountId })).AllowAnonymous();

            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };

            async Task<HttpResponseMessage> Request(HttpMethod method, string path, string token,
                string? account = null, bool useCookie = false)
            {
                using var request = new HttpRequestMessage(method, path);
                if (useCookie)
                    request.Headers.Add("Cookie", $"{AuthCookieService.AuthCookieName}={token}");
                else
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);
                if (account != null)
                    request.Headers.Add("x-account", account);
                return await client.SendAsync(request);
            }

            async Task AssertStatus(HttpStatusCode expected, HttpMethod method, string path,
                string token, string? account = null, bool useCookie = false)
            {
                using var response = await Request(method, path, token, account, useCookie);
                Assert.Equal(expected, response.StatusCode);
            }

            async Task AssertDisabled(HttpMethod method, string path, string token,
                string? account = null, bool useCookie = false)
            {
                using var response = await Request(method, path, token, account, useCookie);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                Assert.Equal(nameof(ApiExceptionType.InvalidAccountSelection),
                    error.RootElement.GetProperty("errorCode").GetString());
            }

            await AssertStatus(HttpStatusCode.OK, HttpMethod.Get, "/probe/caves",
                sharedToken, AccountA);
            await AssertStatus(HttpStatusCode.OK, HttpMethod.Post, "/probe/mutate",
                sharedToken, AccountA);
            var segments = sharedToken.Split('.');
            segments[2] = new string('A', segments[2].Length);
            await AssertStatus(HttpStatusCode.Unauthorized, HttpMethod.Get, "/probe/caves",
                string.Join('.', segments), AccountA);

            await using (var disable = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(disable) { Id = OwnerId, AccountId = AccountA };
                disable.RequestUser = actor;
                var service = new AccountAccessService(new UserRepository(disable, actor),
                    new AccountRepository(disable, actor), actor);
                await service.DisableAccess(SharedUserId, disableMessage: "Account A access disabled.");
                await service.DisableAccess(SingleAccountUserId, disableMessage: "Account A access disabled.");
            }

            var successfulCallsBeforeDenials = Volatile.Read(ref callsToProtectedEndpoints);
            await AssertDisabled(HttpMethod.Get, "/probe/caves", sharedToken, AccountA);
            await AssertDisabled(HttpMethod.Post, "/probe/mutate", sharedToken, AccountA);
            await AssertDisabled(HttpMethod.Get, "/probe/caves", sharedToken,
                AccountA, useCookie: true);
            await AssertDisabled(HttpMethod.Get, $"/probe/caves?account_id={AccountA}", sharedToken);
            await AssertDisabled(HttpMethod.Get, $"/probe/caves?account_id={AccountB}",
                sharedToken, AccountA);

            var controllerRequests = new[]
            {
                (HttpMethod.Get, "/api/caves", typeof(CaveController),
                    nameof(CaveController.GetCaves)),
                (HttpMethod.Get, "/api/map?north=1&south=0&east=1&west=0&zoom=7",
                    typeof(MapController), nameof(MapController.GetMapData)),
                (HttpMethod.Get, "/api/files/file000001/view", typeof(FileController),
                    nameof(FileController.ViewFile)),
                (HttpMethod.Post,
                    $"/api/account/user-manager/{SharedUserId}/restore-access",
                    typeof(AccountUserManagerController),
                    nameof(AccountUserManagerController.RestoreAccess)),
                (HttpMethod.Post,
                    $"/api/account/user-manager/{SharedUserId}/revoke-membership",
                    typeof(AccountUserManagerController),
                    nameof(AccountUserManagerController.RevokeMembership))
            };
            foreach (var (method, path, controller, action) in controllerRequests)
            {
                await AssertDisabled(method, path, sharedToken, AccountA);
                var requestPath = path.Split('?')[0];
                Assert.True(matchedActions.TryGetValue(requestPath, out var matched),
                    $"No controller action matched {method} {path}");
                Assert.Equal((controller, action), matched);
            }

            await AssertDisabled(HttpMethod.Get, "/probe/caves", singleAccountToken, AccountA);
            await AssertStatus(HttpStatusCode.Forbidden, HttpMethod.Get,
                "/probe/caves", singleAccountToken);
            await AssertDisabled(HttpMethod.Get, "/probe/caves", singleAccountToken, AccountB);
            Assert.Equal(successfulCallsBeforeDenials, Volatile.Read(ref callsToProtectedEndpoints));

            await AssertStatus(HttpStatusCode.OK, HttpMethod.Get, "/probe/caves",
                sharedToken, AccountB);
            await AssertStatus(HttpStatusCode.OK, HttpMethod.Post, "/probe/mutate",
                sharedToken, AccountB);
            await AssertStatus(HttpStatusCode.OK, HttpMethod.Get, "/probe/caves",
                sharedToken, AccountB, useCookie: true);
            await AssertStatus(HttpStatusCode.OK, HttpMethod.Get,
                $"/probe/caves?account_id={AccountA}", sharedToken, AccountB);

            using (var accounts = await Request(HttpMethod.Get, "/probe/available-accounts",
                       sharedToken))
            {
                Assert.Equal(HttpStatusCode.OK, accounts.StatusCode);
                using var accountList = JsonDocument.Parse(await accounts.Content.ReadAsStringAsync());
                Assert.Equal(new[] { AccountB }, accountList.RootElement.EnumerateArray()
                    .Select(account => account.GetProperty("value").GetString()));
            }
            using (var bootstrap = await Request(HttpMethod.Get,
                       "/api/app/initialize/security-probe", singleAccountToken, AccountA))
            {
                Assert.Equal(HttpStatusCode.OK, bootstrap.StatusCode);
                var body = await bootstrap.Content.ReadAsStringAsync();
                Assert.Contains("\"isAuthenticated\":true", body);
                Assert.Contains("\"accountId\":null", body);
            }

            await using (var restore = IsolatedPostgresDatabase.NewContext(connectionString))
            {
                var actor = new RequestUser(restore) { Id = OwnerId, AccountId = AccountA };
                restore.RequestUser = actor;
                await new AccountAccessService(new UserRepository(restore, actor),
                    new AccountRepository(restore, actor), actor).RestoreAccess(SharedUserId);
            }
            await AssertStatus(HttpStatusCode.OK, HttpMethod.Get, "/probe/caves",
                sharedToken, AccountA);
            await AssertStatus(HttpStatusCode.Forbidden, HttpMethod.Get, "/probe/caves",
                singleAccountToken, AccountA);

            using var anonymous = await client.GetAsync("/probe/caves");
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        });
    }

    private static async Task SeedAccounts(string connectionString)
    {
        await using var context = IsolatedPostgresDatabase.NewContext(connectionString);
        var actor = new RequestUser(context) { Id = OwnerId };
        context.RequestUser = actor;
        context.Users.AddRange(
            new User("Account", "Owner", "owner@example.test") { Id = OwnerId },
            new User("Shared", "Member", "shared@example.test") { Id = SharedUserId },
            new User("Single", "Member", "single@example.test") { Id = SingleAccountUserId });
        await context.SaveChangesAsync();

        actor.AccountId = AccountA;
        context.Accounts.Add(new Account { Id = AccountA, Name = "Account A" });
        context.AccountUsers.AddRange(
            new AccountUser { AccountId = AccountA, UserId = OwnerId,
                InvitationAcceptedOn = DateTime.UtcNow },
            new AccountUser { AccountId = AccountA, UserId = SharedUserId,
                InvitationAcceptedOn = DateTime.UtcNow },
            new AccountUser { AccountId = AccountA, UserId = SingleAccountUserId,
                InvitationAcceptedOn = DateTime.UtcNow });
        context.Permissions.Add(new Permission
        {
            Id = AdminPermissionId, Key = PermissionPolicyKey.Admin, Name = "Admin",
            Description = "Test administrator", PermissionType = PermissionType.User,
            IsHidden = false
        });
        context.UserPermissions.AddRange(
            new UserPermission { AccountId = AccountA, UserId = OwnerId,
                PermissionId = AdminPermissionId },
            new UserPermission { AccountId = AccountA, UserId = SharedUserId,
                PermissionId = AdminPermissionId },
            new UserPermission { AccountId = AccountA, UserId = SingleAccountUserId,
                PermissionId = AdminPermissionId });
        await context.SaveChangesAsync();

        actor.AccountId = AccountB;
        context.Accounts.Add(new Account { Id = AccountB, Name = "Account B" });
        context.AccountUsers.Add(new AccountUser
        {
            AccountId = AccountB, UserId = SharedUserId,
            InvitationAcceptedOn = DateTime.UtcNow
        });
        context.UserPermissions.Add(new UserPermission
        {
            AccountId = AccountB, UserId = SharedUserId,
            PermissionId = AdminPermissionId
        });
        await context.SaveChangesAsync();
    }
}
