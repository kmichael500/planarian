using Microsoft.AspNetCore.Mvc.Routing;
using Planarian.Modules.Users.Controllers;
using Planarian.Shared.Routing;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class UserEmailConfirmationRouteContractTests
{
    public static TheoryData<string, string, string> Actions => new()
    {
        { nameof(UserController.ConfirmEmail), UserEmailConfirmationRoutes.Api.Confirm, UserEmailConfirmationRoutes.Api.Names.Confirm },
        { nameof(UserController.ResendEmailConfirmation), UserEmailConfirmationRoutes.Api.Resend, UserEmailConfirmationRoutes.Api.Names.Resend }
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public void ApiActionsUseAuthoritativeRouteContracts(string actionName, string template, string routeName)
    {
        var attribute = GetRouteAttribute(actionName);

        Assert.Equal(template, attribute.Template);
        Assert.Equal(routeName, attribute.Name);
    }

    [Fact]
    public void ClientRouteBuildsAndEscapesConfirmationCode()
    {
        Assert.Equal("/confirm-email?code=code%2F%3F%23%26%3D",
            UserEmailConfirmationRoutes.Client.Get("code/?#&="));
    }

    private static IRouteTemplateProvider GetRouteAttribute(string actionName)
    {
        var action = typeof(UserController).GetMethod(actionName);
        Assert.NotNull(action);
        return Assert.Single(action!.GetCustomAttributes(inherit: false).OfType<IRouteTemplateProvider>());
    }
}
