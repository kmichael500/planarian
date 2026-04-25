using Planarian.Shared.Services;

namespace Planarian.Modules.Account.Import.Models;

public class ImportFileRequest : ChunkedUploadSessionCreateRequest
{
    public string DelimiterRegex { get; set; } = null!;
    public string IdRegex { get; set; } = null!;
    public bool IgnoreDuplicates { get; set; }
}
