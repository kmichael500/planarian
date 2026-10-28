using System.Text.RegularExpressions;
using Planarian.Model.Shared.Base;
using Xunit;

namespace Planarian.Tests.Unit.Architecture;

public sealed class SaveChangesInterceptorCoverageContractTests
{
    private static readonly string[] IntentionalNonTenantEntityAllowlist =
    [
        "Lead",
        "LeadTag",
        "Member",
        "MessageLog",
        "MessageLogEvent",
        "MessageType",
        "Photo",
        "Project",
        "ThrottleEventLog",
        "Trip",
        "TripTag"
    ];

    [Fact]
    public void EveryEntityBaseTypeIsEitherValidatedOrExplicitlyClassified()
    {
        var root = FindRepositoryRoot();
        var interceptorPath = Path.Combine(root, "Planarian", "Planarian.Model", "Interceptors",
            "SaveChangesInterceptor.cs");
        var interceptorSource = File.ReadAllText(interceptorPath);
        var switchCases = Regex.Matches(interceptorSource, @"case nameof\(([^)]+)\):")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        var entityTypes = typeof(EntityBase).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && typeof(EntityBase).IsAssignableFrom(type))
            .Select(type => type.Name)
            .ToHashSet(StringComparer.Ordinal);

        var unvalidated = entityTypes
            .Except(switchCases)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(IntentionalNonTenantEntityAllowlist.OrderBy(name => name, StringComparer.Ordinal), unvalidated);
    }

    [Fact]
    public void CurrentCaveAndAccountOwnershipCasesRemainInTheInterceptorSwitch()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Planarian", "Planarian.Model", "Interceptors",
            "SaveChangesInterceptor.cs"));
        var switchCases = Regex.Matches(source, @"case nameof\(([^)]+)\):")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        string[] required =
        [
            "Account", "AccountState", "AccountUser", "TagType",
            "ArcheologyTag", "BiologyTag", "CartographerNameTag", "CaveOtherTag", "CaveReportedByNameTag",
            "GeologicAgeTag", "GeologyTag", "MapStatusTag", "PhysiographicProvinceTag",
            "Cave", "CaveRevision", "CaveImportBatch", "CaveChangeRequest", "CaveProposalVersion",
            "CaveChangeRequestStagedFile", "CaveChangeRequestReviewEvent", "StorageCleanupTarget",
            "County", "Entrance", "EntranceHydrologyTag", "EntranceReportedByNameTag", "EntranceStatusTag",
            "FieldIndicationTag", "EntranceOtherTag", "FeatureSetting", "File", "CaveGeoJson",
            "CavePermission", "Favorite", "UserPermission", "Permission", "State", "User"
        ];

        var missing = required.Where(name => !switchCases.Contains(name)).OrderBy(name => name).ToArray();
        Assert.True(missing.Length == 0,
            $"Tenant-sensitive interceptor cases missing from switch: {string.Join(", ", missing)}");
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Planarian", "Planarian.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate the Planarian repository root.");
    }
}
