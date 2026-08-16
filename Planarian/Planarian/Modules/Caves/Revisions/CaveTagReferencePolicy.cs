using Planarian.Library.Exceptions;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Helpers;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Tags;

namespace Planarian.Modules.Caves.Revisions;

public sealed record ResolvedCaveTagReferences(IReadOnlyList<SnapshotTagReference> Existing,
    IReadOnlyList<ProposalPeopleTagIntent> NewPeople);
internal sealed record CavePeoplePublicationScope(IReadOnlyList<SnapshotTagReference> Existing,
    IReadOnlyList<ProposalPeopleTagIntent> NewPeople);
internal sealed record CavePeoplePublicationContext(CavePeoplePublicationScope Cave,
    IReadOnlyDictionary<string, CavePeoplePublicationScope> Entrances);

public static class CaveTagReferencePolicy
{
    public static IEnumerable<(SnapshotTagRole Role, IEnumerable<string> Values)> CaveGroups(AddCaveVm cave)
    {
        yield return (SnapshotTagRole.Geology, cave.GeologyTagIds);
        yield return (SnapshotTagRole.GeologicAge, cave.GeologicAgeTagIds);
        yield return (SnapshotTagRole.MapStatus, cave.MapStatusTagIds);
        yield return (SnapshotTagRole.PhysiographicProvince, cave.PhysiographicProvinceTagIds);
        yield return (SnapshotTagRole.Archeology, cave.ArcheologyTagIds);
        yield return (SnapshotTagRole.Biology, cave.BiologyTagIds);
        yield return (SnapshotTagRole.CaveOther, cave.OtherTagIds);
        yield return (SnapshotTagRole.Cartographer, cave.CartographerNameTagIds);
        yield return (SnapshotTagRole.CaveReportedBy, cave.ReportedByNameTagIds);
    }

    public static IEnumerable<(SnapshotTagRole Role, IEnumerable<string> Values)> EntranceGroups(AddEntranceVm entrance)
    {
        yield return (SnapshotTagRole.EntranceStatus, entrance.EntranceStatusTagIds);
        yield return (SnapshotTagRole.EntranceHydrology, entrance.EntranceHydrologyTagIds);
        yield return (SnapshotTagRole.FieldIndication, entrance.FieldIndicationTagIds);
        yield return (SnapshotTagRole.EntranceReportedBy, entrance.ReportedByNameTagIds);
        yield return (SnapshotTagRole.EntranceOther, entrance.EntranceOtherTagIds);
    }

    public static string RequiredKey(SnapshotTagRole role) => role switch
    {
        SnapshotTagRole.Geology => TagTypeKeyConstant.Geology,
        SnapshotTagRole.GeologicAge => TagTypeKeyConstant.GeologicAge,
        SnapshotTagRole.MapStatus => TagTypeKeyConstant.MapStatus,
        SnapshotTagRole.PhysiographicProvince => TagTypeKeyConstant.PhysiographicProvince,
        SnapshotTagRole.Archeology => TagTypeKeyConstant.Archeology,
        SnapshotTagRole.Biology => TagTypeKeyConstant.Biology,
        SnapshotTagRole.CaveOther or SnapshotTagRole.EntranceOther => TagTypeKeyConstant.CaveOther,
        SnapshotTagRole.Cartographer or SnapshotTagRole.CaveReportedBy or SnapshotTagRole.EntranceReportedBy =>
            TagTypeKeyConstant.People,
        SnapshotTagRole.EntranceStatus => TagTypeKeyConstant.EntranceStatus,
        SnapshotTagRole.EntranceHydrology => TagTypeKeyConstant.EntranceHydrology,
        SnapshotTagRole.FieldIndication => TagTypeKeyConstant.FieldIndication,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    public static bool AllowsNewPeopleIntent(SnapshotTagRole role) => role is
        SnapshotTagRole.Cartographer or SnapshotTagRole.CaveReportedBy or SnapshotTagRole.EntranceReportedBy;

    public static ResolvedCaveTagReferences Resolve(IEnumerable<(SnapshotTagRole Role, IEnumerable<string> Values)> groups,
        IEnumerable<TagNameCandidate> idCandidates,
        IEnumerable<TagNameCandidate> eligiblePeopleNameCandidates, string accountId)
    {
        var byId = idCandidates.ToDictionary(candidate => candidate.Id, StringComparer.Ordinal);
        var peopleByRole = eligiblePeopleNameCandidates
            .Where(candidate => candidate.Key == TagTypeKeyConstant.People &&
                                (candidate.IsDefault || candidate.AccountId == accountId))
            .ToList();
        var existing = new List<SnapshotTagReference>();
        var newPeople = new List<ProposalPeopleTagIntent>();
        var existingIdsByRole = new Dictionary<SnapshotTagRole, HashSet<string>>();
        var newNamesByRole = new Dictionary<SnapshotTagRole, HashSet<string>>();
        foreach (var (role, rawValues) in groups)
        {
            var requiredKey = RequiredKey(role);
            foreach (var value in rawValues.Where(value => !string.IsNullOrWhiteSpace(value))
                         .Select(value => value.Trim()))
            {
                if (byId.TryGetValue(value, out var tag))
                {
                    if (tag.Key != requiredKey || (!tag.IsDefault && tag.AccountId != accountId))
                        throw ApiExceptionDictionary.BadRequest($"The selected tag is not valid for {role}.");
                    AddExisting(role, tag);
                }
                else if (AllowsNewPeopleIntent(role))
                {
                    var match = TagNameMatchPolicy.Select(value, accountId, peopleByRole);
                    if (match is not null)
                    {
                        AddExisting(role, match);
                        continue;
                    }

                    ValidateNewPeopleName(value);
                    if (newNamesByRole.GetValueOrDefault(role) is not { } seenNames)
                        newNamesByRole[role] = seenNames = new HashSet<string>(TagNameMatchPolicy.IdentityComparer);
                    if (seenNames.Add(value)) newPeople.Add(new ProposalPeopleTagIntent(role, value));
                }
                else
                {
                    throw ApiExceptionDictionary.BadRequest($"The selected tag is not valid for {role}.");
                }
            }
        }
        return new ResolvedCaveTagReferences(existing, newPeople);

        void AddExisting(SnapshotTagRole role, TagNameCandidate tag)
        {
            if (existingIdsByRole.GetValueOrDefault(role) is not { } seenIds)
                existingIdsByRole[role] = seenIds = new HashSet<string>(StringComparer.Ordinal);
            if (seenIds.Add(tag.Id)) existing.Add(new SnapshotTagReference(role, tag.Id, tag.Name));
        }
    }

    public static void ValidateNewPeopleName(string? value)
    {
        var name = value?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw ApiExceptionDictionary.BadRequest("People names cannot be blank.");
        if (name.Length > PropertyLength.Name)
            throw ApiExceptionDictionary.BadRequest(
                $"People names cannot exceed {PropertyLength.Name} characters.");
    }

    public static void RequireTypedReference(string id, string requiredKey,
        IReadOnlyDictionary<string, TagNameCandidate> candidates, string accountId, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(id) || !candidates.TryGetValue(id, out var tag) || tag.Key != requiredKey ||
            (!tag.IsDefault && tag.AccountId != accountId))
            throw ApiExceptionDictionary.BadRequest($"The selected {fieldName} is invalid.");
    }
}
