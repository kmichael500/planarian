using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using Planarian.Library.Exceptions;
using Planarian.Modules.Import.Models;

namespace Planarian.Modules.Import.Parsing;

public sealed class CaveImportCsvParser
{
    public async Task<IReadOnlyList<CaveCsvModel>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var records = new List<CaveCsvModel>();
        var failures = new List<FailedCaveCsvRecord<CaveCsvModel>>();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null });
        csv.Context.RegisterClassMap<CaveCsvModelMap>();
        if (!await csv.ReadAsync()) return records;
        csv.ReadHeader();
        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var record = new CaveCsvModel();
            var errors = new List<string>();
            CsvFieldReader.TryGet(csv, nameof(record.CaveName), true, errors, out string? caveName); if (!string.IsNullOrWhiteSpace(caveName)) record.CaveName = caveName;
            CsvFieldReader.TryGet(csv, nameof(record.AlternateNames), false, errors, out string? alternate); record.AlternateNames = alternate;
            CsvFieldReader.TryGet(csv, nameof(record.State), true, errors, out string? state); if (!string.IsNullOrWhiteSpace(state)) record.State = state;
            CsvFieldReader.TryGet(csv, nameof(record.CountyCode), true, errors, out string? countyCode); if (!string.IsNullOrWhiteSpace(countyCode)) record.CountyCode = countyCode;
            CsvFieldReader.TryGet(csv, nameof(record.CountyName), true, errors, out string? countyName); if (!string.IsNullOrWhiteSpace(countyName)) record.CountyName = countyName;
            CsvFieldReader.TryGet(csv, nameof(record.CountyCaveNumber), true, errors, out int countyNumber); record.CountyCaveNumber = countyNumber;
            CsvFieldReader.TryGet(csv, nameof(record.MapStatuses), false, errors, out string? map); record.MapStatuses = map;
            CsvFieldReader.TryGet(csv, nameof(record.CartographerNames), false, errors, out string? cartographers); record.CartographerNames = cartographers;
            CsvFieldReader.TryGet(csv, nameof(record.CaveLengthFt), false, errors, out double? length); record.CaveLengthFt = length;
            CsvFieldReader.TryGet(csv, nameof(record.CaveDepthFt), false, errors, out double? depth); record.CaveDepthFt = depth;
            CsvFieldReader.TryGet(csv, nameof(record.MaxPitDepthFt), false, errors, out double? pit); record.MaxPitDepthFt = pit;
            CsvFieldReader.TryGet(csv, nameof(record.NumberOfPits), false, errors, out int? pits); record.NumberOfPits = pits;
            CsvFieldReader.TryGet(csv, nameof(record.Narrative), false, errors, out string? narrative); record.Narrative = narrative;
            CsvFieldReader.TryGet(csv, nameof(record.Geology), false, errors, out string? geology); record.Geology = geology;
            CsvFieldReader.TryGet(csv, nameof(record.GeologicAges), false, errors, out string? ages); record.GeologicAges = ages;
            CsvFieldReader.TryGet(csv, nameof(record.PhysiographicProvinces), false, errors, out string? provinces); record.PhysiographicProvinces = provinces;
            CsvFieldReader.TryGet(csv, nameof(record.Archeology), false, errors, out string? archaeology); record.Archeology = archaeology;
            CsvFieldReader.TryGet(csv, nameof(record.Biology), false, errors, out string? biology); record.Biology = biology;
            CsvFieldReader.TryGet(csv, nameof(record.IsArchived), false, errors, out bool archived); record.IsArchived = archived;
            CsvFieldReader.TryGet(csv, nameof(record.ReportedOnDate), false, errors, out string? reportedOn); record.ReportedOnDate = reportedOn;
            CsvFieldReader.TryGet(csv, nameof(record.ReportedByNames), false, errors, out string? reportedBy); record.ReportedByNames = reportedBy;
            CsvFieldReader.TryGet(csv, nameof(record.OtherTags), false, errors, out string? other); record.OtherTags = other;
            if (errors.Count == 0) records.Add(record);
            else foreach (var error in errors) failures.Add(new(record, rowNumber, error));
        }
        if (failures.Count > 0) throw ApiExceptionDictionary.InvalidImport(failures.OrderBy(f => f.RowNumber).ToList(), ImportType.Cave);
        return records;
    }
}

