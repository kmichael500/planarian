using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;
using Planarian.Modules.Import.Planning;
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
    public void ApplicationAndPlanningTypesDoNotDependDirectlyOnDatabaseInfrastructure()
    {
        var assembly = typeof(CaveImportPlanner).Assembly;
        var candidates = assembly.GetTypes().Where(type =>
            type.FullName is "Planarian.Modules.Account.Import.Services.ImportService" or
                "Planarian.Modules.Caves.Revisions.CaveMutationCoordinator" ||
            type.Namespace == "Planarian.Modules.Import.Planning" &&
            !type.Name.EndsWith("Repository", StringComparison.Ordinal)).ToList();
        var forbiddenNames = new[]
        {
            "Planarian.Model.Database.PlanarianDbContext", "Microsoft.EntityFrameworkCore.DbContext",
            "Microsoft.EntityFrameworkCore.DbSet`1", "Npgsql.NpgsqlConnection", "Npgsql.NpgsqlCommand"
        };

        foreach (var type in candidates)
        foreach (var dependency in DeclaredDependencies(type))
            Assert.DoesNotContain(forbiddenNames, name => IsOrContains(dependency, name));

        Assert.Empty(typeof(CaveImportPlanner).GetConstructors().SelectMany(c => c.GetParameters()));
        Assert.Empty(typeof(EntranceImportPlanner).GetConstructors().SelectMany(c => c.GetParameters()));
    }

    private static IEnumerable<Type> DeclaredDependencies(Type type) =>
        type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(f => f.FieldType))
            .Concat(type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(p => p.PropertyType));

    private static bool IsOrContains(Type type, string forbiddenName) =>
        type.FullName == forbiddenName || type.IsGenericType && type.GetGenericArguments().Any(t => IsOrContains(t, forbiddenName));

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
