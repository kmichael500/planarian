using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Base;
using Planarian.Shared.Models;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Files;

public sealed class FileResponseSecurityTests
{
    [Theory]
    [InlineData("payload.html")]
    [InlineData("payload.htm")]
    [InlineData("payload.svg")]
    [InlineData("payload.xml")]
    [InlineData("payload.xhtml")]
    [InlineData("payload.js")]
    [InlineData("payload.unknown")]
    public void ActiveOrUnknownContentIsForcedToDownload(string fileName)
    {
        var result = Resolve(fileName, "text/html", requestedDownload: false);

        Assert.Equal("application/octet-stream", result.ContentType);
        Assert.True(result.ForceDownload);
    }

    [Theory]
    [InlineData("data.txt", "text/plain; charset=utf-8")]
    [InlineData("data.csv", "text/plain; charset=utf-8")]
    [InlineData("data.json", "text/plain; charset=utf-8")]
    [InlineData("data.geojson", "text/plain; charset=utf-8")]
    [InlineData("data.kml", "text/plain; charset=utf-8")]
    [InlineData("data.gpx", "text/plain; charset=utf-8")]
    [InlineData("data.plt", "text/plain; charset=utf-8")]
    [InlineData("shapes.zip", "application/octet-stream")]
    public void ClientParsedContentCannotBecomeExecutableBrowserContent(string fileName, string expectedContentType)
    {
        var result = Resolve(fileName, "text/html", requestedDownload: false);

        Assert.Equal(expectedContentType, result.ContentType);
        Assert.False(result.ForceDownload);
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "image/jpeg")]
    [InlineData("photo.png", "image/png")]
    [InlineData("photo.gif", "image/gif")]
    [InlineData("document.pdf", "application/pdf")]
    public void OnlyExpectedPassiveFormatsMayRenderInline(string fileName, string expectedContentType)
    {
        var result = Resolve(fileName, "application/octet-stream", requestedDownload: false);

        Assert.Equal(expectedContentType, result.ContentType);
        Assert.False(result.ForceDownload);
    }

    [Fact]
    public void ExplicitDownloadAlwaysWins()
    {
        var result = Resolve("photo.jpg", "image/jpeg", requestedDownload: true);

        Assert.Equal("image/jpeg", result.ContentType);
        Assert.True(result.ForceDownload);
    }

    [Fact]
    public async Task FileResultsAlwaysDisableContentTypeSniffingAndHonorAttachmentDisposition()
    {
        var controller = new TestController();
        var response = new AuthenticatedFileResponse
        {
            OpenReadStreamAsync = _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])),
            ContentType = "application/octet-stream",
            FileName = "payload.svg",
            Download = true
        };

        var actionResult = await controller.CreateResult(response);

        Assert.Equal("nosniff", controller.Response.Headers["X-Content-Type-Options"].ToString());
        var fileResult = Assert.IsType<FileStreamResult>(actionResult);
        Assert.Equal("payload.svg", fileResult.FileDownloadName);
        Assert.Equal("application/octet-stream", fileResult.ContentType);
    }

    [Fact]
    public async Task OrdinaryFileResultPreservesValidatorsAndEnablesRanges()
    {
        var controller = new TestController();
        var lastModified = new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero);
        var entityTag = EntityTagHeaderValue.Parse("\"file-etag\"");
        var response = new AuthenticatedFileResponse
        {
            OpenReadStreamAsync = _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])),
            ContentType = "application/pdf",
            EntityTag = entityTag,
            LastModified = lastModified
        };

        var result = Assert.IsType<FileStreamResult>(await controller.CreateResult(response));

        Assert.True(result.EnableRangeProcessing);
        Assert.Equal(entityTag, result.EntityTag);
        Assert.Equal(lastModified, result.LastModified);
        Assert.Equal("private, no-cache", controller.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task OrdinaryConditionalGetStillReturnsNotModified()
    {
        var controller = new TestController();
        controller.HttpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddMvcCore()
            .Services
            .BuildServiceProvider();
        controller.Request.Method = HttpMethods.Get;
        controller.Request.Headers[HeaderNames.IfNoneMatch] = "\"file-etag\"";
        controller.Response.Body = new MemoryStream();
        var response = new AuthenticatedFileResponse
        {
            OpenReadStreamAsync = _ => Task.FromResult<Stream>(new MemoryStream([10, 20, 30, 40])),
            ContentType = "application/pdf",
            EntityTag = EntityTagHeaderValue.Parse("\"file-etag\"")
        };

        var result = Assert.IsType<FileStreamResult>(await controller.CreateResult(response));
        await result.ExecuteResultAsync(controller.ControllerContext);

        Assert.Equal(StatusCodes.Status304NotModified, controller.Response.StatusCode);
        Assert.Equal(0, controller.Response.Body.Length);
    }

    [Fact]
    public async Task RangeRequestExecutesAsPartialContentWithCorrectBytes()
    {
        var controller = new TestController();
        controller.HttpContext.RequestServices = new ServiceCollection()
            .AddLogging()
            .AddMvcCore()
            .Services
            .BuildServiceProvider();
        controller.Request.Method = HttpMethods.Get;
        controller.Request.Headers[HeaderNames.Range] = "bytes=1-2";
        controller.Response.Body = new MemoryStream();
        var response = new AuthenticatedFileResponse
        {
            OpenReadStreamAsync = _ => Task.FromResult<Stream>(new MemoryStream([10, 20, 30, 40])),
            ContentType = "application/pdf"
        };

        var result = Assert.IsType<FileStreamResult>(await controller.CreateResult(response));
        await result.ExecuteResultAsync(controller.ControllerContext);

        Assert.Equal(StatusCodes.Status206PartialContent, controller.Response.StatusCode);
        Assert.Equal("bytes", controller.Response.Headers[HeaderNames.AcceptRanges].ToString());
        Assert.Equal("bytes 1-2/4", controller.Response.Headers[HeaderNames.ContentRange].ToString());
        controller.Response.Body.Position = 0;
        Assert.Equal([20, 30], ((MemoryStream)controller.Response.Body).ToArray());
    }

    [Fact]
    public async Task PdfStreamSessionUsesNoStoreAndOmitsConditionalValidators()
    {
        var controller = new TestController();
        controller.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = Guid.NewGuid().ToString();
        controller.Request.Headers[HeaderNames.Range] = "bytes=0-1023";
        var response = new AuthenticatedFileResponse
        {
            OpenReadStreamAsync = _ => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])),
            ContentType = "application/pdf",
            EntityTag = EntityTagHeaderValue.Parse("\"pdf-etag\""),
            LastModified = DateTimeOffset.UtcNow
        };

        var result = Assert.IsType<FileStreamResult>(await controller.CreateResult(response));

        Assert.True(result.EnableRangeProcessing);
        Assert.Null(result.EntityTag);
        Assert.Null(result.LastModified);
        Assert.Equal("private, no-store", controller.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    private static (string ContentType, bool ForceDownload) Resolve(
        string fileName,
        string sourceContentType,
        bool requestedDownload)
    {
        var policyType = typeof(BlobService).Assembly.GetType(
            "Planarian.Shared.Services.FileResponsePolicy",
            throwOnError: true)!;
        var resolveMethod = policyType.GetMethod(
            "Resolve",
            BindingFlags.Public | BindingFlags.Static)!;

        return ((string ContentType, bool ForceDownload))resolveMethod.Invoke(
            null,
            [fileName, sourceContentType, requestedDownload])!;
    }

    private sealed class TestController : PlanarianControllerBase
    {
        public TestController() : base(null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        public Task<IActionResult> CreateResult(AuthenticatedFileResponse response) => CreateFileResult(response);
    }
}
