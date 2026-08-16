using Microsoft.AspNetCore.Mvc.Routing;
using Planarian.Modules.Users.Controllers;
using Planarian.Shared.Routing;
using Xunit;

namespace Planarian.Tests.Unit.Users;

public sealed class UserPasswordResetRouteContractTests
{
    public static TheoryData<string, string, string> Actions => new()
    {
        { nameof(UserController.SendPasswordReset), UserPasswordResetRoutes.Api.SendEmail, UserPasswordResetRoutes.Api.Names.SendEmail },
        { nameof(UserController.ResetPassword), UserPasswordResetRoutes.Api.Reset, UserPasswordResetRoutes.Api.Names.Reset }
    };

    [Theory]
    [MemberData(nameof(Actions))]
    public void ApiActionsUseAuthoritativeRouteContracts(string actionName, string template, string routeName)
    {
        var attribute = GetRouteAttribute(actionName);

        Assert.Equal(template, attribute.Template);
        Assert.Equal(routeName, attribute.Name);
    }


    private static IRouteTemplateProvider GetRouteAttribute(string actionName)
    {
        var action = typeof(UserController).GetMethod(actionName);
        Assert.NotNull(action);
        return Assert.Single(action!.GetCustomAttributes(inherit: false).OfType<IRouteTemplateProvider>());
    }
}
