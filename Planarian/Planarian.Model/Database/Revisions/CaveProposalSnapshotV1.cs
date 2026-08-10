using System.Text.Json;

namespace Planarian.Model.Database.Revisions;

public enum CountyNumberIntent
{
    AutomaticNext,
    FirstAvailable,
    Manual
}

public sealed record CaveProposalSnapshotV1
{
    public int SchemaVersion { get; init; } = 1;
    public string CaveId { get; init; } = null!;
    public string AccountId { get; init; } = null!;
    public string Name { get; init; } = null!;
    public IReadOnlyList<string> AlternateNames { get; init; } = [];
    public string StateId { get; init; } = null!;
    public string CountyId { get; init; } = null!;
    public CountyNumberIntent CountyNumberIntent { get; init; }
    public int? RequestedCountyNumber { get; init; }
    public double? LengthFeet { get; init; }
    public double? DepthFeet { get; init; }
    public double? MaxPitDepthFeet { get; init; }
    public int? NumberOfPits { get; init; }
    public string? Narrative { get; init; }
    public DateTime? ReportedOn { get; init; }
    public bool IsArchived { get; init; }
    public IReadOnlyList<SnapshotTagReference> Tags { get; init; } = [];
    public IReadOnlyList<CaveProposalEntranceV1> Entrances { get; init; } = [];
    public IReadOnlyList<ProposalFileIntent> Files { get; init; } = [];
    public IReadOnlyList<ProposalTagIntent> NewTagIntents { get; init; } = [];
}

public sealed record ProposalTagIntent(SnapshotTagRole Role, string Name);

/// <summary>Pending entrance intent. New tag names stay attached to this entrance.</summary>
public sealed record CaveProposalEntranceV1
{
    public string EntranceId { get; init; } = null!;
    public string? Name { get; init; }
    public bool IsPrimary { get; init; }
    public string? Description { get; init; }
    public string? ReportedByUserId { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? Elevation { get; init; }
    public int Srid { get; init; } = 4326;
    public string LocationQualityTagId { get; init; } = null!;
    public DateTime? ReportedOn { get; init; }
    public double? PitDepthFeet { get; init; }
    public IReadOnlyList<SnapshotTagReference> Tags { get; init; } = [];
    public IReadOnlyList<ProposalTagIntent> NewTagIntents { get; init; } = [];
}

public enum ProposalFileDisposition
{
    RetainPublished,
    RemovePublished,
    PublishStaged
}

/// <summary>Complete file intent; absence from another table never implies removal.</summary>
public sealed record ProposalFileIntent(string FileId, ProposalFileDisposition Disposition,
    string? FileTypeTagId = null, string? DisplayName = null);

public static class CaveProposalJson
{
    public static string Serialize(CaveProposalSnapshotV1 snapshot) => JsonSerializer.Serialize(snapshot, CaveSnapshotJson.Options);

    public static CaveProposalSnapshotV1 Deserialize(string json, int schemaVersion)
    {
        if (schemaVersion != 1) throw new NotSupportedException($"Unsupported Cave proposal schema version: {schemaVersion}.");
        var proposal = JsonSerializer.Deserialize<CaveProposalSnapshotV1>(json, CaveSnapshotJson.Options)
                       ?? throw new InvalidOperationException("Cave proposal JSON was empty.");
        if (proposal.SchemaVersion != schemaVersion)
            throw new InvalidOperationException("Cave proposal row and payload schema versions differ.");
        return proposal;
    }
}
