using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Services;

public class FavoriteVm
{
    [MaxLength(PropertyLength.Id)] public string? AccountId { get; set; }
    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }

    [MaxLength(PropertyLength.Max)] public string? Notes { get; set; }

    public List<string> Tags { get; set; } = new();
}