using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Planarian.Model.Shared;
using Planarian.Modules.Users.Controllers;
using Planarian.Shared.Routing;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Email;

public sealed class UserInvitationRouteContractTests
{
    public static TheoryData<string, string, string> InvitationActions => new()
    {
        { nameof(UserController.GetPendingInvitations), UserInvitationRoutes.Api.Pending, UserInvitationRoutes.Api.Names.Pending },
        { nameof(UserController.GetInvitation), UserInvitationRoutes.Api.ByCode, UserInvitationRoutes.Api.Names.Get },
        { nameof(UserController.AcceptInvitation), UserInvitationRoutes.Api.Accept, UserInvitationRoutes.Api.Names.Accept },
        { nameof(UserController.DeclineInvitation), UserInvitationRoutes.Api.Decline, UserInvitationRoutes.Api.Names.Decline }
    };

    [Theory]
    [MemberData(nameof(InvitationActions))]
    public void InvitationApiActionsUseAuthoritativeRouteContracts(string actionName, string template, string routeName)
    {
        var attribute = GetRouteAttribute(actionName);

        Assert.Equal(template, attribute.Template);
        Assert.Equal(routeName, attribute.Name);
    }


    [Fact]
    public void InvitationCodeConstraintUsesDomainLengthSourceOfTruth()
    {
        var constraint = new InvitationCodeRouteConstraint();

        Assert.Equal(PropertyLength.InvitationCode, constraint.MinLength);
        Assert.Equal(PropertyLength.InvitationCode, constraint.MaxLength);
    }

    [Theory]
    [InlineData("Ab3xZ91Qwe", true)]
    [InlineData("1234567890", true)]
    [InlineData("abcdefghi", false)]
    [InlineData("abcdefghijk", false)]
    public void InvitationCodeConstraintPreservesFixedLengthAlphanumericRouting(string code, bool expected)
    {
        var constraint = new InvitationCodeRouteConstraint();
        var values = new RouteValueDictionary { [UserInvitationRoutes.CodeParameter] = code };

        var matches = constraint.Match(
            new DefaultHttpContext(),
            null!,
            UserInvitationRoutes.CodeParameter,
            values,
            RouteDirection.IncomingRequest);

        Assert.Equal(expected, matches);
    }


    private static IRouteTemplateProvider GetRouteAttribute(string actionName)
    {
        var action = typeof(UserController).GetMethod(actionName);
        Assert.NotNull(action);
        return Assert.Single(action!.GetCustomAttributes(inherit: false).OfType<IRouteTemplateProvider>());
    }
}
