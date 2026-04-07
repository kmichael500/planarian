namespace Planarian.Modules.Account.Model;

public sealed class AccountBackupDownloadVm
{
    public string FileName { get; set; } = null!;
    public string DownloadUrl { get; set; } = null!;
}