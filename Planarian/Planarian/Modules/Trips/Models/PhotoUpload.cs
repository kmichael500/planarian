namespace Planarian.Modules.Trips.Models;

public class PhotoUpload
{
    public IFormFile File { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Uid { get; set; } = null!;
}