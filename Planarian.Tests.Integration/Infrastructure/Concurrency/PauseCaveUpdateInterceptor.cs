using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Planarian.Tests.Integration.Infrastructure.Concurrency;

internal sealed class PauseCaveUpdateInterceptor : DbCommandInterceptor
{
    public TaskCompletionSource CaveUpdateReached { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ContinueUpdate { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        await PauseIfCaveUpdateAsync(command, cancellationToken);
        return result;
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        await PauseIfCaveUpdateAsync(command, cancellationToken);
        return result;
    }

    private async Task PauseIfCaveUpdateAsync(DbCommand command, CancellationToken cancellationToken)
    {
        if (!command.CommandText.Contains("UPDATE \"Caves\"", StringComparison.Ordinal)) return;
        CaveUpdateReached.TrySetResult();
        await ContinueUpdate.Task.WaitAsync(cancellationToken);
    }
}
