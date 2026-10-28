using System.Reflection;
using CsvHelper.Configuration.Attributes;
using Planarian.Modules.Import.Models;
using Xunit;

namespace Planarian.Tests.Unit.Import;

/// <summary>
/// Drift guard for the CSV import contracts. Persistence semantics are exercised by the real-PostgreSQL
/// parity/lifecycle tests; these declarations make a newly added import field an explicit test decision.
/// </summary>
public sealed class ImportFieldCoverageTests
{
    [Fact]
    public void CaveCsvFieldMatrixMustBeUpdatedWhenContractChanges() =>
        AssertDeclaredProperties<CaveCsvModel>(
            nameof(CaveCsvModel.CaveName),
            nameof(CaveCsvModel.AlternateNames),
            nameof(CaveCsvModel.State),
            nameof(CaveCsvModel.CountyCode),
            nameof(CaveCsvModel.CountyCaveNumber),
            nameof(CaveCsvModel.CountyName),
            nameof(CaveCsvModel.MapStatuses),
            nameof(CaveCsvModel.CartographerNames),
            nameof(CaveCsvModel.CaveLengthFt),
            nameof(CaveCsvModel.CaveDepthFt),
            nameof(CaveCsvModel.MaxPitDepthFt),
            nameof(CaveCsvModel.NumberOfPits),
            nameof(CaveCsvModel.Narrative),
            nameof(CaveCsvModel.Geology),
            nameof(CaveCsvModel.GeologicAges),
            nameof(CaveCsvModel.PhysiographicProvinces),
            nameof(CaveCsvModel.Archeology),
            nameof(CaveCsvModel.Biology),
            nameof(CaveCsvModel.ReportedOnDate),
            nameof(CaveCsvModel.ReportedByNames),
            nameof(CaveCsvModel.IsArchived),
            nameof(CaveCsvModel.OtherTags));

    [Fact]
    public void EntranceCsvFieldMatrixMustBeUpdatedWhenContractChanges()
    {
        AssertDeclaredProperties<EntranceCsvModel>(
            nameof(EntranceCsvModel.EntranceId),
            nameof(EntranceCsvModel.CountyCode),
            nameof(EntranceCsvModel.CountyCaveNumber),
            nameof(EntranceCsvModel.EntranceName),
            nameof(EntranceCsvModel.DecimalLatitude),
            nameof(EntranceCsvModel.DecimalLongitude),
            nameof(EntranceCsvModel.EntranceElevationFt),
            nameof(EntranceCsvModel.LocationQuality),
            nameof(EntranceCsvModel.EntranceDescription),
            nameof(EntranceCsvModel.EntrancePitDepth),
            nameof(EntranceCsvModel.EntranceStatuses),
            nameof(EntranceCsvModel.EntranceHydrology),
            nameof(EntranceCsvModel.FieldIndication),
            nameof(EntranceCsvModel.ReportedOnDate),
            nameof(EntranceCsvModel.ReportedByNames),
            nameof(EntranceCsvModel.EntranceOtherTags),
            nameof(EntranceCsvModel.IsPrimaryEntrance));

        var entranceId = typeof(EntranceCsvModel).GetProperty(nameof(EntranceCsvModel.EntranceId));
        Assert.NotNull(entranceId);
        Assert.Single(entranceId!.GetCustomAttributes(typeof(IgnoreAttribute), inherit: true));
    }

    private static void AssertDeclaredProperties<T>(params string[] expected)
    {
        var actual = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), actual);
    }
}
