namespace Planarian.Modules.TripObjectives.Controllers;

public class TripPhotoUpload
{
    public IFormFile File { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Uid { get; set; } = null!;
}