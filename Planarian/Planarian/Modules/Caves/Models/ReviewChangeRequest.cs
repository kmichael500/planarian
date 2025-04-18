using System.ComponentModel.DataAnnotations;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class ReviewChangeRequest
{
    public string? Id { get; set; } 
    public bool Approve { get; set; }
    
    public AddCaveVm Cave { get; set; } = null!;
    
    [MaxLength(PropertyLength.LargeText)] public string? Notes { get; set; }
}