using System.Text.Json;
using System.Text.Json.Serialization;

namespace Planarian.Model.Database.Revisions;

public sealed record CavePublishedSnapshotV1
{
    public int SchemaVersion { get; init; } = 1;
    public string CaveId { get; init; } = null!;
    public string AccountId { get; init; } = null!;
    public string Name { get; init; } = null!;
    public IReadOnlyList<string> AlternateNames { get; init; } = [];
    public SnapshotReference State { get; init; } = null!;
    public SnapshotReference County { get; init; } = null!;
    public int CountyNumber { get; init; }
    public string? ReportedByUserId { get; init; }
    public double? LengthFeet { get; init; }
    public double? DepthFeet { get; init; }
    public double? MaxPitDepthFeet { get; init; }
    public int? NumberOfPits { get; init; }
    public string? Narrative { get; init; }
    public DateTime? ReportedOn { get; init; }
    public bool IsArchived { get; init; }
    public IReadOnlyList<SnapshotTagReference> Tags { get; init; } = [];
    public IReadOnlyList<CaveEntranceSnapshotV1> Entrances { get; init; } = [];
    public IReadOnlyList<CaveFileSnapshotV1> Files { get; init; } = [];
}

public sealed record SnapshotReference(string Id, string NameAtRevision, string? DisplayIdAtRevision = null,
    string? AbbreviationAtRevision = null);

public enum SnapshotTagRole
{
    Geology, GeologicAge, MapStatus, PhysiographicProvince, Archeology, Biology, CaveOther,
    Cartographer, CaveReportedBy, EntranceStatus, EntranceHydrology, FieldIndication,
    EntranceReportedBy, EntranceOther
}

public sealed record SnapshotTagReference(SnapshotTagRole Role, string TagTypeId, string NameAtRevision);

public sealed record CaveEntranceSnapshotV1
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public bool IsPrimary { get; init; }
    public string? Description { get; init; }
    public string? ReportedByUserId { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? Elevation { get; init; }
    public int Srid { get; init; } = 4326;
    public string LocationQualityTagId { get; init; } = null!;
    public string LocationQualityNameAtRevision { get; init; } = null!;
    public DateTime? ReportedOn { get; init; }
    public double? PitDepthFeet { get; init; }
    public IReadOnlyList<SnapshotTagReference> Tags { get; init; } = [];
}

public sealed record CaveFileSnapshotV1
{
    public string Id { get; init; } = null!;
    public string FileTypeTagId { get; init; } = null!;
    public string FileTypeNameAtRevision { get; init; } = null!;
    public string FileName { get; init; } = null!;
    public string? DisplayName { get; init; }
}

public static class CaveSnapshotJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(CavePublishedSnapshotV1 snapshot) =>
        JsonSerializer.Serialize(snapshot, Options);

    public static CavePublishedSnapshotV1 Deserialize(string json, int schemaVersion)
    {
        if (schemaVersion != 1)
            throw new NotSupportedException($"Unsupported Cave snapshot schema version: {schemaVersion}.");

        var snapshot = JsonSerializer.Deserialize<CavePublishedSnapshotV1>(json, Options)
                       ?? throw new InvalidOperationException("Cave snapshot JSON was empty.");
        if (snapshot.SchemaVersion != schemaVersion)
            throw new InvalidOperationException("Cave snapshot row and payload schema versions differ.");
        return snapshot;
    }
}
