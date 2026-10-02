namespace Planarian.Library.Helpers;

public static class FileValidation
{
    private static readonly HashSet<string> ValidPhotoFileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a };

    public static bool IsValidPhotoFileType(string fileType)
    {
        return ValidPhotoFileTypes.Contains(NormalizeFileTypeName(fileType));
    }

    public static bool IsValidPhotoFile(Stream stream, string fileName)
    {
        var fileType = NormalizeFileTypeName(Path.GetExtension(NormalizeUploadedFileName(fileName)));
        if (!IsValidPhotoFileType(fileType) || !stream.CanRead)
        {
            return false;
        }

        Span<byte> header = stackalloc byte[PngSignature.Length];
        var originalPosition = stream.CanSeek ? stream.Position : (long?)null;

        try
        {
            var bytesRead = 0;
            while (bytesRead < header.Length)
            {
                var read = stream.Read(header[bytesRead..]);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }

            return fileType switch
            {
                ".jpg" or ".jpeg" => bytesRead >= 3 &&
                                     header[0] == 0xff &&
                                     header[1] == 0xd8 &&
                                     header[2] == 0xff,
                ".png" => bytesRead >= PngSignature.Length && header.SequenceEqual(PngSignature),
                _ => false
            };
        }
        finally
        {
            if (originalPosition.HasValue)
            {
                stream.Position = originalPosition.Value;
            }
        }
    }

    public static string NormalizeUploadedFileName(string fileName)
    {
        return Path.GetFileName(fileName.Replace('\\', '/'));
    }

    public static string NormalizeFileTypeName(string fileType)
    {
        return fileType.ToLowerInvariant();
    }
}
