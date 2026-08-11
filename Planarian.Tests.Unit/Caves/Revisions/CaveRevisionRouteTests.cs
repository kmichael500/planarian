using Microsoft.AspNetCore.Mvc;
using Planarian.Modules.Caves.Controllers;
using Xunit;

namespace Planarian.Tests.Caves.Revisions;

public sealed class CaveRevisionRouteTests
{
    [Fact]
    public void RevisionDetailRouteAcceptsGeneratedRevisionIds()
    {
        var action = typeof(CaveController).GetMethod(nameof(CaveController.GetRevision));
        var route = Assert.Single(action!.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>());

        Assert.Equal("{caveId:length(10)}/revisions/{revisionId:length(10)}", route.Template);
    }
}
