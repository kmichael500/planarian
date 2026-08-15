using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Planarian.Modules.App.Controllers;
using Planarian.Modules.Authentication.Controllers;
using Planarian.Modules.Register.Controllers;
using Planarian.Modules.Users.Controllers;
using Planarian.Shared.Email.Controllers;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Security;

public sealed class ControllerSecurityContractTests
{
    private static readonly HashSet<string> AntiforgeryBypassAllowlist =
    [
        Endpoint<AuthenticationController>(nameof(AuthenticationController.Token)),
        Endpoint<MailgunWebhookController>(nameof(MailgunWebhookController.Handle))
    ];

    private static readonly HashSet<string> AnonymousEndpointAllowlist =
    [
        Endpoint<AppController>(nameof(AppController.Initialize)),
        Endpoint<AuthenticationController>(nameof(AuthenticationController.Login)),
        Endpoint<AuthenticationController>(nameof(AuthenticationController.Token)),
        Endpoint<RegisterController>(nameof(RegisterController.Register)),
        Endpoint<UserController>(nameof(UserController.ConfirmEmail)),
        Endpoint<UserController>(nameof(UserController.ResendEmailConfirmation)),
        Endpoint<UserController>(nameof(UserController.DeclineInvitation)),
        Endpoint<UserController>(nameof(UserController.GetInvitation)),
        Endpoint<UserController>(nameof(UserController.SendPasswordReset)),
        Endpoint<UserController>(nameof(UserController.ResetPassword)),
        Endpoint<MailgunWebhookController>(nameof(MailgunWebhookController.Handle))
    ];

    [Fact]
    public void AntiforgeryBypassesAreLimitedToTheExplicitAllowlist()
    {
        var actual = GetControllerActions()
            .Where(endpoint => HasMetadata<IgnoreAntiforgeryTokenAttribute>(endpoint.Controller, endpoint.Action))
            .Select(endpoint => Endpoint(endpoint.Controller, endpoint.Action))
            .OrderBy(endpoint => endpoint)
            .ToArray();

        Assert.Equal(AntiforgeryBypassAllowlist.OrderBy(endpoint => endpoint), actual);
    }

    [Fact]
    public void EveryControllerEndpointIsAuthorizedOrExplicitlyAllowlistedAsAnonymous()
    {
        var endpoints = GetControllerActions().ToArray();
        var endpointIds = endpoints.Select(endpoint => Endpoint(endpoint.Controller, endpoint.Action)).ToHashSet();
        var failures = new List<string>();
        foreach (var missing in AnonymousEndpointAllowlist.Except(endpointIds).OrderBy(endpoint => endpoint))
        {
            failures.Add($"Allowlisted anonymous endpoint no longer exists: {missing}");
        }

        foreach (var endpoint in endpoints)
        {
            var endpointId = Endpoint(endpoint.Controller, endpoint.Action);
            var isAllowlistedAnonymous = AnonymousEndpointAllowlist.Contains(endpointId);
            var allowsAnonymous = HasMetadata<IAllowAnonymous>(endpoint.Controller, endpoint.Action);
            var requiresAuthorization = HasMetadata<IAuthorizeData>(endpoint.Controller, endpoint.Action);

            if (isAllowlistedAnonymous)
            {
                if (!allowsAnonymous)
                    failures.Add($"{endpointId} is intentionally public but is not explicitly marked [AllowAnonymous].");
                continue;
            }

            if (allowsAnonymous)
                failures.Add($"{endpointId} is [AllowAnonymous] but is not in the explicit anonymous allowlist.");
            else if (!requiresAuthorization)
                failures.Add($"{endpointId} has no effective [Authorize] requirement.");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
    private static IEnumerable<(Type Controller, MethodInfo Action)> GetControllerActions()
    {
        return typeof(AuthenticationController).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Where(action => action.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>().Any())
                .Select(action => (Controller: controller, Action: action)));
    }

    private static bool HasMetadata<T>(Type controller, MethodInfo action)
    {
        return controller.GetCustomAttributes(inherit: true).OfType<T>().Any()
               || action.GetCustomAttributes(inherit: true).OfType<T>().Any();
    }

    private static string Endpoint<TController>(string actionName)
    {
        return $"{typeof(TController).FullName}.{actionName}";
    }

    private static string Endpoint(Type controller, MethodInfo action)
    {
        return $"{controller.FullName}.{action.Name}";
    }
}
