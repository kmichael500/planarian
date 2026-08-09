using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;
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
    public void ProhibitedImportInfrastructurePackagesAreAbsentFromProjectReferences()
    {
        var forbidden = new[] { "linq2db", "EFCore.BulkExtensions", "Z.EntityFramework.Extensions" };
        var root = FindRepositoryRoot();
        var productionRoot = Path.Combine(root, "Planarian");
        var packages = Directory.EnumerateFiles(productionRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !Path.GetFileNameWithoutExtension(path).Contains("Test", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => XDocument.Load(path).Descendants().Where(e => e.Name.LocalName == "PackageReference")
                .Select(e => (Path: path, Package: e.Attribute("Include")?.Value ?? e.Attribute("Update")?.Value ?? "")))
            .ToList();

        foreach (var package in packages)
            Assert.DoesNotContain(forbidden, name => string.Equals(name, package.Package, StringComparison.OrdinalIgnoreCase));

        // project.assets.json is the restore graph, so this also catches a package
        // arriving through a transitive dependency rather than a direct reference.
        var restoredPackages = Directory.EnumerateFiles(productionRoot, "project.assets.json", SearchOption.AllDirectories)
            .Where(path => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains("Test", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => JsonDocument.Parse(System.IO.File.ReadAllText(path)).RootElement
                .GetProperty("libraries").EnumerateObject().Select(property => property.Name.Split('/')[0]))
            .ToList();
        foreach (var package in restoredPackages)
            Assert.DoesNotContain(forbidden, name => string.Equals(name, package, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WorkflowEntityQueryFilterBypassesRestoreAccountPredicateImmediately()
    {
        // Supplemental source lint only. ImportTenantAdversarialIntegrationTests
        // and PostgreSQL FK tests are the executable tenant-security boundary.
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
