namespace Planarian.Shared.Models;

public class ProgressVm
{
    public string StatusMessage { get; set; } = string.Empty;
    public int? ProcessedCount { get; set; }
    public int? TotalCount { get; set; }
    public ProgressState? State { get; set; }
}
