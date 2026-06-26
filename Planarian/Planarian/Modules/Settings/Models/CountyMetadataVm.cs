namespace Planarian.Modules.Settings.Models;

public class CountyMetadataVm
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string DisplayId { get; set; } = null!;
    public string? CountyIdDelimiter { get; set; }
}
