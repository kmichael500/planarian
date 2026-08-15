using System.Text;
using Planarian.Library.Helpers;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Security;

public sealed class FileUploadValidationTests
{
    private static readonly byte[] JpegHeader = [0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.jpeg")]
    public void JpegUploadsRequireJpegContent(string fileName)
    {
        Assert.True(IsValidPhoto(fileName, JpegHeader));
        Assert.False(IsValidPhoto(fileName, Encoding.UTF8.GetBytes("<html><script>alert(1)</script>")));
    }

    [Fact]
    public void PngUploadsRequirePngContent()
    {
        Assert.True(IsValidPhoto("photo.png", PngHeader));
        Assert.False(IsValidPhoto("photo.png", JpegHeader));
    }

    [Fact]
    public void PhotoContentMustMatchTheDeclaredPhotoExtension()
    {
        Assert.False(IsValidPhoto("photo.jpg", PngHeader));
        Assert.False(IsValidPhoto("photo.txt", JpegHeader));
    }

    [Theory]
    [InlineData("survey.custom", "survey.custom")]
    [InlineData("payload.html", "payload.html")]
    [InlineData("tool.exe", "tool.exe")]
    [InlineData("../../nested/map.pdf", "map.pdf")]
    [InlineData(@"C:\fakepath\map.pdf", "map.pdf")]
    public void UploadedFileNamesAreNormalizedWithoutRestrictingAttachmentTypes(
        string untrustedFileName,
        string expectedFileName)
    {
        Assert.Equal(expectedFileName, NormalizeUploadedFileName(untrustedFileName));
    }

    private static bool IsValidPhoto(string fileName, byte[] content)
    {
        using var stream = new MemoryStream(content);
        return FileValidation.IsValidPhotoFile(stream, fileName);
    }

    private static string NormalizeUploadedFileName(string fileName)
    {
        return FileValidation.NormalizeUploadedFileName(fileName);
    }
}
