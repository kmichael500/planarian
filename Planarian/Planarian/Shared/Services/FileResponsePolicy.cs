namespace Planarian.Shared.Services;

internal static class FileResponsePolicy
{
    private const string PlainTextContentType = "text/plain; charset=utf-8";
    private const string BinaryContentType = "application/octet-stream";

    private static readonly HashSet<string> InlineSafeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".pdf"
    };

    private static readonly HashSet<string> ClientParsedTextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".csv",
        ".gpx",
        ".geojson",
        ".json",
        ".kml",
        ".plt"
    };

    private static readonly HashSet<string> ClientParsedBinaryExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip"
    };

    public static (string ContentType, bool ForceDownload) Resolve(
        string? fileName,
        string sourceContentType,
        bool requestedDownload)
    {
        if (requestedDownload)
        {
            return (sourceContentType, true);
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);

        if (InlineSafeExtensions.Contains(extension))
        {
            return (MimeTypes.GetMimeType(extension) ?? sourceContentType, false);
        }

        if (ClientParsedBinaryExtensions.Contains(extension))
        {
            return (BinaryContentType, false);
        }

        if (ClientParsedTextExtensions.Contains(extension))
        {
            return (PlainTextContentType, false);
        }

        return (BinaryContentType, true);
    }
}
