using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class CaveHistoryRecord
{
    [MaxLength(PropertyLength.Id)] public string CaveId { get; set; } = null!;
    [MaxLength(PropertyLength.Id)] public string? EntranceId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? FileId { get; set; }

    [MaxLength(PropertyLength.Name)] public string? EntranceName { get; set; } 
    [MaxLength(PropertyLength.Id)] public string ChangedByUserId { get; set; } = null!;

    [MaxLength(PropertyLength.Id)] public string? ApprovedByUserId { get; set; }


    [MaxLength(PropertyLength.Key)] public string PropertyName { get; set; } = null!;
    public string? PropertyId { get; set; }

    [MaxLength(PropertyLength.Key)] public string ChangeType { get; set; } = null!;
    [MaxLength(PropertyLength.Key)] public string ChangeValueType { get; set; } = null!;

    public string? ValueString { get; set; } 
    public int? ValueInt { get; set; }
    public double? ValueDouble { get; set; }
    public bool? ValueBool { get; set; }
    public DateTime? ValueDateTime { get; set; }
    public DateTime CreatedOn { get; set; }
}

public class CaveHistoryRequest
{
    
}