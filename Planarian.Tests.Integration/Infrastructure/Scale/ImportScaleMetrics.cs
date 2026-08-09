using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Planarian.Tests;

internal sealed record TimedSql(string Sql, TimeSpan Duration);

internal sealed class SqlTimingInterceptor : DbCommandInterceptor
{
    private readonly ConcurrentQueue<TimedSql> _items = new();

    public IReadOnlyList<TimedSql> Items => _items.ToArray();

    public void Reset()
    {
        while (_items.TryDequeue(out _)) { }
    }

    private void Add(DbCommand command, CommandExecutedEventData eventData) =>
        _items.Enqueue(new TimedSql(command.CommandText, eventData.Duration));

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result)
    {
        Add(command, eventData);
        return result;
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        Add(command, eventData);
        return ValueTask.FromResult(result);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Add(command, eventData);
        return result;
    }

    public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        Add(command, eventData);
        return ValueTask.FromResult(result);
    }
}

internal sealed class SaveMetricsInterceptor : SaveChangesInterceptor
{
    public int Saves { get; private set; }
    public int TrackedHighWater { get; private set; }

    public void Reset()
    {
        Saves = 0;
        TrackedHighWater = 0;
    }

    private void Record(DbContext? context)
    {
        Saves++;
        if (context is not null)
            TrackedHighWater = Math.Max(TrackedHighWater, context.ChangeTracker.Entries().Count());
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Record(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Context);
        return ValueTask.FromResult(result);
    }
}

internal sealed record CaveScaleMetrics(
    string Phase, int Rows, double ParseMs, double StateLoadMs, double PurePlanningMs, double ExecutionMs,
    int Inserts, int Updates, int NoChange, int Deletes, int Commands, int Selects, int WriteCommands,
    int RevisionWriteCommands, double WriteSqlMs, double RevisionSqlMs, int SaveChanges, int TrackedHighWater);

internal sealed record EntranceScaleMetrics(
    string Phase, int Rows, int Targets, double ParseMs, double StateLoadMs, double PurePlanningMs,
    double ExecutionMs, int Commands, int Selects, int WriteCommands, int RevisionWriteCommands,
    double WriteSqlMs, double RevisionSqlMs, int SaveChanges, int TrackedHighWater);
