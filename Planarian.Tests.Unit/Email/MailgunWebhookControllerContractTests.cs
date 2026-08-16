using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Planarian.Shared.Email.Controllers;
using Xunit;

namespace Planarian.Tests.Unit.Email;

public sealed class MailgunWebhookControllerContractTests
{
    [Fact]
    public void WebhookEndpointIsAnonymousPostWithAntiforgeryBypassAndBoundedBody()
    {
        var controller = typeof(MailgunWebhookController);
        var action = controller.GetMethod(nameof(MailgunWebhookController.Handle));
        var route = Assert.Single(controller.GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>());

        Assert.Equal("api/webhooks/mailgun", route.Template);
        Assert.NotNull(action);
        Assert.Single(action!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: false));
        Assert.Single(action.GetCustomAttributes(typeof(HttpPostAttribute), inherit: false));
        Assert.Single(action.GetCustomAttributes(typeof(IgnoreAntiforgeryTokenAttribute), inherit: false));

        var requestLimit = Assert.Single(
            action.GetCustomAttributes(typeof(RequestSizeLimitAttribute), inherit: false)
                .Cast<RequestSizeLimitAttribute>());
        Assert.Equal(64 * 1024, ((IRequestSizeLimitMetadata)requestLimit).MaxRequestBodySize);
    }
}
