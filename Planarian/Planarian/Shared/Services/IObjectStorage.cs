namespace Planarian.Shared.Services;

public sealed record StorageObjectAddress(string Partition, string Key);

public sealed record StoredObjectReadResult(
    Func<CancellationToken, Task<Stream>> OpenReadStreamAsync,
    string? ContentType,
    string? EntityTag,
    DateTimeOffset? LastModified);

public interface IObjectStorage
{
    Task PutAsync(StorageObjectAddress address, Stream content, string? contentType,
        CancellationToken cancellationToken);

    Task<StoredObjectReadResult?> OpenReadAsync(StorageObjectAddress address,
        CancellationToken cancellationToken);

    Task<bool> DeleteIfExistsAsync(StorageObjectAddress address,
        CancellationToken cancellationToken);
}
