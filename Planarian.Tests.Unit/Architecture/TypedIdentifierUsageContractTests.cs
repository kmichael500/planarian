using System.Text.RegularExpressions;
using Xunit;

namespace Planarian.Tests.Unit.Architecture;

public sealed class TypedIdentifierUsageContractTests
{
    [Fact]
    public void ProductionEfAndReflectionMemberLookupsDoNotUseRawClrMemberNames()
    {
        var root = FindRepositoryRoot();
        var sourceRoots = new[]
        {
            Path.Combine(root, "Planarian", "Planarian"),
            Path.Combine(root, "Planarian", "Planarian.Model"),
            Path.Combine(root, "Planarian", "Planarian.Library")
        };
        var violations = new List<string>();
        // Exact-contract nameof(...) lookups are intentionally allowed. The architecture rule rejects duplicated
        // raw member spellings; generic relationship code is separately exercised against EF model metadata.
        var patterns = new[]
        {
            new Regex("\\.FindProperty\\(\\s*\"", RegexOptions.Compiled),
            new Regex("\\bEF\\.Property<[^>]+>\\([^,]+,\\s*\"", RegexOptions.Compiled),
            new Regex("\\.Property\\(\\s*\"", RegexOptions.Compiled),
            new Regex("GetType\\(\\)\\.GetProperty\\(\\s*\"", RegexOptions.Compiled)
        };

        foreach (var sourceRoot in sourceRoots)
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(root, file);
                var segments = relativePath.Split(Path.DirectorySeparatorChar);
                if (segments.Contains("Migrations", StringComparer.Ordinal) ||
                    segments.Contains("bin", StringComparer.Ordinal) ||
                    segments.Contains("obj", StringComparer.Ordinal))
                    continue;

                var lines = System.IO.File.ReadAllLines(file);
                for (var lineNumber = 0; lineNumber < lines.Length; lineNumber++)
                {
                    if (!patterns.Any(pattern => pattern.IsMatch(lines[lineNumber]))) continue;
                    violations.Add($"{Path.GetRelativePath(root, file)}:{lineNumber + 1}: {lines[lineNumber].Trim()}");
                }
            }

        Assert.True(violations.Count == 0,
            "CLR member lookup must not duplicate member spelling in raw strings; generic EF/reflection code should use mapped metadata rather than borrowed member names:\n" +
            string.Join("\n", violations));
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "Planarian", "Planarian.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate the Planarian repository root.");
    }
}
