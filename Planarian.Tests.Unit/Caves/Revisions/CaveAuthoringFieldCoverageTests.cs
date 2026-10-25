using System.Reflection;
using Planarian.Modules.Caves.Models;
using Planarian.Modules.Files.Controllers;
using Xunit;

namespace Planarian.Tests.Unit.Caves.Revisions;

/// <summary>
/// Drift guard for the authoring contract. Persistence semantics are exercised by the real-PostgreSQL
/// field-lifecycle integration tests; this class makes a newly added authoring field an explicit test decision.
/// </summary>
public sealed class CaveAuthoringFieldCoverageTests
{
    [Fact]
    public void CaveAuthoringFieldMatrixMustBeUpdatedWhenContractChanges() =>
        AssertDeclaredProperties<AddCaveVm>(
            nameof(AddCaveVm.Id),
            nameof(AddCaveVm.ExpectedRevisionId),
            nameof(AddCaveVm.Name),
            nameof(AddCaveVm.AlternateNames),
            nameof(AddCaveVm.CountyId),
            nameof(AddCaveVm.StateId),
            nameof(AddCaveVm.IsCountyNumberManuallySet),
            nameof(AddCaveVm.UseFirstAvailableCountyNumber),
            nameof(AddCaveVm.CountyNumber),
            nameof(AddCaveVm.LengthFeet),
            nameof(AddCaveVm.DepthFeet),
            nameof(AddCaveVm.MaxPitDepthFeet),
            nameof(AddCaveVm.NumberOfPits),
            nameof(AddCaveVm.Narrative),
            nameof(AddCaveVm.ReportedOn),
            nameof(AddCaveVm.Entrances),
            nameof(AddCaveVm.Files),
            nameof(AddCaveVm.LinePlots),
            nameof(AddCaveVm.GeologyTagIds),
            nameof(AddCaveVm.ReportedByNameTagIds),
            nameof(AddCaveVm.BiologyTagIds),
            nameof(AddCaveVm.ArcheologyTagIds),
            nameof(AddCaveVm.CartographerNameTagIds),
            nameof(AddCaveVm.MapStatusTagIds),
            nameof(AddCaveVm.GeologicAgeTagIds),
            nameof(AddCaveVm.PhysiographicProvinceTagIds),
            nameof(AddCaveVm.OtherTagIds));

    [Fact]
    public void EntranceAuthoringFieldMatrixMustBeUpdatedWhenContractChanges() =>
        AssertDeclaredProperties<AddEntranceVm>(
            nameof(AddEntranceVm.Id),
            nameof(AddEntranceVm.IsPrimary),
            nameof(AddEntranceVm.LocationQualityTagId),
            nameof(AddEntranceVm.Name),
            nameof(AddEntranceVm.Description),
            nameof(AddEntranceVm.Latitude),
            nameof(AddEntranceVm.Longitude),
            nameof(AddEntranceVm.ElevationFeet),
            nameof(AddEntranceVm.ReportedOn),
            nameof(AddEntranceVm.PitFeet),
            nameof(AddEntranceVm.EntranceStatusTagIds),
            nameof(AddEntranceVm.FieldIndicationTagIds),
            nameof(AddEntranceVm.EntranceHydrologyTagIds),
            nameof(AddEntranceVm.ReportedByNameTagIds),
            nameof(AddEntranceVm.EntranceOtherTagIds));

    [Fact]
    public void FileMetadataAuthoringFieldMatrixMustBeUpdatedWhenContractChanges() =>
        AssertDeclaredProperties<EditFileMetadataVm>(
            nameof(EditFileMetadataVm.Id),
            nameof(EditFileMetadataVm.Name),
            nameof(EditFileMetadataVm.FileTypeTagId));

    [Fact]
    public void LinePlotAuthoringFieldMatrixMustBeUpdatedWhenContractChanges() =>
        AssertDeclaredProperties<GeoJsonUploadVm>(
            nameof(GeoJsonUploadVm.Id),
            nameof(GeoJsonUploadVm.GeoJson),
            nameof(GeoJsonUploadVm.Name));

    private static void AssertDeclaredProperties<T>(params string[] expected)
    {
        var actual = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected.Order(StringComparer.Ordinal).ToArray(), actual);
    }
}
