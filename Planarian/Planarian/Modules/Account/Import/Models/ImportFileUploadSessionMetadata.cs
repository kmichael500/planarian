namespace Planarian.Modules.Account.Import.Models;

public class ImportFileUploadSessionMetadata
{
    public string DelimiterRegex { get; set; } = null!;
    public string IdRegex { get; set; } = null!;
    public bool IgnoreDuplicates { get; set; }
    public string? CompletionRequestId { get; set; }
    public FileImportResult? CompletionResult { get; set; }

    // Shared by all request-scoped ImportService instances for this upload session.
    // Serializes the complete publication and cached result, not just Azure block commit.
    public SemaphoreSlim FinalizationGate { get; } = new(1, 1);
}