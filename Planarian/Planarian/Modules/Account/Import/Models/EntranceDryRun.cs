using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Import.Models;

public class EntranceDryRun
{
    public string AssociatedCave { get; set; }
    public int EntranceCountChange { get; set; }
    [MaxLength(PropertyLength.Id)] public string LocationQuality { get; set; } = null!;

    public bool IsPrimaryEntrance { get; set; }
    [MaxLength(PropertyLength.Name)] public string? EntranceName { get; set; }
    public string? EntranceDescription { get; set; }

    public double DecimalLatitude { get; set; }
    public double DecimalLongitude { get; set; }
    public double EntranceElevationFt { get; set; }

    public DateTime? ReportedOnDate { get; set; }

    public double? EntrancePitDepth { get; set; }

    public IEnumerable<string> EntranceStatuses { get; set; } = new HashSet<string>();
    public IEnumerable<string> FieldIndication { get; set; } = new HashSet<string>();
    public IEnumerable<string> EntranceHydrology { get; set; } = new HashSet<string>();

    public IEnumerable<string> ReportedByNames { get; set; } = new HashSet<string>();
}