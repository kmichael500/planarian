using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class ReportedByNameVm
{
    [MaxLength(PropertyLength.Id)]public string? TagTypeId { get; set; }
    [MaxLength(PropertyLength.Name)]public string? Name { get; set; }
}