namespace Planarian.Model.Database.Revisions;

public sealed record DiffEntitySummary(int Added, int Removed, int Changed, int Total);

public sealed record CaveChangeScopeSummary(
    int CaveFields,
    DiffEntitySummary Entrances,
    DiffEntitySummary Files,
    DiffEntitySummary LinePlots,
    int OtherChanges);

/// <summary>
/// Builds the compact list summary for an immutable proposal by projecting only data already
/// captured in proposal/base snapshots, then delegating semantic comparison to the canonical diff service.
/// No current labels, files, or other mutable database state are needed for this presentation.
/// </summary>
public static class CaveProposalChangeScopeSummary
{
    public static CaveChangeScopeSummary Build(CavePublishedSnapshotV1 @base, CaveProposalSnapshotV1 proposal)
    {
        var diff = new CaveRevisionDiffService().Compare(@base, Project(@base, proposal));
        var caveFields = new HashSet<string>(diff.Scalars.Keys, StringComparer.Ordinal);
        foreach (var role in diff.AddedTags.Select(tag => tag.Role).Concat(diff.RemovedTags.Select(tag => tag.Role)))
            caveFields.Add($"Tag:{role}");

        var otherChanges = 0;
        foreach (var metadata in diff.ReferenceMetadataChanges)
        {
            if (metadata.Path is CaveRevisionDiffWireContract.State or CaveRevisionDiffWireContract.County)
                caveFields.Add(metadata.Path);
            else if (metadata.Path.StartsWith($"{CaveRevisionDiffWireContract.Tags}/", StringComparison.Ordinal))
                caveFields.Add($"Tag:{metadata.Path[(CaveRevisionDiffWireContract.Tags.Length + 1)..]}");
            else if (!metadata.Path.StartsWith($"{CaveRevisionDiffWireContract.Entrances}/", StringComparison.Ordinal) &&
                     !metadata.Path.StartsWith($"{CaveRevisionDiffWireContract.Files}/", StringComparison.Ordinal))
                otherChanges++;
        }

        return new CaveChangeScopeSummary(
            caveFields.Count,
            EntitySummary(diff.AddedEntrances, diff.RemovedEntrances, diff.ChangedEntrances),
            EntitySummary(diff.AddedFiles, diff.RemovedFiles, diff.ChangedFiles),
            EntitySummary(diff.AddedLinePlots, diff.RemovedLinePlots, diff.ChangedLinePlots),
            otherChanges);
    }

    private static DiffEntitySummary EntitySummary(
        IReadOnlyCollection<string> added,
        IReadOnlyCollection<string> removed,
        IReadOnlyCollection<string> changed) =>
        new(added.Count, removed.Count, changed.Count, added.Count + removed.Count + changed.Count);

    private static CavePublishedSnapshotV1 Project(CavePublishedSnapshotV1 @base, CaveProposalSnapshotV1 proposal)
    {
        var baseEntrances = @base.Entrances.ToDictionary(entrance => entrance.Id, StringComparer.Ordinal);
        var baseFiles = @base.Files.ToDictionary(file => file.Id, StringComparer.Ordinal);

        return new CavePublishedSnapshotV1
        {
            CaveId = proposal.CaveId,
            AccountId = proposal.AccountId,
            Name = proposal.Name,
            AlternateNames = proposal.AlternateNames,
            State = new SnapshotReference(
                proposal.StateId,
                proposal.StateNameAtRevision ?? SameReferenceName(@base.State, proposal.StateId),
                null,
                proposal.StateAbbreviationAtRevision ?? SameReferenceAbbreviation(@base.State, proposal.StateId)),
            County = new SnapshotReference(
                proposal.CountyId,
                proposal.CountyNameAtRevision ?? SameReferenceName(@base.County, proposal.CountyId),
                proposal.CountyDisplayIdAtRevision ?? SameReferenceDisplayId(@base.County, proposal.CountyId)),
            CountyNumber = proposal.RequestedCountyNumber ??
                           (@base.County.Id == proposal.CountyId ? @base.CountyNumber : 0),
            LengthFeet = proposal.LengthFeet,
            DepthFeet = proposal.DepthFeet,
            MaxPitDepthFeet = proposal.MaxPitDepthFeet,
            NumberOfPits = proposal.NumberOfPits,
            Narrative = proposal.Narrative,
            ReportedOn = proposal.ReportedOn,
            IsArchived = proposal.IsArchived,
            Tags = proposal.Tags.Concat(proposal.NewPeopleTagIntents.Select(intent =>
                new SnapshotTagReference(intent.Role, intent.Name, intent.Name))).ToList(),
            Entrances = proposal.Entrances.Select(entrance => ProjectEntrance(entrance, baseEntrances)).ToList(),
            Files = proposal.Files.Select(file => ProjectFile(file, baseFiles))
                .Where(file => file is not null).Cast<CaveFileSnapshotV1>().ToList(),
            LinePlots = proposal.LinePlots.OrderBy(linePlot => linePlot.Id, StringComparer.Ordinal)
                .Select(linePlot => new CaveLinePlotSnapshotV1
                {
                    Id = linePlot.Id,
                    Name = linePlot.Name,
                    ContentHash = CaveJsonContent.NormalizeAndHash(linePlot.GeoJson).ContentHash
                }).ToList()
        };
    }

