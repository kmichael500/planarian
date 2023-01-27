namespace Planarian.Modules.TripObjectives.Models;

public class TripPhotoUpload
{
    public IFormFile File { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Uid { get; set; } = null!;
}