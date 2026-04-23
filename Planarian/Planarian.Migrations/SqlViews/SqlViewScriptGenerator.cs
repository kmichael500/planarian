using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Planarian.Model.Shared.Base;

namespace Planarian.Migrations.SqlViews;

internal static class SqlViewScriptGenerator
{
    public static IReadOnlyList<SqlViewInfo> GetChangedViews()
    {
        var currentViews = GetCurrentSqlViews();
        var snapshotViews = GetSnapshotSqlViews();
        var viewKeys = currentViews.Keys
            .Union(snapshotViews.Keys)
            .OrderBy(key =>
                currentViews.TryGetValue(key, out var current)
                    ? current.ViewName
                    : snapshotViews[key].ViewName);

        var changedViews = new List<SqlViewInfo>();
        foreach (var key in viewKeys)
        {
            currentViews.TryGetValue(key, out var current);
            snapshotViews.TryGetValue(key, out var snapshot);

            if (string.Equals(snapshot?.Hash, current?.Hash, StringComparison.Ordinal))
            {
                continue;
            }

            var view = new SqlViewInfo(
                key,
                current?.ViewName ?? snapshot?.ViewName ?? key,
                current?.Sql,
                snapshot?.Sql);

            ValidateSqlPair(view);
            changedViews.Add(view);
        }

        return changedViews;
    }

    public static string GenerateUpSql(IReadOnlyList<SqlViewInfo> changedViews)
    {
        var builder = new StringBuilder();

        foreach (var view in changedViews)
        {
            builder.AppendLine($"// {view.ViewName}");
            AppendSqlOrDrop(builder, view.ViewName, view.CurrentSql, "current SQL");
        }

        return builder.ToString();
    }

    public static string GenerateDownSql(IReadOnlyList<SqlViewInfo> changedViews)
    {
        var builder = new StringBuilder();

        foreach (var view in changedViews.Reverse())
        {
            builder.AppendLine($"// {view.ViewName}");
            AppendSqlOrDrop(builder, view.ViewName, view.PreviousSql, "snapshot SQL");
        }

        return builder.ToString();
    }

    private static Dictionary<string, CurrentSqlViewInfo> GetCurrentSqlViews()
    {
        return typeof(ViewBase).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ViewBase).IsAssignableFrom(type))
            .Select(type =>
            {
                return new CurrentSqlViewInfo(
                    type.FullName ?? type.Name,
                    ViewBase.GetViewName(type),
                    ViewBase.GetSqlHash(type),
                    ViewBase.ReadSqlResource(type));
            })
            .ToDictionary(view => view.ClrTypeName);
    }

    private static Dictionary<string, SnapshotSqlViewInfo> GetSnapshotSqlViews()
    {
        var snapshot = GetModelSnapshot();
        if (snapshot == null)
        {
            return new Dictionary<string, SnapshotSqlViewInfo>();
        }

        var views = new Dictionary<string, SnapshotSqlViewInfo>();

        foreach (var entityType in snapshot.Model.GetEntityTypes())
        {
            var viewName = entityType.GetViewName();
            if (string.IsNullOrWhiteSpace(viewName))
            {
                continue;
            }

            var clrTypeName = entityType.FindAnnotation(ViewBase.SqlViewClrTypeAnnotation)?.Value as string;
            var hash = entityType.FindAnnotation(ViewBase.SqlViewHashAnnotation)?.Value as string;
            var sql = entityType.FindAnnotation(ViewBase.SqlViewSqlAnnotation)?.Value as string;
            var annotatedViewName = entityType.FindAnnotation(ViewBase.SqlViewViewNameAnnotation)?.Value as string;

            if (string.IsNullOrWhiteSpace(clrTypeName))
            {
                clrTypeName = entityType.Name;
            }

            if (string.IsNullOrWhiteSpace(hash) &&
                string.IsNullOrWhiteSpace(sql) &&
                string.IsNullOrWhiteSpace(annotatedViewName))
            {
                continue;
            }

            views[clrTypeName] = new SnapshotSqlViewInfo(clrTypeName, annotatedViewName ?? viewName, hash, sql);
        }

        return views;
    }

    private static ModelSnapshot? GetModelSnapshot()
    {
        var snapshotType = typeof(SqlViewScriptGenerator).Assembly.GetTypes()
            .SingleOrDefault(type => !type.IsAbstract && typeof(ModelSnapshot).IsAssignableFrom(type));

        if (snapshotType == null)
        {
            return null;
        }

        return (ModelSnapshot?)Activator.CreateInstance(snapshotType, nonPublic: true);
    }

    private static void ValidateSqlPair(SqlViewInfo view)
    {
        if (!string.IsNullOrWhiteSpace(view.CurrentSql) || !string.IsNullOrWhiteSpace(view.PreviousSql))
        {
            return;
        }

        throw new InvalidOperationException(
            $"SQL view '{view.ViewName}' has no current SQL and no snapshot SQL. " +
            "At least one side must contain SQL so the migration can create, restore, or drop the view.");
    }

    private static void AppendSqlOrDrop(StringBuilder builder, string viewName, string? sql, string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            AppendSql(builder, $@"DROP VIEW IF EXISTS ""{viewName}"";", validateCreateView: false, sourceName);
            return;
        }

        AppendSql(builder, $@"DROP VIEW IF EXISTS ""{viewName}"";", validateCreateView: false, sourceName);
        AppendSql(builder, sql, validateCreateView: true, sourceName);
    }

    private static void AppendSql(StringBuilder builder, string sql, bool validateCreateView, string sourceName)
    {
        sql = sql.TrimEnd();
        if (validateCreateView && !StartsWithCreateOrReplaceView(sql))
        {
            throw new InvalidOperationException(
                $"SQL view {sourceName} must contain migration-ready SQL that starts with CREATE OR REPLACE VIEW.");
        }

        var delimiter = GetRawStringDelimiter(sql);
        builder.Append("migrationBuilder.Sql(").AppendLine(delimiter);
        builder.AppendLine(sql);
        builder.Append(delimiter).AppendLine(");");
        builder.AppendLine();
    }

    private static bool StartsWithCreateOrReplaceView(string sql)
    {
        var normalizedSql = Regex.Replace(sql.TrimStart(), @"\s+", " ");
        return normalizedSql.StartsWith("CREATE OR REPLACE VIEW", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetRawStringDelimiter(string value)
    {
        var maxQuotes = 0;
        var currentQuotes = 0;

        foreach (var character in value)
        {
            if (character == '"')
            {
                currentQuotes++;
                maxQuotes = Math.Max(maxQuotes, currentQuotes);
                continue;
            }

            currentQuotes = 0;
        }

        return new string('"', Math.Max(3, maxQuotes + 1));
    }
}

internal sealed record SqlViewInfo(string ClrTypeName, string ViewName, string? CurrentSql, string? PreviousSql);

internal sealed record CurrentSqlViewInfo(string ClrTypeName, string ViewName, string Hash, string Sql);

internal sealed record SnapshotSqlViewInfo(string ClrTypeName, string ViewName, string? Hash, string? Sql);
