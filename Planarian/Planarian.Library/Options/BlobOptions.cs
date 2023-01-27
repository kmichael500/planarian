namespace Planarian.Library.Options;

public class BlobOptions
{
    public const string Key = "Blob";
    public string ConnectionString { get; set; } = null!;
    public string ContainerName { get; set; } = null!;
}