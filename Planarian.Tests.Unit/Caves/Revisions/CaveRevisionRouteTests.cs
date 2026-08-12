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

    [Fact]
    public void AuthoringContextUsesCommandShapedPostRoute()
    {
        var action = typeof(CaveChangeRequestController)
            .GetMethod(nameof(CaveChangeRequestController.InitializeAuthoringContext));
        var route = Assert.Single(action!.GetCustomAttributes(typeof(HttpPostAttribute), inherit: false)
            .Cast<HttpPostAttribute>());

        Assert.Equal("caves/{caveId:length(10)}/authoring-context", route.Template);
        Assert.Empty(action.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false));
    }

    [Fact]
    public void ProposalVersionReadRouteAcceptsGeneratedIds()
    {
        var action = typeof(CaveChangeRequestController).GetMethod(nameof(CaveChangeRequestController.GetVersion));
        var route = Assert.Single(action!.GetCustomAttributes(typeof(HttpGetAttribute), inherit: false)
            .Cast<HttpGetAttribute>());

        Assert.Equal("{requestId:length(10)}/versions/{versionId:length(10)}", route.Template);
    }
}