    private static CaveEntranceSnapshotV1 ProjectEntrance(
        CaveProposalEntranceV1 entrance,
        IReadOnlyDictionary<string, CaveEntranceSnapshotV1> baseEntrances)
    {
        baseEntrances.TryGetValue(entrance.EntranceId, out var previous);
        var locationQualityName = entrance.LocationQualityNameAtRevision ??
                                  (previous?.LocationQualityTagId == entrance.LocationQualityTagId
                                      ? previous.LocationQualityNameAtRevision
                                      : entrance.LocationQualityTagId);
        return new CaveEntranceSnapshotV1
        {
            Id = entrance.EntranceId,
            Name = entrance.Name,
            IsPrimary = entrance.IsPrimary,
            Description = entrance.Description,
            Latitude = entrance.Latitude,
            Longitude = entrance.Longitude,
            Elevation = entrance.Elevation,
            Srid = entrance.Srid,
            LocationQualityTagId = entrance.LocationQualityTagId,
            LocationQualityNameAtRevision = locationQualityName,
            ReportedOn = entrance.ReportedOn,
            PitDepthFeet = entrance.PitDepthFeet,
            Tags = entrance.Tags.Concat(entrance.NewPeopleTagIntents.Select(intent =>
                new SnapshotTagReference(intent.Role, intent.Name, intent.Name))).ToList()
        };
    }

    private static CaveFileSnapshotV1? ProjectFile(
        ProposalFileIntent file,
        IReadOnlyDictionary<string, CaveFileSnapshotV1> baseFiles) => file.Disposition switch
    {
        ProposalFileDisposition.RetainPublished when baseFiles.TryGetValue(file.FileId, out var published) =>
            published with
            {
                FileTypeTagId = file.FileTypeTagId ?? published.FileTypeTagId,
                FileTypeNameAtRevision = file.FileTypeName ?? published.FileTypeNameAtRevision,
                Name = file.Name ?? published.Name,
                Extension = published.Extension
            },
        ProposalFileDisposition.PublishStaged => new CaveFileSnapshotV1
        {
            Id = file.FileId,
            FileTypeTagId = file.FileTypeTagId ?? "unavailable",
            FileTypeNameAtRevision = file.FileTypeName ?? file.FileTypeTagId ?? "File",
            Name = file.Name ?? file.FileId,
            Extension = file.Extension ?? string.Empty
        },
        _ => null
    };

    private static string SameReferenceName(SnapshotReference reference, string id) =>
        reference.Id == id ? reference.NameAtRevision : id;

    private static string? SameReferenceAbbreviation(SnapshotReference reference, string id) =>
        reference.Id == id ? reference.AbbreviationAtRevision : null;

    private static string? SameReferenceDisplayId(SnapshotReference reference, string id) =>
        reference.Id == id ? reference.DisplayIdAtRevision : null;
}
