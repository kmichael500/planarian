using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Account.Controller;
using Xunit;

namespace Planarian.Tests.Unit.Account;

public sealed class ArchiveAuthorizationContractTests
{
    public static TheoryData<string> ArchiveActions => new()
    {
        nameof(AccountController.StartArchive),
        nameof(AccountController.CancelArchive),
        nameof(AccountController.GetArchiveStatus),
        nameof(AccountController.GetRecentArchives),
        nameof(AccountController.DownloadArchive),
        nameof(AccountController.DeleteArchive)
    };

    [Theory]
    [MemberData(nameof(ArchiveActions))]
    public void EveryArchiveEndpointRequiresAccountAdminPolicy(string actionName)
    {
        var action = typeof(AccountController).GetMethod(
            actionName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(action);
        var policies = action!.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Select(attribute => attribute.Policy)
            .ToArray();
        Assert.Contains(PermissionPolicyKey.Admin, policies);
    }
}
