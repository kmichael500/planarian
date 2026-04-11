namespace Planarian.Modules.Account.Model;

public class ArchiveStatusVm
{
    public bool IsActive { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public int? ProcessedCount { get; set; }
    public int? TotalCount { get; set; }
    public string? Message { get; set; }
    public bool? IsError { get; set; }
    public bool? IsCanceled { get; set; }
}
