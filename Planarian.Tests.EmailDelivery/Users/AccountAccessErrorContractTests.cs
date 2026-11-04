using Microsoft.AspNetCore.Http;
using Planarian.Library.Exceptions;
using Planarian.Tests.EmailDelivery.TestInfrastructure;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Users;

public sealed class AccountAccessErrorContractTests
{
    [Fact]
    public void InvalidAccountSelectionIsForbiddenWithoutInvalidatingAuthentication()
    {
        var error = ApiExceptionDictionary.InvalidAccountSelection;

        Assert.Equal(StatusCodes.Status403Forbidden, error.StatusCode);
        Assert.Equal(ApiExceptionType.InvalidAccountSelection, error.ErrorCode);
        Assert.Equal(304, (int)error.ErrorCode);
    }

    [Fact]
    public void TypeScriptSharesTheBackendAlreadyMemberWireName()
    {
        var file = Path.Combine(RepositoryPaths.Root, "Planarian.Web", "src",
            "Shared", "Models", "ApiErrorResponse.ts");
        var frontendType = File.ReadAllText(file);

        Assert.Contains(
            $"{nameof(ApiExceptionType.UserAlreadyInAccount)} = \"{nameof(ApiExceptionType.UserAlreadyInAccount)}\"",
            frontendType);
    }

    [Fact]
    public void TypeScriptSharesTheBackendInvalidAccountSelectionWireName()
    {
        var file = Path.Combine(RepositoryPaths.Root, "Planarian.Web", "src",
            "Shared", "Models", "ApiErrorResponse.ts");
        var frontendType = File.ReadAllText(file);

        Assert.Contains(
            $"{nameof(ApiExceptionType.InvalidAccountSelection)} = \"{nameof(ApiExceptionType.InvalidAccountSelection)}\"",
            frontendType);
    }
}
