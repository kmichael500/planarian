namespace Planarian.Library.Helpers;

public static class FileValidation
{
    private static readonly List<string> ValidPhotoFileTypes = new() { ".png", ".jpg", ".jpeg" };

    public static bool IsValidPhotoFileType(string fileType)
    {
        var fileTypeNormalized = NormalizeFileTypeName(fileType);

        return ValidPhotoFileTypes.Contains(fileTypeNormalized);
    }

    public static string NormalizeFileTypeName(string fileType)
    {
        var fileTypeNormalized = fileType.ToLowerInvariant();

        return fileTypeNormalized;
    }
}