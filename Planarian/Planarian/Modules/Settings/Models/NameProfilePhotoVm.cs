namespace Planarian.Modules.Settings.Models;

public class NameProfilePhotoVm
{
    public NameProfilePhotoVm(string name, string? blobKey) : this(name)
    {
        BlobKey = blobKey;
    }

    private NameProfilePhotoVm(string name)
    {
        Name = name;
    }

    public NameProfilePhotoVm()
    {
    }

    public string Name { get; set; } = null!;
    public string? BlobKey { get; set; }

    public string? ProfilePhotoUrl { get; set; }
}