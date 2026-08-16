using System.Text.Json;
using Planarian.Model.Database.Revisions;
using Planarian.Model.Shared;
using Planarian.Library.Exceptions;
using Planarian.Modules.Caves.Models;

namespace Planarian.Modules.Caves.Revisions;

public static class CaveAlternateNameNormalizer
{
    public static IReadOnlyList<string> Normalize(IEnumerable<string>? names) => (names ?? [])
        .Select(name => name?.Trim())
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Select(name => name!)
        .Distinct(StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToList();
}

/// <summary>Database-independent validation and normalization for every Cave mutation authoring path.</summary>
public static class CaveMutationValidation
{
    public static void NormalizeAndValidate(AddCaveVm values)
    {
        if (string.IsNullOrWhiteSpace(values.Name))
            throw ApiExceptionDictionary.BadRequest("Cave name is required.");
        if (string.IsNullOrWhiteSpace(values.StateId))
            throw ApiExceptionDictionary.BadRequest("State is required.");
        if (string.IsNullOrWhiteSpace(values.CountyId))
            throw ApiExceptionDictionary.BadRequest("County is required.");
        if (values.Entrances is null || !values.Entrances.Any())
            throw ApiExceptionDictionary.EntranceRequired("At least 1 entrance is required!");
        if (values.NumberOfPits < 0)
            throw ApiExceptionDictionary.BadRequest("Number of pits must be greater than or equal to 0!");
        if (values.LengthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Length must be greater than or equal to 0!");
        if (values.DepthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Depth must be greater than or equal to 0!");
        if (values.MaxPitDepthFeet < 0)
            throw ApiExceptionDictionary.BadRequest("Max pit depth must be greater than or equal to 0!");
        if (values.IsCountyNumberManuallySet && (!values.CountyNumber.HasValue || values.CountyNumber <= 0))
            throw ApiExceptionDictionary.BadRequest("County number must be greater than 0 when manually set.");

        var entrances = values.Entrances.ToList();
        if (entrances.Any(entrance => entrance.Latitude is > 90 or < -90))
            throw ApiExceptionDictionary.BadRequest("Latitude must be between -90 and 90!");
        if (entrances.Any(entrance => entrance.Longitude is > 180 or < -180))
            throw ApiExceptionDictionary.BadRequest("Longitude must be between -180 and 180!");
        if (entrances.Any(entrance => entrance.ElevationFeet < 0))
            throw ApiExceptionDictionary.BadRequest("Elevation must be greater than or equal to 0!");
        if (entrances.Any(entrance => entrance.PitFeet < 0))
            throw ApiExceptionDictionary.BadRequest("Pit depth must be greater than or equal to 0!");
        if (entrances.Any(entrance => string.IsNullOrWhiteSpace(entrance.LocationQualityTagId)))
            throw ApiExceptionDictionary.BadRequest("Location quality is required for every entrance.");
        if (entrances.Count(entrance => entrance.IsPrimary) != 1)
            throw ApiExceptionDictionary.BadRequest("Exactly one entrance must be marked as primary!");

        foreach (var entrance in entrances)
            entrance.Id = NullIfWhiteSpace(entrance.Id);

        var duplicateEntranceId = entrances.Where(entrance => entrance.Id is not null)
            .GroupBy(entrance => entrance.Id!, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateEntranceId is not null)
            throw ApiExceptionDictionary.BadRequest("Entrance IDs must be unique within a Cave mutation.");

        var files = (values.Files ?? []).ToList();
        foreach (var file in files)
            file.Id = file.Id?.Trim()!;
        if (files.Any(file => string.IsNullOrWhiteSpace(file.Id)))
            throw ApiExceptionDictionary.BadRequest("File IDs are required.");
        if (files.GroupBy(file => file.Id, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw ApiExceptionDictionary.BadRequest("File IDs must be unique within a Cave mutation.");

        values.Name = values.Name.Trim();
        values.StateId = values.StateId.Trim();
        values.CountyId = values.CountyId.Trim();
        values.AlternateNames = CaveAlternateNameNormalizer.Normalize(values.AlternateNames);
        values.GeologyTagIds = NormalizeExistingSet(values.GeologyTagIds);
        values.GeologicAgeTagIds = NormalizeExistingSet(values.GeologicAgeTagIds);
        values.MapStatusTagIds = NormalizeExistingSet(values.MapStatusTagIds);
        values.PhysiographicProvinceTagIds = NormalizeExistingSet(values.PhysiographicProvinceTagIds);
        values.ArcheologyTagIds = NormalizeExistingSet(values.ArcheologyTagIds);
        values.BiologyTagIds = NormalizeExistingSet(values.BiologyTagIds);
        values.OtherTagIds = NormalizeExistingSet(values.OtherTagIds);
        values.CartographerNameTagIds = NormalizeSet(values.CartographerNameTagIds);
        values.ReportedByNameTagIds = NormalizeSet(values.ReportedByNameTagIds);
        foreach (var entrance in entrances)
        {
            entrance.LocationQualityTagId = entrance.LocationQualityTagId.Trim();
            entrance.EntranceStatusTagIds = NormalizeExistingSet(entrance.EntranceStatusTagIds);
            entrance.EntranceHydrologyTagIds = NormalizeExistingSet(entrance.EntranceHydrologyTagIds);
            entrance.FieldIndicationTagIds = NormalizeExistingSet(entrance.FieldIndicationTagIds);
            entrance.ReportedByNameTagIds = NormalizeSet(entrance.ReportedByNameTagIds);
            entrance.EntranceOtherTagIds = NormalizeExistingSet(entrance.EntranceOtherTagIds);
        }
        var linePlots = (values.LinePlots ?? []).ToList();
        foreach (var linePlot in linePlots)
        {
            linePlot.Id = NullIfWhiteSpace(linePlot.Id);
            if (linePlot.Id?.Length > PropertyLength.Id)
                throw ApiExceptionDictionary.BadRequest(
                    $"Line plot IDs cannot exceed {PropertyLength.Id} characters.");
            linePlot.Name = linePlot.Name?.Trim()!;
            if (string.IsNullOrWhiteSpace(linePlot.Name))
                throw ApiExceptionDictionary.BadRequest("Line plot name is required.");
            if (linePlot.Name.Length > PropertyLength.Name)
                throw ApiExceptionDictionary.BadRequest(
                    $"Line plot names cannot exceed {PropertyLength.Name} characters.");
            if (string.IsNullOrWhiteSpace(linePlot.GeoJson))
                throw ApiExceptionDictionary.BadRequest("Line plot GeoJSON is required.");
            try
            {
                linePlot.GeoJson = CaveJsonContent.NormalizeFeatureCollection(linePlot.GeoJson);
            }
            catch (JsonException)
            {
                throw ApiExceptionDictionary.BadRequest(
                    "Line plot GeoJSON must be a valid FeatureCollection.");
            }
        }
        if (linePlots.Where(linePlot => linePlot.Id is not null)
            .GroupBy(linePlot => linePlot.Id!, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw ApiExceptionDictionary.BadRequest("Line plot IDs must be unique within a Cave mutation.");

        values.Entrances = entrances;
        values.Files = files;
        values.LinePlots = linePlots;
    }

    private static IReadOnlyList<string> NormalizeSet(IEnumerable<string>? values) => (values ?? [])
        .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim())
        .Distinct(StringComparer.Ordinal).ToList();

    private static IReadOnlyList<string> NormalizeExistingSet(IEnumerable<string>? values)
    {
        var submitted = (values ?? []).ToList();
        if (submitted.Any(string.IsNullOrWhiteSpace))
            throw ApiExceptionDictionary.BadRequest("Tag IDs cannot be blank.");
        return NormalizeSet(submitted);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
