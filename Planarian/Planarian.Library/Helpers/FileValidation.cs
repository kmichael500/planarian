namespace Planarian.Library.Helpers;

public static class FileValidation
{
    public const int MaximumFileNameLength = 1000;
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

public readonly record struct ParsedFileName(string Name, string Extension)
{
    public string CompleteName => FileNamePolicy.Compose(Name, Extension);
}

public static class FileNamePolicy
{
    public static ParsedFileName Parse(string untrustedFileName)
    {
        var normalized = FileValidation.NormalizeUploadedFileName(untrustedFileName);
        ValidateCompleteName(normalized);

        var finalDot = normalized.LastIndexOf('.');
        var hasExtension = finalDot > 0 && finalDot < normalized.Length - 1;
        var extension = hasExtension ? normalized[finalDot..] : string.Empty;
        var name = hasExtension ? normalized[..finalDot] : normalized;
        Compose(name, extension);
        return new ParsedFileName(name, extension);
    }

    public static string Compose(string name, string extension)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(extension);
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A file name is required.", nameof(name));
        if (name is "." or ".." || ContainsUnsafeFileNameCharacters(name))
            throw new ArgumentException("A file name cannot contain path separators or control characters.", nameof(name));
        if (extension.Length > 0)
        {
            if (extension.Length == 1 || extension[0] != '.')
                throw new ArgumentException("A file extension must include its leading dot and a value.", nameof(extension));
            if (ContainsUnsafeFileNameCharacters(extension))
                throw new ArgumentException("A file extension cannot contain path separators or control characters.", nameof(extension));
        }

        var completeName = name + extension;
        ValidateCompleteName(completeName);
        return completeName;
    }

    public static string ComposeEditableName(string existingName, string proposedName, string extension)
    {
        ArgumentNullException.ThrowIfNull(existingName);
        var completeName = Compose(proposedName, extension);
        if (!string.Equals(existingName, proposedName, StringComparison.Ordinal) &&
            extension.Length > 0 && proposedName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The editable file name must not include its fixed extension.", nameof(proposedName));
        return completeName;
    }

    private static bool ContainsUnsafeFileNameCharacters(string value) =>
        value.Any(character => character is '/' or '\\' || char.IsControl(character));

    public static void ValidateCompleteName(string completeName)
    {
        ArgumentNullException.ThrowIfNull(completeName);
        if (completeName.Length == 0)
            throw new ArgumentException("A file name is required.", nameof(completeName));
        if (completeName.Length > FileValidation.MaximumFileNameLength)
            throw new ArgumentException(
                $"A complete file name cannot exceed {FileValidation.MaximumFileNameLength} characters.",
                nameof(completeName));
    }
}
