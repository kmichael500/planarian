using Planarian.Library.Exceptions;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Modules.Settings.Services;
using Xunit;

namespace Planarian.Tests.Unit.Settings;

public sealed class SettingsPermissionFilterPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(PermissionKey.View)]
    [InlineData(PermissionKey.Manager)]
    public void SupportedFiltersAreAccepted(string? permissionKey)
    {
        SettingsPermissionFilterPolicy.Validate(permissionKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("view")]
    [InlineData(PermissionKey.Admin)]
    [InlineData(PermissionPolicyKey.Export)]
    [InlineData("UnknownPermission")]
    public void UnsupportedFiltersFailClosed(string permissionKey)
    {
        var exception = Assert.Throws<ApiException>(() => SettingsPermissionFilterPolicy.Validate(permissionKey));

        Assert.Equal(ApiExceptionType.BadRequest, exception.ErrorCode);
    }
}
