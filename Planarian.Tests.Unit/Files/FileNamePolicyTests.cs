using Planarian.Library.Helpers;
using Xunit;

namespace Planarian.Tests.Unit.Files;

public sealed class FileNamePolicyTests
{
    [Theory]
    [InlineData("survey.pdf", "survey", ".pdf")]
    [InlineData("survey.final.pdf", "survey.final", ".pdf")]
    [InlineData("survey.pdf.pdf", "survey.pdf", ".pdf")]
    [InlineData("survey", "survey", "")]
    [InlineData("survey.PDF", "survey", ".PDF")]
    [InlineData("Mügelhöhle.plan.pdf", "Mügelhöhle.plan", ".pdf")]
    [InlineData("../../nested/map.pdf", "map", ".pdf")]
    [InlineData(@"C:\fakepath\map.pdf", "map", ".pdf")]
    [InlineData(".gitignore", ".gitignore", "")]
    [InlineData("survey.", "survey.", "")]
    [InlineData(".env.local", ".env", ".local")]
    public void ParsePreservesTheExactNormalizedCompleteName(
        string untrustedFileName, string expectedName, string expectedExtension)
    {
        var parsed = FileNamePolicy.Parse(untrustedFileName);

        Assert.Equal(expectedName, parsed.Name);
        Assert.Equal(expectedExtension, parsed.Extension);
        Assert.Equal(FileValidation.NormalizeUploadedFileName(untrustedFileName), parsed.CompleteName);
    }

    [Fact]
    public void CompleteNameMayUseTheFullSupportedLength()
    {
        var maximum = new string('a', FileValidation.MaximumFileNameLength - 4) + ".pdf";

        Assert.Equal(maximum, FileNamePolicy.Parse(maximum).CompleteName);
    }

    [Fact]
    public void ParseRejectsACompleteNamePastTheSupportedLength()
    {
        var oversized = new string('a', FileValidation.MaximumFileNameLength - 3) + ".pdf";

        Assert.Throws<ArgumentException>(() => FileNamePolicy.Parse(oversized));
    }

    [Fact]
    public void ComposeRejectsACompleteNamePastTheSupportedLength()
    {
        Assert.Throws<ArgumentException>(() => FileNamePolicy.Compose(
            new string('a', FileValidation.MaximumFileNameLength), ".x"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\nname.pdf")]
    [InlineData(".")]
    [InlineData("..")]
    public void ParseRejectsInvalidNormalizedFileNames(string fileName)
    {
        Assert.Throws<ArgumentException>(() => FileNamePolicy.Parse(fileName));
    }

    [Theory]
    [InlineData("folder/name")]
    [InlineData(@"folder\name")]
    [InlineData("bad\nname")]
    [InlineData("   ")]
    [InlineData(".")]
    [InlineData("..")]
    public void ComposeRejectsInvalidNames(string name)
    {
        Assert.Throws<ArgumentException>(() => FileNamePolicy.Compose(name, ".pdf"));
    }

    [Theory]
    [InlineData("survey.pdf", ".pdf")]
    [InlineData("survey.PDF", ".pdf")]
    public void EditableNameCannotNewlyRepeatTheFixedExtension(string proposedName, string extension)
    {
        Assert.Throws<ArgumentException>(() =>
            FileNamePolicy.ComposeEditableName("survey", proposedName, extension));
    }

    [Fact]
    public void ExistingRepeatedExtensionRemainsValidWhenNameIsUnchanged()
    {
        Assert.Equal("survey.pdf.pdf",
            FileNamePolicy.ComposeEditableName("survey.pdf", "survey.pdf", ".pdf"));
    }

    [Theory]
    [InlineData("pdf")]
    [InlineData(".")]
    [InlineData(".p/df")]
    [InlineData(@".p\df")]
    [InlineData(".p\ndf")]
    public void ComposeRejectsMalformedExtensions(string extension)
    {
        Assert.Throws<ArgumentException>(() => FileNamePolicy.Compose("survey", extension));
    }
}
