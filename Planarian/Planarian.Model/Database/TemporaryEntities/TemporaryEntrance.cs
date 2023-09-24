using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;
using Planarian.Model.Shared.Base;

namespace Planarian.Model.Database.TemporaryEntities;

public class TemporaryEntrance : EntityBase
{
    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }
    [MaxLength(PropertyLength.SmallText)] public string CountyDisplayId { get; set; } = null!;
    public int CountyCaveNumber { get; set; }

    [MaxLength(PropertyLength.Id)] public string? ReportedByUserId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string LocationQualityTagId { get; set; } = null!;


    [MaxLength(PropertyLength.Name)] public string? Name { get; set; }

    public bool IsPrimary { get; set; } = false;
    public string? Description { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Elevation { get; set; }

    public DateTime? ReportedOn { get; set; }
    [MaxLength(PropertyLength.Name)] public string? ReportedByName { get; set; }

    public double? PitFeet { get; set; }
}