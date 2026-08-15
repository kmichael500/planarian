using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Planarian.Modules.Users.Controllers;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Architecture;

public sealed class ApiRouteNameContractTests
{
    [Fact]
    public void NamedControllerEndpointNamesAreUnique()
    {
        var names = typeof(UserController).Assembly
            .GetTypes()
            .Where(type => typeof(ControllerBase).IsAssignableFrom(type))
            .SelectMany(type => type.GetMethods())
            .SelectMany(method => method.GetCustomAttributes(inherit: false).OfType<IRouteTemplateProvider>())
            .Select(attribute => attribute.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Assert.Equal(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }
}
