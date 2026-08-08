namespace Planarian.Model.Shared;

/// <summary>
/// Immutable tenant scope derived only from the authenticated request user.
/// Callers cannot construct account-scoped work from an imported or posted ID.
/// </summary>
public sealed record AccountExecutionScope(string AccountId, string UserId)
{
    public static AccountExecutionScope Require(RequestUser requestUser)
    {
        if (requestUser is null || string.IsNullOrWhiteSpace(requestUser.AccountId))
            throw new InvalidOperationException("An authenticated account context is required.");
        if (string.IsNullOrWhiteSpace(requestUser.Id))
            throw new InvalidOperationException("An authenticated user context is required.");
        return new AccountExecutionScope(requestUser.AccountId, requestUser.Id);
    }
}
