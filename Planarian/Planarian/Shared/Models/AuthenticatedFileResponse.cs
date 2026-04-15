using Microsoft.Net.Http.Headers;

namespace Planarian.Shared.Models;

public sealed class AuthenticatedFileResponse
{
    public required Stream Stream { get; init; }
    public required string ContentType { get; init; }
    public string? FileName { get; init; }
    public bool Download { get; init; }
    public EntityTagHeaderValue? EntityTag { get; init; }
    public DateTimeOffset? LastModified { get; init; }
}
