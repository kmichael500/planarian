using System.Text.RegularExpressions;
using Planarian.Model.Database.Entities;
using Planarian.Model.Database.Entities.Leads;
using Planarian.Model.Database.Entities.Projects;
using Planarian.Model.Database.Entities.RidgeWalker;
using Planarian.Model.Database.Entities.Trips;
using Planarian.Model.Shared.Base;
using Xunit;

namespace Planarian.Tests.Unit.Architecture;

public sealed class SaveChangesInterceptorCoverageContractTests
{
    private static readonly string[] IntentionalNonTenantEntityAllowlist =
    [
        nameof(Lead),
        nameof(LeadTag),
        nameof(Member),
        nameof(MessageLog),
        nameof(MessageLogEvent),
        nameof(MessageType),
        nameof(Photo),
        nameof(Project),
        nameof(ThrottleEventLog),
        nameof(Trip),
        nameof(TripTag)
    ];

    [Fact]
    public void EveryEntityBaseTypeIsEitherValidatedOrExplicitlyClassified()
    {
        var root = FindRepositoryRoot();
        var interceptorPath = Path.Combine(root, "Planarian", "Planarian.Model", "Interceptors",
            "SaveChangesInterceptor.cs");
        var interceptorSource = System.IO.File.ReadAllText(interceptorPath);
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
        var source = System.IO.File.ReadAllText(Path.Combine(root, "Planarian", "Planarian.Model", "Interceptors",
            "SaveChangesInterceptor.cs"));
        var switchCases = Regex.Matches(source, @"case nameof\(([^)]+)\):")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        string[] required =
        [
            nameof(Account), nameof(AccountState), nameof(AccountUser), nameof(TagType),
            nameof(ArcheologyTag), nameof(BiologyTag), nameof(CartographerNameTag), nameof(CaveOtherTag), nameof(CaveReportedByNameTag),
            nameof(GeologicAgeTag), nameof(GeologyTag), nameof(MapStatusTag), nameof(PhysiographicProvinceTag),
            nameof(Cave), nameof(CaveRevision), nameof(CaveImportBatch), nameof(CaveChangeRequest), nameof(CaveProposalVersion),
            nameof(CaveChangeRequestStagedFile), nameof(CaveChangeRequestReviewEvent), nameof(StorageCleanupTarget),
            nameof(County), nameof(Entrance), nameof(EntranceHydrologyTag), nameof(EntranceReportedByNameTag), nameof(EntranceStatusTag),
            nameof(FieldIndicationTag), nameof(EntranceOtherTag), nameof(FeatureSetting), nameof(Planarian.Model.Database.Entities.RidgeWalker.File), nameof(CaveGeoJson),
            nameof(CavePermission), nameof(Favorite), nameof(UserPermission), nameof(Permission), nameof(State), nameof(User)
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
            if (System.IO.File.Exists(Path.Combine(directory.FullName, "Planarian", "Planarian.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Unable to locate the Planarian repository root.");
    }
}
