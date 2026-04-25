namespace Planarian.Modules.Account.Import.Models;

public class ImportFileUploadSessionMetadata
{
    public string DelimiterRegex { get; set; } = null!;
    public string IdRegex { get; set; } = null!;
    public bool IgnoreDuplicates { get; set; }
    public string? CompletionRequestId { get; set; }
    public FileImportResult? CompletionResult { get; set; }
}