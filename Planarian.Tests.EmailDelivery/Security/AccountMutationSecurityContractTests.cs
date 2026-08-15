using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planarian.Modules.Users.Controllers;
using Planarian.Modules.Users.Models;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Security;

public sealed class AccountMutationSecurityContractTests
{
    [Fact]
    public void SettingsPasswordChangeRequiresCurrentAndNewPassword()
    {
        var method = typeof(UserController).GetMethod(nameof(UserController.UpdateCurrentUserPassword))!;
        var body = method.GetParameters().Single(p => p.GetCustomAttribute<FromBodyAttribute>() != null);

        Assert.Equal(typeof(UpdateCurrentUserPasswordVm), body.ParameterType);
        AssertRequiredStringProperty<UpdateCurrentUserPasswordVm>(nameof(UpdateCurrentUserPasswordVm.CurrentPassword));
        AssertRequiredStringProperty<UpdateCurrentUserPasswordVm>(nameof(UpdateCurrentUserPasswordVm.Password));
    }

    [Fact]
    public void ProfileUpdateAllowsCurrentPasswordOnlyWhenReauthenticationIsNeeded()
    {
        var method = typeof(UserController).GetMethod(nameof(UserController.UpdateCurrentUser))!;
        var body = method.GetParameters().Single(p => p.GetCustomAttribute<FromBodyAttribute>() != null);
        var currentPassword = typeof(UpdateCurrentUserVm).GetProperty(nameof(UpdateCurrentUserVm.CurrentPassword));

        Assert.Equal(typeof(UpdateCurrentUserVm), body.ParameterType);
        Assert.NotNull(currentPassword);
        Assert.Equal(typeof(string), currentPassword!.PropertyType);
        Assert.Empty(currentPassword.GetCustomAttributes<RequiredAttribute>());
    }

    [Fact]
    public void ForgotPasswordResetRemainsAnonymousAndDoesNotRequireCurrentPassword()
    {
        var method = typeof(UserController).GetMethod(nameof(UserController.ResetPassword))!;
        var body = method.GetParameters().Single(p => p.GetCustomAttribute<FromBodyAttribute>() != null);

        Assert.NotNull(method.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(typeof(string), body.ParameterType);
    }

    private static void AssertRequiredStringProperty<T>(string propertyName)
    {
        var property = typeof(T).GetProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(typeof(string), property!.PropertyType);
        Assert.Single(property.GetCustomAttributes<RequiredAttribute>());
    }
}
