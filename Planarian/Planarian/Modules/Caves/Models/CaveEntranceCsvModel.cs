using CsvHelper.Configuration.Attributes;

namespace Planarian.Modules.Caves.Models;

public class CaveEntranceCsvModel
{
    public string CaveId { get; set; } = null!;

    public string? CaveName { get; set; }

    public string? CaveAlternateNames { get; set; }

    public string? CaveCounty { get; set; }

    public string CaveCountyDisplayId { get; set; } = null!;

    public string? CaveState { get; set; }

    public int CaveCountyNumber { get; set; }

    public double? CaveLengthFeet { get; set; }

    public double? CaveDepthFeet { get; set; }

    public double? CaveMaxPitDepthFeet { get; set; }

    public int? CaveNumberOfPits { get; set; }

    public string? CaveNarrative { get; set; }

    public DateTime? CaveReportedOn { get; set; }

    public bool CaveIsArchived { get; set; }

    public string? CaveGeologyTags { get; set; }

    public string? CaveMapStatusTags { get; set; }

    public string? CaveGeologicAgeTags { get; set; }

    public string? CavePhysiographicProvinceTags { get; set; }

    public string? CaveBiologyTags { get; set; }

    public string? CaveArcheologyTags { get; set; }

    public string? CaveCartographerNameTags { get; set; }

    public string? CaveReportedByTags { get; set; }

    public string? CaveOtherTags { get; set; }

    public string? EntranceName { get; set; }

    public string? EntranceDescription { get; set; }

    public bool EntranceIsPrimary { get; set; }

    public DateTime? EntranceReportedOn { get; set; }

    public double? EntrancePitDepthFeet { get; set; }

    public double EntranceLatitude { get; set; }

    public double EntranceLongitude { get; set; }

    public double EntranceElevation { get; set; }

    public string? EntranceLocationQuality { get; set; }

    public string? EntranceStatusTags { get; set; }

    public string? EntranceFieldIndicationTags { get; set; }

    public string? EntranceHydrologyTags { get; set; }

    public string? EntranceReportedByTags { get; set; }

    public string? EntranceOtherTags { get; set; }
}