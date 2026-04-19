namespace Planarian.Modules.Account.Import.Models;

public class ImportFileUploadSessionVm
{
    public string SessionId { get; set; } = null!;
    public long UploadedBytes { get; set; }
    public long TotalBytes { get; set; }
    public string Status { get; set; } = null!;
}
