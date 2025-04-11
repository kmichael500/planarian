using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class FavoriteVm
{
    [MaxLength(PropertyLength.Id)] public string? CaveId { get; set; }
}