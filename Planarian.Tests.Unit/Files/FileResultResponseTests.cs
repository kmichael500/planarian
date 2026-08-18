using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using Planarian.Library.Options;
using Planarian.Model.Database;
using Planarian.Model.Shared;
using Planarian.Modules.Authentication.Services;
using Planarian.Shared.Base;
using Planarian.Shared.Models;
using Xunit;

namespace Planarian.Tests.Unit.Files;

public sealed class FileResultResponseTests
{
    [Fact]
    public async Task OrdinaryFileResultPreservesFrameworkRangeAndConditionalRequestMetadata()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);
        var lastModified = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        var entityTag = EntityTagHeaderValue.Parse("\"file-etag\"");
        var response = CreateResponse(entityTag, lastModified);

        var actionResult = await controller.CreateResult(response);

        var fileResult = Assert.IsType<FileStreamResult>(actionResult);
        Assert.True(fileResult.EnableRangeProcessing);
        Assert.Equal(entityTag, fileResult.EntityTag);
        Assert.Equal(lastModified, fileResult.LastModified);
        Assert.Equal("private, no-cache", controller.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task PdfStreamSessionWithConditionalHeadersReturnsRangeCapableNoStoreResult()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);
        controller.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = Guid.NewGuid().ToString();
        controller.Request.Headers[HeaderNames.Range] = "bytes=0-1023";
        controller.Request.Headers[HeaderNames.IfNoneMatch] = "\"pdf-etag\"";
        var opened = false;
        var response = CreateResponse(
            EntityTagHeaderValue.Parse("\"pdf-etag\""),
            DateTimeOffset.UtcNow,
            () => opened = true);

        var actionResult = await controller.CreateResult(response);

        var fileResult = Assert.IsType<FileStreamResult>(actionResult);
        Assert.True(opened);
        Assert.True(fileResult.EnableRangeProcessing);
        Assert.Null(fileResult.EntityTag);
        Assert.Null(fileResult.LastModified);
        Assert.Equal("private, no-store", controller.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    [Fact]
    public async Task InvalidStreamSessionHeaderKeepsOrdinaryFileResponseBehavior()
    {
        using var db = CreateDbContext();
        var controller = CreateController(db);
        controller.Request.Headers[RequestThrottleService.FileStreamSessionHeaderName] = "not-a-guid";
        var entityTag = EntityTagHeaderValue.Parse("\"file-etag\"");
        var response = CreateResponse(entityTag, null);

        var actionResult = await controller.CreateResult(response);

        var fileResult = Assert.IsType<FileStreamResult>(actionResult);
        Assert.Equal(entityTag, fileResult.EntityTag);
        Assert.Equal("private, no-cache", controller.Response.Headers[HeaderNames.CacheControl].ToString());
    }

    private static AuthenticatedFileResponse CreateResponse(
        EntityTagHeaderValue? entityTag,
        DateTimeOffset? lastModified,
        Action? onOpen = null) =>
        new()
        {
            OpenReadStreamAsync = _ =>
            {
                onOpen?.Invoke();
                return Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
            },
            ContentType = "application/pdf",
            EntityTag = entityTag,
            LastModified = lastModified
        };

    private static PlanarianDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<PlanarianDbContext>().Options);

    private static TestController CreateController(PlanarianDbContext db) =>
        new(
            new RequestUser(db),
            new TokenService(new AuthOptions
            {
                JwtSecret = new string('x', 32),
                JwtIssuer = "planarian-tests"
            }));

    private sealed class TestController : PlanarianControllerBase
    {
        public TestController(RequestUser requestUser, TokenService tokenService) : base(requestUser, tokenService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            };
        }

        public Task<IActionResult> CreateResult(AuthenticatedFileResponse response) => CreateFileResult(response);
    }
}
