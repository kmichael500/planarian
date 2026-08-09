using System.Text.RegularExpressions;
using System.Text.Json;
using System.Xml.Linq;
using System.Runtime.CompilerServices;
using Planarian.Model.Database;
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
        var candidates = assembly.GetTypes().Where(IsApplicationOrchestrationType).ToList();

        foreach (var type in candidates)
        foreach (var dependency in DeclaredDependencies(type))
            Assert.False(IsForbiddenDatabaseDependency(dependency),
                $"{type.FullName} directly declares forbidden database dependency {dependency.FullName}.");

        Assert.Empty(typeof(CaveImportPlanner).GetConstructors().SelectMany(c => c.GetParameters()));
        Assert.Empty(typeof(EntranceImportPlanner).GetConstructors().SelectMany(c => c.GetParameters()));
    }

    [Fact]
    public void WorkflowClassificationWouldCatchAContextInjectingConvenienceType()
    {
        Assert.True(IsApplicationOrchestrationType(typeof(ContextInjectingWorkflow)));
        Assert.Contains(DeclaredDependencies(typeof(ContextInjectingWorkflow)), IsForbiddenDatabaseDependency);
    }

    private static bool IsApplicationOrchestrationType(Type type)
    {
        if (!type.IsClass || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false) ||
            type.Name.EndsWith("Repository", StringComparison.Ordinal)) return false;

        var namespaceName = type.Namespace ?? string.Empty;
        return type.Name.EndsWith("Service", StringComparison.Ordinal) ||
               type.Name.EndsWith("Workflow", StringComparison.Ordinal) ||
               type.Name.EndsWith("Coordinator", StringComparison.Ordinal) ||
               type.Name.EndsWith("Planner", StringComparison.Ordinal) ||
               namespaceName.Contains(".Services", StringComparison.Ordinal) ||
               namespaceName.Contains(".Planning", StringComparison.Ordinal) ||
               namespaceName.Contains(".Workflows", StringComparison.Ordinal) ||
               namespaceName.Contains(".Coordinators", StringComparison.Ordinal);
    }

    private static IEnumerable<Type> DeclaredDependencies(Type type) =>
        type.GetConstructors().SelectMany(c => c.GetParameters()).Select(p => p.ParameterType)
            .Concat(type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                   System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(f => f.FieldType))
            .Concat(type.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                                       System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly)
                .Select(p => p.PropertyType));

    private static bool IsForbiddenDatabaseDependency(Type type)
    {
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        if (definition.FullName is "Planarian.Model.Database.PlanarianDbContext" or
            "Microsoft.EntityFrameworkCore.DbContext" or "Microsoft.EntityFrameworkCore.DbSet`1" or
            "Npgsql.NpgsqlConnection" or "Npgsql.NpgsqlCommand")
            return true;

        return type.HasElementType && type.GetElementType() is { } element && IsForbiddenDatabaseDependency(element) ||
               type.IsGenericType && type.GetGenericArguments().Any(IsForbiddenDatabaseDependency);
    }

    private sealed class ContextInjectingWorkflow
    {
        private readonly PlanarianDbContext _db;

        public ContextInjectingWorkflow(PlanarianDbContext db) => _db = db;
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
