namespace Planarian.Modules.Account.Model;

public sealed class AccountBackupFileByCaveDto : AccountBackupFileDto
{
    public string CavePlanarianId { get; set; } = null!;
}