using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Planarian.Shared.Base;
using Planarian.Shared.Models;
using Planarian.Shared.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Security;

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
