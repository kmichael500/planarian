using System.Reflection;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;

namespace Planarian.Model.Shared.Base;

public sealed record SqlMigrationScriptDefinition(
    string Name,
    string UpSql,
    string DownSql,
    string Hash);

public static class SqlMigrationScriptRegistry
{
    public const string AnnotationPrefix = "Planarian:SqlMigrationScript:";

    private const string ResourceRoot = "Planarian.Model.Database.MigrationScripts.";
    private const string UpSuffix = ".Up.sql";
    private const string DownSuffix = ".Down.sql";

    public static void ApplyAnnotations(ModelBuilder modelBuilder)
    {
        foreach (var script in GetCurrentScripts().Values)
        {
            modelBuilder.HasAnnotation(GetAnnotationName(script.Name, "Hash"), script.Hash);
            modelBuilder.HasAnnotation(GetAnnotationName(script.Name, "UpSql"), script.UpSql);
            modelBuilder.HasAnnotation(GetAnnotationName(script.Name, "DownSql"), script.DownSql);
        }
    }

    public static IReadOnlyDictionary<string, SqlMigrationScriptDefinition> GetCurrentScripts()
    {
        var assembly = typeof(SqlMigrationScriptRegistry).Assembly;
        var resources = assembly.GetManifestResourceNames();

        var scriptNames = resources
            .Where(resource => resource.StartsWith(ResourceRoot, StringComparison.Ordinal)
                               && resource.EndsWith(UpSuffix, StringComparison.Ordinal))
            .Select(resource => resource[ResourceRoot.Length..^UpSuffix.Length])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var scripts = new Dictionary<string, SqlMigrationScriptDefinition>(StringComparer.Ordinal);
        foreach (var name in scriptNames)
        {
            var upSql = ReadResource(assembly, $"{ResourceRoot}{name}{UpSuffix}");
            var downSql = ReadResource(assembly, $"{ResourceRoot}{name}{DownSuffix}");
            var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{upSql}\0{downSql}"));

            scripts.Add(
                name,
                new SqlMigrationScriptDefinition(
                    name,
                    upSql,
                    downSql,
                    Convert.ToHexString(hashBytes)));
        }

        return scripts;
    }

    public static string GetAnnotationName(string scriptName, string valueName)
    {
        return $"{AnnotationPrefix}{scriptName}:{valueName}";
    }

    public static bool TryParseAnnotationName(
        string annotationName,
        out string scriptName,
        out string valueName)
    {
        scriptName = string.Empty;
        valueName = string.Empty;

        if (!annotationName.StartsWith(AnnotationPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = annotationName[AnnotationPrefix.Length..];
        var separatorIndex = remainder.LastIndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == remainder.Length - 1)
        {
            return false;
        }

        scriptName = remainder[..separatorIndex];
        valueName = remainder[(separatorIndex + 1)..];
        return true;
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"SQL migration resource '{resourceName}' was not found in assembly '{assembly.GetName().Name}'.");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
