namespace Planarian.Modules.Photos.Models;

public class PhotoVm
{
    public PhotoVm(string id, string? title, string? description, string url)
    {
        Id = id;
        Title = title;
        Description = description;
        Url = url;
    }

    public PhotoVm()
    {
    }

    public string Id { get; set; } = null!;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string Url { get; set; }
}