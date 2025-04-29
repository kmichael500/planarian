using System.ComponentModel.DataAnnotations;
using Planarian.Model.Database.Entities.RidgeWalker.ViewModels;
using Planarian.Model.Shared;

namespace Planarian.Modules.Caves.Models;

public class ReviewChangeRequest
{
    public string? Id { get; set; } 
    public bool Approve { get; set; }
    
    [MaxLength(PropertyLength.LargeText)] public string? Notes { get; set; }
}