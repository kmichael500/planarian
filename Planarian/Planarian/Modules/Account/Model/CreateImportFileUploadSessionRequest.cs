namespace Planarian.Modules.Account.Model;

public class CreateImportFileUploadSessionRequest
{
    public string FileName { get; set; } = null!;
    public long FileSize { get; set; }
    public string DelimiterRegex { get; set; } = null!;
    public string IdRegex { get; set; } = null!;
    public bool IgnoreDuplicates { get; set; }
    public string? RequestId { get; set; }
}
