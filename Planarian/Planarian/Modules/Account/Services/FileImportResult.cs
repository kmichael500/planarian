namespace Planarian.Modules.Account.Services;

public class FileImportResult
{
    public string FileName { get; set; }
    public bool IsSuccessful { get; set; }
    public string AssociatedCave { get; set; }
    public string Message { get; set; }
}