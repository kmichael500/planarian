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

public sealed record SnapshotReference(string Id, string NameAtRevision);

public sealed record SnapshotTagReference(string TagTypeId, string NameAtRevision, string Key);

public sealed record CaveEntranceSnapshotV1
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public bool IsPrimary { get; init; }
    public string? Description { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? Elevation { get; init; }
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
    public string? BlobContainer { get; init; }
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

        return JsonSerializer.Deserialize<CavePublishedSnapshotV1>(json, Options)
               ?? throw new InvalidOperationException("Cave snapshot JSON was empty.");
    }
}
