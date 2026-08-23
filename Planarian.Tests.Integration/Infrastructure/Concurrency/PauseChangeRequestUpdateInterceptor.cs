using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Planarian.Tests.Integration.Infrastructure.Concurrency;

internal sealed class PauseChangeRequestUpdateInterceptor : DbCommandInterceptor
{
    public TaskCompletionSource ChangeRequestUpdateReached { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ContinueUpdate { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await PauseIfChangeRequestUpdateAsync(command, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await PauseIfChangeRequestUpdateAsync(command, cancellationToken);
        return result;
    }

    private async Task PauseIfChangeRequestUpdateAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (!command.CommandText.Contains("UPDATE \"CaveChangeRequests\"", StringComparison.Ordinal)) return;
        ChangeRequestUpdateReached.TrySetResult();
        await ContinueUpdate.Task.WaitAsync(cancellationToken);
    }
}
