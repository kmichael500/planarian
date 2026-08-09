using System.Reflection;
using System.Text.Json;
using Xunit;

namespace Planarian.Tests;

public sealed class MainImportBehaviorGoldenFixtureTests
{
    // This is deliberately the exact historical oracle, not the current branch.
    private const string BaselineCommit="11cdd9edc58d85bcf14a9d82c797f715d3a0e2ae";

    [Fact]
    public void GoldenMainCasesAreAnchoredCompleteAndBackedByExecutableTests()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"Fixtures","import-main-11cdd9e.json");
        using var document=JsonDocument.Parse(System.IO.File.ReadAllText(path));var root=document.RootElement;
        Assert.Equal(BaselineCommit,root.GetProperty("baselineCommit").GetString());var cases=root.GetProperty("cases").EnumerateArray().ToList();Assert.NotEmpty(cases);
        var required=new[]{"required-fields","invalid-numeric","optional-date","state-resolution","county-creation","reference-case","tags-and-insert","update-and-preserve-relationships","no-change","sync-deletion","required-coordinates","coordinate-ranges","elevation-pit-validation","location-quality-tags-geometry","append-primary-rules","sync-replacement","targeted-deletion-scope"};
        foreach(var behavior in required)Assert.Contains(cases,x=>x.GetProperty("behavior").GetString()==behavior);
        var testTypes=new[]{typeof(CaveImportCompatibilityTests),typeof(EntranceImportCompatibilityTests),typeof(ImportPreviewCommitEquivalenceTests)};
        foreach(var item in cases){Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("inputCsv").GetString()));Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("validation").GetString()));Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("preview").GetString()));Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("committed").GetString()));var method=item.GetProperty("coverageTest").GetString();Assert.Contains(testTypes.SelectMany(x=>x.GetMethods(BindingFlags.Public|BindingFlags.Instance)).Select(x=>x.Name),x=>x==method);}
    }
}
