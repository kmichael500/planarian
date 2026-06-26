namespace Planarian.Modules.Settings.Models;

public class ChunkedUploaderConfigVm
{
    public int MaxConcurrentUploads { get; set; }
    public long MaxFileSizeBytes { get; set; }
    public int ChunkSizeBytes { get; set; }
}
