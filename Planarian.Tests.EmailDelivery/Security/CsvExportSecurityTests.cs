using System.Formats.Tar;
using System.Reflection;
using System.Text;
using CsvHelper;
using Planarian.Modules.Account.Archive.Models;
using Planarian.Modules.Account.Archive.Services;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Security;

public sealed class CsvExportSecurityTests
{
    [Fact]
    public void CsvWritersAreCentralizedBehindTheSecurityPolicy()
    {
        var repositoryRoot = FindRepositoryRoot();
        var productionRoot = Path.Combine(repositoryRoot, "Planarian", "Planarian");
        var directWriterFiles = Directory.EnumerateFiles(productionRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                           !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => File.ReadAllText(path).Contains("new CsvWriter(", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/'))
            .OrderBy(path => path)
            .ToArray();

        Assert.Equal(new[] { "Planarian/Planarian/Shared/Services/CsvExportPolicy.cs" }, directWriterFiles);
    }

    [Fact]
    public void CsvWriterPolicyEscapesDangerousStringsWithoutChangingNegativeNumbers()
    {
        using var textWriter = new StringWriter();
        using (var csv = CreateCsvWriter(textWriter))
        {
            csv.WriteRecords(new[]
            {
                new CsvSecurityProbe(-86.75, "-user text", "=formula", "normal")
            });
        }

        var output = textWriter.ToString();
        Assert.Contains("-86.75", output);
        Assert.DoesNotContain("'-86.75", output);
        Assert.Contains("'-user text", output);
        Assert.Contains("'=formula", output);
    }

    [Fact]
    public async Task MissingArchiveCsvEscapesSpreadsheetFormulaPrefixes()
    {
        var missingFiles = new[]
        {
            new MissingArchiveFile("=1+1", "@SUM(1,1)", "+entry", "-blob", "\tformula"),
            new MissingArchiveFile("safe", "safe", "safe", "safe", "\rformula")
        };

        using var archiveStream = new MemoryStream();
        await using (var archive = new TarWriter(archiveStream, TarEntryFormat.Pax, leaveOpen: true))
        {
            var method = typeof(ExportService).GetMethod(
                "WriteMissingFilesEntry",
                BindingFlags.NonPublic | BindingFlags.Static)!;
            var task = (Task)method.Invoke(
                null,
                [archive, missingFiles, CancellationToken.None])!;
            await task;
        }

        archiveStream.Position = 0;
        using var reader = new TarReader(archiveStream, leaveOpen: true);
        var entry = reader.GetNextEntry();
        Assert.NotNull(entry);
        Assert.Equal("missing-files.csv", entry!.Name);
        Assert.NotNull(entry.DataStream);

        using var contentReader = new StreamReader(entry.DataStream!, Encoding.UTF8);
        var csv = await contentReader.ReadToEndAsync();

        Assert.StartsWith("CaveDisplayId,CaveName,EntryPath,BlobKey,Reason", csv);
        Assert.Contains("'=1+1", csv);
        Assert.Contains("'@SUM(1,1)", csv);
        Assert.Contains("'+entry", csv);
        Assert.Contains("'-blob", csv);
        Assert.Contains("'\tformula", csv);
        Assert.Contains("'\rformula", csv);
    }

    private static CsvWriter CreateCsvWriter(TextWriter writer)
    {
        var policyType = typeof(ExportService).Assembly.GetType(
            "Planarian.Shared.Services.CsvExportPolicy",
            throwOnError: true)!;
        var createWriter = policyType.GetMethod(
            "CreateWriter",
            BindingFlags.Public | BindingFlags.Static)!;

        return (CsvWriter)createWriter.Invoke(null, new object[] { writer })!;
    }

    private sealed record CsvSecurityProbe(
        double Longitude,
        string MinusText,
        string Formula,
        string Normal);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory != null;
             directory = directory.Parent)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
