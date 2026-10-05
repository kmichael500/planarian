namespace Planarian.Modules.App.Models;

public class RevokedAccountVm
{
    public string Display { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? Reason { get; set; }
}
