using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;

namespace Planarian.Modules.Import.Models;

public class EntranceCsvModel
{
    public string CountyCaveNumber { get; set; }
    public string? EntranceName { get; set; }
    public string? EntranceDescription { get; set; }
    public bool? IsPrimaryEntrance { get; set; }
    public double? EntrancePitDepth { get; set; }
    public string? EntranceStatus { get; set; }
    public string? EntranceHydrology { get; set; }
    public string? EntranceHydrologyFrequency { get; set; }
    public string? FieldIndication { get; set; }
    public string? CountyCode { get; set; }
    public double? DecimalLatitude { get; set; }
    public double? DecimalLongitude { get; set; }
    public double? EntranceElevationFt { get; set; }
    public string? GeologyFormation { get; set; }
    public string? ReportedOnDate { get; set; }
    public string? ReportedByName { get; set; }
    public string? LocationQuality { get; set; }
    [Ignore] [JsonIgnore] public string? EntranceId { get; set; }

}