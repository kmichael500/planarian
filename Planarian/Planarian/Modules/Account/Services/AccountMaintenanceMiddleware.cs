using Planarian.Library.Exceptions;
using Planarian.Model.Shared;
using Planarian.Modules.Account.Repositories;

namespace Planarian.Modules.Account.Services;

/// <summary>
/// A shared PostgreSQL lease spans each account-scoped mutating HTTP request,
/// including its writes to external storage. Reset takes the exclusive lease
/// and fails closed if another request or background archive is still active.
/// </summary>
public sealed class AccountMaintenanceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, RequestUser requestUser,
        AccountMaintenanceLockRepository maintenance,
        AccountRepository accountRepository)
    {
        var accountId = requestUser.AccountId;
        var path = context.Request.Path;
        if (string.IsNullOrWhiteSpace(accountId) ||
            !(HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) ||
              HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method)) ||
            path.Equals("/api/account/reset", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/account/reset/cleanup", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        await using var lease = await maintenance.TryAcquireAsync(accountId, exclusive: false,
            context.RequestAborted);
        if (lease is null)
            throw ApiExceptionDictionary.Conflict("Account maintenance is in progress. Retry after it finishes.");

        // A failed post-commit delete cannot be allowed to delete newly uploaded bytes
        // during a later retry. Until cleanup debt is cleared, reject new writes.
        if (await accountRepository.HasPendingCleanupAsync(context.RequestAborted))
            throw ApiExceptionDictionary.Conflict(
                "Account reset storage cleanup is incomplete. Retry cleanup before modifying this account.");

        await next(context);
    }
}