public sealed class EntranceImportCsvParser
{
    public async Task<IReadOnlyList<EntranceCsvModel>> ParseAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        var records = new List<EntranceCsvModel>();
        var failures = new List<FailedCaveCsvRecord<EntranceCsvModel>>();
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { MissingFieldFound = null });
        csv.Context.RegisterClassMap<EntranceCsvModelMap>();
        if (!await csv.ReadAsync()) return records;
        csv.ReadHeader();
        var rowNumber = 1;
        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;
            var record = new EntranceCsvModel();
            var errors = new List<string>();
            CsvFieldReader.TryGet(csv, nameof(record.CountyCode), true, errors, out string? countyCode); record.CountyCode = countyCode;
            CsvFieldReader.TryGet(csv, nameof(record.CountyCaveNumber), true, errors, out string? caveNumber); record.CountyCaveNumber = caveNumber;
            CsvFieldReader.TryGet(csv, nameof(record.EntranceName), false, errors, out string? name); record.EntranceName = name;
            CsvFieldReader.TryGet(csv, nameof(record.DecimalLatitude), true, errors, out double latitude); record.DecimalLatitude = latitude;
            CsvFieldReader.TryGet(csv, nameof(record.DecimalLongitude), true, errors, out double longitude); record.DecimalLongitude = longitude;
            CsvFieldReader.TryGet(csv, nameof(record.EntranceElevationFt), true, errors, out double elevation); record.EntranceElevationFt = elevation;
            CsvFieldReader.TryGet(csv, nameof(record.LocationQuality), true, errors, out string? quality); record.LocationQuality = quality ?? string.Empty;
            CsvFieldReader.TryGet(csv, nameof(record.EntranceDescription), false, errors, out string? description); record.EntranceDescription = description;
            CsvFieldReader.TryGet(csv, nameof(record.EntrancePitDepth), false, errors, out double? pit); record.EntrancePitDepth = pit;
            CsvFieldReader.TryGet(csv, nameof(record.EntranceStatuses), false, errors, out string? statuses); record.EntranceStatuses = statuses;
            CsvFieldReader.TryGet(csv, nameof(record.EntranceHydrology), false, errors, out string? hydrology); record.EntranceHydrology = hydrology;
            CsvFieldReader.TryGet(csv, nameof(record.FieldIndication), false, errors, out string? indication); record.FieldIndication = indication;
            CsvFieldReader.TryGet(csv, nameof(record.ReportedOnDate), false, errors, out string? reportedOn); record.ReportedOnDate = reportedOn;
            CsvFieldReader.TryGet(csv, nameof(record.ReportedByNames), false, errors, out string? reportedBy); record.ReportedByNames = reportedBy;
            CsvFieldReader.TryGet(csv, nameof(record.IsPrimaryEntrance), false, errors, out bool primary); record.IsPrimaryEntrance = primary;
            if (errors.Count == 0) records.Add(record);
            else foreach (var error in errors) failures.Add(new(record, rowNumber, error));
        }
        if (failures.Count > 0) throw ApiExceptionDictionary.InvalidImport(failures.OrderBy(f => f.RowNumber).ToList(), ImportType.Entrance);
        return records;
    }
}

internal static class CsvFieldReader
{
    public static bool TryGet<T>(IReaderRow csv, string fieldName, bool required, ICollection<string> errors, out T? value)
    {
        var hasValue = csv.TryGetField(fieldName, out value);
        if (!hasValue || (typeof(T) == typeof(string) && string.IsNullOrWhiteSpace(value?.ToString())))
        {
            if (required) errors.Add($"{fieldName} is required.");
            return false;
        }
        if (typeof(T) == typeof(string) && value != null) value = (T)(object)value.ToString()!.Trim();
        return true;
    }
}
