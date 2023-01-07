namespace Planarian.Library.Helpers;

public static class FileValidation
{
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

    private static readonly List<string> ValidPhotoFileTypes = new List<string> { ".png", ".jpg", ".jpeg" };
}