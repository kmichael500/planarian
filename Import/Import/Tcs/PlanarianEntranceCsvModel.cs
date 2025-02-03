using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;

namespace Tcs;

public class EntranceCsvModel
{
    public string? CountyCode { get; set; }
    public string? CountyCaveNumber { get; set; }
    public string? EntranceName { get; set; }
    public double? DecimalLatitude { get; set; }
    public double? DecimalLongitude { get; set; }
    public double? EntranceElevationFt { get; set; }
    public string? LocationQuality { get; set; }
    public string? EntranceDescription { get; set; }
    public double? EntrancePitDepth { get; set; }
    public string? EntranceStatuses { get; set; }
    public string? EntranceHydrology { get; set; }
    public string? FieldIndication { get; set; }
    public string? ReportedOnDate { get; set; }
    public string? ReportedByNames { get; set; }
    public bool? IsPrimaryEntrance { get; set; }
    [Ignore] [JsonIgnore] public string? EntranceId { get; set; }
}