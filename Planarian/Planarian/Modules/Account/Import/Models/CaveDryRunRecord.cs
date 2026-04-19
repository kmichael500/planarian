using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Account.Import.Models;

public class CaveDryRunRecord
{
    public string CountyCode { get; set; } = null!;
    public int CountyCaveNumber { get; set; }
    [MaxLength(PropertyLength.Name)] public string CaveName { get; set; } = null!;
    public string? ChangesSummary { get; set; }
    public string Action { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string State { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string CountyName { get; set; } = null!;
    public IEnumerable<string> AlternateNames { get; set; } = new List<string>();

    public double? CaveLengthFeet { get; set; }
    public double? CaveDepthFeet { get; set; }
    public double? MaxPitDepthFeet { get; set; }
    public int? NumberOfPits { get; set; } = 0;
    public DateTime? ReportedOnDate { get; set; }
    public bool IsArchived { get; set; } = false;
    public IEnumerable<string> Geology { get; set; } = new HashSet<string>();
    public IEnumerable<string> ReportedByNames { get; set; } = new HashSet<string>();
    public IEnumerable<string> Biology { get; set; } = new HashSet<string>();
    public IEnumerable<string> Archeology { get; set; } = new HashSet<string>();
    public IEnumerable<string> CartographerNames { get; set; } = new HashSet<string>();
    public IEnumerable<string> GeologicAges { get; set; } = new HashSet<string>();
    public IEnumerable<string> PhysiographicProvinces { get; set; } = new HashSet<string>();
    public IEnumerable<string> OtherTags { get; set; } = new HashSet<string>();
    public IEnumerable<string> MapStatuses { get; set; } = new HashSet<string>();
    public string? Narrative { get; set; }
}