using System.Runtime.CompilerServices;
using Planarian.Model.Database;
using Planarian.Modules.Import.Planning;
using Xunit;

namespace Planarian.Tests;

public sealed class ApplicationArchitectureTests
{
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
}
