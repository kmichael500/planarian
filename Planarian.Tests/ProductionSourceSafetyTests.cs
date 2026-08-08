using System.Text.RegularExpressions;
using Xunit;

namespace Planarian.Tests;

public sealed class ProductionSourceSafetyTests
{
    [Fact]
    public void DeprecatedImportAndRevisionImplementationsAreAbsentFromProductionSource()
    {
        var source = ReadProductionSources();
        var forbidden = new[]
        {
            "linq2db",
            "LinqToDB",
            "EFCore.BulkExtensions",
            "TemporaryEntrance",
            "EntranceImportPlanStore",
            "CaveChangeHistory",
            "ChangeLogBuilder",
            "field-event replay"
        };
        foreach (var term in forbidden)
            Assert.DoesNotContain(term, source, StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotMatch(new Regex(@"CREATE\s+TEMP[^;]*Entrance",
            RegexOptions.IgnoreCase | RegexOptions.Singleline), source);
    }

    [Fact]
    public void WorkflowEntityQueryFilterBypassesRestoreAccountPredicateImmediately()
    {
        var files = ProductionFiles();
        var sets = new[]
        {
            "CaveRevisions",
            "CaveImportBatches",
            "CaveChangeRequests",
            "CaveProposalVersions",
            "CaveChangeRequestStagedFiles"
        };

        foreach (var file in files)
        {
            var text = System.IO.File.ReadAllText(file);
            foreach (var set in sets)
            {
                foreach (Match match in Regex.Matches(text,
                             $@"\b{Regex.Escape(set)}\s*\.\s*IgnoreQueryFilters\s*\(\s*\)",
                             RegexOptions.Multiline))
                {
                    var length = Math.Min(600, text.Length - match.Index);
                    var trustedWindow = text.Substring(match.Index, length);
                    Assert.Matches(new Regex(@"AccountId\s*==|\.AccountId\s*\.Equals\s*\(",
                        RegexOptions.Multiline), trustedWindow);
                }
            }
        }
    }

    private static string ReadProductionSources() => string.Join("\n", ProductionFiles().Select(System.IO.File.ReadAllText));

    private static IReadOnlyList<string> ProductionFiles()
    {
        var root = FindRepositoryRoot();
        return new[]
            {
                Path.Combine(root, "Planarian", "Planarian"),
                Path.Combine(root, "Planarian", "Planarian.Model")
            }
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "Planarian", "Planarian.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root from test output directory.");
    }
}
