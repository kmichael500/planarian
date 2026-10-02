using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Planarian.Library.Options;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Infrastructure;

public sealed class HttpResponseExceptionMiddlewareTests
{
    [Fact]
    public async Task UnexpectedExceptionReturnsInternalServerError()
    {
        var middleware = new HttpResponseExceptionMiddleware(
            _ => throw new InvalidOperationException("boom"),
            new ServerOptions(),
            NullLogger<HttpResponseExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
    }
}
