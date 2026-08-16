namespace Planarian.Modules.Tags;

public interface ITagNameMatchCandidate
{
    string Id { get; }
    string Name { get; }
    string? AccountId { get; }
}

public sealed record TagNameCandidate(string Id, string Name, string Key, string? AccountId, bool IsDefault)
    : ITagNameMatchCandidate;

public static class TagNameMatchPolicy
{
    public static IEqualityComparer<string> IdentityComparer { get; } =
        StringComparer.InvariantCultureIgnoreCase;

    public static TCandidate? Select<TCandidate>(string requestedNormalizedName, string currentAccountId,
        IEnumerable<TCandidate> eligibleCandidates)
        where TCandidate : class, ITagNameMatchCandidate
    {
        return eligibleCandidates
            .Where(candidate => IdentityComparer.Equals(candidate.Name, requestedNormalizedName))
            .OrderByDescending(candidate => candidate.Name.Equals(requestedNormalizedName,
                StringComparison.Ordinal))
            .ThenByDescending(candidate => candidate.AccountId == currentAccountId)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }
}
