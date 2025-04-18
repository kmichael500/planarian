namespace Planarian.Modules.Caves.Models;

public class ChangesForReviewVm
{
    public string Id { get; set; } = null!;
    public string CaveName { get; set; } = null!;
    public string? CaveDisplayId { get; set; }
    public string CountyId { get; set; } = null!;
    
    public bool IsNew { get; set; }
    
    public DateTime SubmittedOn { get; set; }
    public string SubmittedByUserId { get; set; }
}