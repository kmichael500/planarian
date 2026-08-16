using System.Text;
using Planarian.Library.Exceptions;
using Planarian.Modules.Import.Parsing;
using Xunit;

namespace Planarian.Tests;

public sealed class ImportCsvParserTests
{
    [Theory]
    [InlineData("CaveLengthFt")]
    [InlineData("CaveDepthFt")]
    [InlineData("MaxPitDepthFt")]
    [InlineData("NumberOfPits")]
    [InlineData("IsArchived")]
    public async Task MalformedOptionalCaveTypedValueIsRejected(string field)
    {
        var csv = $"CaveName,CountyName,CountyCode,CountyCaveNumber,State,{field}\n" +
                  "Test Cave,Test County,TC,1,TN,not-a-value\n";

        await Assert.ThrowsAsync<ApiException>(() =>
            new CaveImportCsvParser().ParseAsync(Stream(csv)));
    }

    [Theory]
    [InlineData("EntrancePitDepth")]
    [InlineData("IsPrimaryEntrance")]
    public async Task MalformedOptionalEntranceTypedValueIsRejected(string field)
    {
        var csv = "CountyCode,CountyCaveNumber,DecimalLatitude,DecimalLongitude,EntranceElevationFt,LocationQuality," + field + "\n" +
                  "TC,1,35.5,-86.5,500,GPS,not-a-value\n";

        await Assert.ThrowsAsync<ApiException>(() =>
            new EntranceImportCsvParser().ParseAsync(Stream(csv)));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public async Task NonFiniteCaveNumberIsRejected(string value)
    {
        var csv = "CaveName,CountyName,CountyCode,CountyCaveNumber,State,CaveLengthFt\n" +
                  $"Test Cave,Test County,TC,1,TN,{value}\n";

        var exception = await Assert.ThrowsAsync<ApiException>(() =>
            new CaveImportCsvParser().ParseAsync(Stream(csv)));
        var failure = Assert.Single(Assert.IsAssignableFrom<IEnumerable<FailedCaveCsvRecord<Planarian.Modules.Import.Models.CaveCsvModel>>>(exception.Data));
        Assert.Null(failure.CaveCsvModel.CaveLengthFt);
    }

    [Theory]
    [InlineData("DecimalLatitude", "NaN")]
    [InlineData("DecimalLongitude", "Infinity")]
    [InlineData("EntranceElevationFt", "-Infinity")]
    [InlineData("EntrancePitDepth", "NaN")]
    public async Task NonFiniteEntranceNumberIsRejected(string field, string value)
    {
        var latitude = field == "DecimalLatitude" ? value : "35.5";
        var longitude = field == "DecimalLongitude" ? value : "-86.5";
        var elevation = field == "EntranceElevationFt" ? value : "500";
        var pitDepth = field == "EntrancePitDepth" ? value : "1";
        var csv = "CountyCode,CountyCaveNumber,DecimalLatitude,DecimalLongitude,EntranceElevationFt,LocationQuality,EntrancePitDepth\n" +
                  $"TC,1,{latitude},{longitude},{elevation},GPS,{pitDepth}\n";

        await Assert.ThrowsAsync<ApiException>(() =>
            new EntranceImportCsvParser().ParseAsync(Stream(csv)));
    }

    [Fact]
    public async Task BlankOptionalStringColumnsRemainBlank()
    {
        var caveCsv = "CaveName,CountyName,CountyCode,CountyCaveNumber,State,Narrative\n" +
                      "Test Cave,Test County,TC,1,TN,\n";
        var entranceCsv = "CountyCode,CountyCaveNumber,DecimalLatitude,DecimalLongitude,EntranceElevationFt,LocationQuality,EntranceDescription\n" +
                          "TC,1,35.5,-86.5,500,GPS,\n";

        Assert.Equal(string.Empty, Assert.Single(await new CaveImportCsvParser().ParseAsync(Stream(caveCsv))).Narrative);
        Assert.Equal(string.Empty, Assert.Single(await new EntranceImportCsvParser().ParseAsync(Stream(entranceCsv))).EntranceDescription);
    }

    [Fact]
    public async Task BlankOptionalTypedValuesRemainAllowed()
    {
        var caveCsv = "CaveName,CountyName,CountyCode,CountyCaveNumber,State,CaveLengthFt,IsArchived\n" +
                      "Test Cave,Test County,TC,1,TN,,\n";
        var entranceCsv = "CountyCode,CountyCaveNumber,DecimalLatitude,DecimalLongitude,EntranceElevationFt,LocationQuality,EntrancePitDepth,IsPrimaryEntrance\n" +
                          "TC,1,35.5,-86.5,500,GPS,,\n";

        Assert.Single(await new CaveImportCsvParser().ParseAsync(Stream(caveCsv)));
        Assert.Single(await new EntranceImportCsvParser().ParseAsync(Stream(entranceCsv)));
    }

    private static MemoryStream Stream(string csv) => new(Encoding.UTF8.GetBytes(csv));
}
