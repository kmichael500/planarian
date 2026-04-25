using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Planarian.Migrations.SqlViews;

namespace Planarian.Migrations.DesignTime;

public class PlanarianDesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IMigrationsScaffolder, SqlViewMigrationsScaffolder>());
    }
}

internal sealed class SqlViewMigrationsScaffolder : MigrationsScaffolder
{
    public SqlViewMigrationsScaffolder(MigrationsScaffolderDependencies dependencies) : base(dependencies)
    {
    }

    public override ScaffoldedMigration ScaffoldMigration(
        string migrationName,
        string? rootNamespace,
        string? subNamespace,
        string? language)
    {
        var migration = base.ScaffoldMigration(migrationName, rootNamespace, subNamespace, language);
        var changedViews = SqlViewScriptGenerator.GetChangedViews();
        if (changedViews.Count == 0)
        {
            return migration;
        }

        return new ScaffoldedMigration(
            migration.FileExtension,
            migration.PreviousMigrationId,
            InjectSqlViewScripts(migration.MigrationCode, changedViews),
            migration.MigrationId,
            migration.MetadataCode,
            migration.MigrationSubNamespace,
            migration.SnapshotCode,
            migration.SnapshotName,
            migration.SnapshotSubnamespace);
    }

    private static string InjectSqlViewScripts(string migrationCode, IReadOnlyList<SqlViewInfo> changedViews)
    {
        var upSql = Indent(SqlViewScriptGenerator.GenerateUpSql(changedViews), 12);
        var downSql = Indent(SqlViewScriptGenerator.GenerateDownSql(changedViews), 12);

        return InjectIntoMethod(
            InjectIntoMethod(migrationCode, "Up", upSql, insertAtStart: false),
            "Down",
            downSql,
            insertAtStart: true);
    }

    private static string InjectIntoMethod(string migrationCode, string methodName, string sql, bool insertAtStart)
    {
        var methodStart = $"protected override void {methodName}(MigrationBuilder migrationBuilder)";
        var methodIndex = migrationCode.IndexOf(methodStart, StringComparison.Ordinal);
        if (methodIndex < 0)
        {
            throw new InvalidOperationException($"Could not find migration method '{methodName}'.");
        }

        var openBraceIndex = migrationCode.IndexOf('{', methodIndex);
        if (openBraceIndex < 0)
        {
            throw new InvalidOperationException($"Could not find body for migration method '{methodName}'.");
        }

        if (insertAtStart)
        {
            return migrationCode.Insert(openBraceIndex + 1, Environment.NewLine + sql);
        }

        var closeBraceIndex = FindMatchingCloseBrace(migrationCode, openBraceIndex);
        var closeBraceLineStart = migrationCode.LastIndexOf(Environment.NewLine, closeBraceIndex, StringComparison.Ordinal);
        if (closeBraceLineStart < 0)
        {
            return migrationCode.Insert(closeBraceIndex, sql);
        }

        return migrationCode.Insert(closeBraceLineStart + Environment.NewLine.Length, sql);
    }

    private static int FindMatchingCloseBrace(string value, int openBraceIndex)
    {
        var depth = 0;

        for (var index = openBraceIndex; index < value.Length; index++)
        {
            if (value[index] == '{')
            {
                depth++;
                continue;
            }

            if (value[index] != '}')
            {
                continue;
            }

            depth--;
            if (depth == 0)
            {
                return index;
            }
        }

        throw new InvalidOperationException("Could not find matching method body close brace.");
    }

    private static string Indent(string value, int spaces)
    {
        var indent = new string(' ', spaces);
        var lines = value.TrimEnd().Split(Environment.NewLine);
        return string.Join(Environment.NewLine, lines.Select(line => line.Length == 0 ? line : indent + line)) +
               Environment.NewLine;
    }
}
