using System.Text;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Planarian.Model.Shared.Base;

namespace Planarian.Migrations.SqlScripts;

internal static class SqlMigrationScriptGenerator
{
    public static IReadOnlyList<SqlMigrationScriptInfo> GetChangedScripts()
    {
        var currentScripts = SqlMigrationScriptRegistry.GetCurrentScripts();
        var snapshotScripts = GetSnapshotScripts();
        var scriptNames = currentScripts.Keys
            .Union(snapshotScripts.Keys)
            .OrderBy(name => name, StringComparer.Ordinal);

        var changedScripts = new List<SqlMigrationScriptInfo>();
        foreach (var name in scriptNames)
        {
            currentScripts.TryGetValue(name, out var current);
            snapshotScripts.TryGetValue(name, out var snapshot);

            if (string.Equals(snapshot?.Hash, current?.Hash, StringComparison.Ordinal))
            {
                continue;
            }

            changedScripts.Add(new SqlMigrationScriptInfo(
                name,
                current?.UpSql,
                current?.DownSql,
                snapshot?.UpSql,
                snapshot?.DownSql));
        }

        return changedScripts;
    }

    public static string GenerateUpSql(IReadOnlyList<SqlMigrationScriptInfo> changedScripts)
    {
        var builder = new StringBuilder();

        foreach (var script in changedScripts)
        {
            builder.AppendLine($"// {script.Name}");
            var sql = script.CurrentUpSql ?? script.PreviousDownSql;
            AppendSql(builder, script.Name, sql, "up");
        }

        return builder.ToString();
    }

    public static string GenerateDownSql(IReadOnlyList<SqlMigrationScriptInfo> changedScripts)
    {
        var builder = new StringBuilder();

        foreach (var script in changedScripts.Reverse())
        {
            builder.AppendLine($"// {script.Name}");
            var sql = script.PreviousUpSql ?? script.CurrentDownSql;
            AppendSql(builder, script.Name, sql, "down");
        }

        return builder.ToString();
    }

    private static Dictionary<string, SnapshotSqlMigrationScriptInfo> GetSnapshotScripts()
    {
        var snapshot = GetModelSnapshot();
        if (snapshot == null)
        {
            return new Dictionary<string, SnapshotSqlMigrationScriptInfo>(StringComparer.Ordinal);
        }

        var values = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var annotation in snapshot.Model.GetAnnotations())
        {
            if (!SqlMigrationScriptRegistry.TryParseAnnotationName(
                    annotation.Name,
                    out var scriptName,
                    out var valueName)
                || annotation.Value is not string value)
            {
                continue;
            }

            if (!values.TryGetValue(scriptName, out var scriptValues))
            {
                scriptValues = new Dictionary<string, string>(StringComparer.Ordinal);
                values.Add(scriptName, scriptValues);
            }

            scriptValues[valueName] = value;
        }

        return values.ToDictionary(
            pair => pair.Key,
            pair => new SnapshotSqlMigrationScriptInfo(
                pair.Key,
                pair.Value.GetValueOrDefault("Hash"),
                pair.Value.GetValueOrDefault("UpSql"),
                pair.Value.GetValueOrDefault("DownSql")),
            StringComparer.Ordinal);
    }

    private static ModelSnapshot? GetModelSnapshot()
    {
        var snapshotType = typeof(SqlMigrationScriptGenerator).Assembly.GetTypes()
            .SingleOrDefault(type => !type.IsAbstract && typeof(ModelSnapshot).IsAssignableFrom(type));

        if (snapshotType == null)
        {
            return null;
        }

        return (ModelSnapshot?)Activator.CreateInstance(snapshotType, nonPublic: true);
    }

    private static void AppendSql(StringBuilder builder, string scriptName, string? sql, string direction)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new InvalidOperationException(
                $"SQL migration script '{scriptName}' has no {direction} SQL for this migration transition.");
        }

        sql = sql.TrimEnd();
        var delimiter = GetRawStringDelimiter(sql);
        builder.Append("migrationBuilder.Sql(").AppendLine(delimiter);
        builder.AppendLine(sql);
        builder.Append(delimiter).AppendLine(");");
        builder.AppendLine();
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

internal sealed record SqlMigrationScriptInfo(
    string Name,
    string? CurrentUpSql,
    string? CurrentDownSql,
    string? PreviousUpSql,
    string? PreviousDownSql);

internal sealed record SnapshotSqlMigrationScriptInfo(
    string Name,
    string? Hash,
    string? UpSql,
    string? DownSql);
