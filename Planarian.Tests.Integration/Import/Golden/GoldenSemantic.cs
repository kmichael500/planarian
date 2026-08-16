using Planarian.Model.Database.Revisions;

namespace Planarian.Tests.Integration.Import.Golden;

internal static class GoldenSemantic
{
    public static string? Date(DateTime? value) => value?.ToString("yyyy-MM-dd");

    public static SortedDictionary<string, List<string>> CaveTags(
        IEnumerable<string> geology, IEnumerable<string> ages, IEnumerable<string> map,
        IEnumerable<string> provinces, IEnumerable<string> archeology, IEnumerable<string> biology,
        IEnumerable<string> other, IEnumerable<string> cartographers, IEnumerable<string> reporters) =>
        NonEmpty(("Geology", geology), ("GeologicAge", ages), ("MapStatus", map),
            ("PhysiographicProvince", provinces), ("Archeology", archeology), ("Biology", biology),
            ("CaveOther", other), ("Cartographer", cartographers), ("CaveReportedBy", reporters));

    public static SortedDictionary<string, List<string>> EntranceTags(
        IEnumerable<string> statuses, IEnumerable<string> hydrology, IEnumerable<string> field,
        IEnumerable<string> reporters) => NonEmpty(("EntranceStatus", statuses),
        ("EntranceHydrology", hydrology), ("FieldIndication", field), ("EntranceReportedBy", reporters));

    public static SortedDictionary<string, List<string>> SnapshotTags(IEnumerable<SnapshotTagReference> tags) =>
        new(tags.GroupBy(tag => tag.Role.ToString()).OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Select(tag => tag.NameAtRevision).Order().ToList()));

    private static SortedDictionary<string, List<string>> NonEmpty(
        params (string Role, IEnumerable<string> Values)[] groups) => new(groups
        .Select(group => (group.Role, Values: group.Values.Order().ToList()))
        .Where(group => group.Values.Count > 0)
        .ToDictionary(group => group.Role, group => group.Values));
}
