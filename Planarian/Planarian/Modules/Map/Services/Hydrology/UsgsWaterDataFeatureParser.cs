using System.Globalization;
using System.Text.Json;
using Planarian.Modules.Map.Models;

namespace Planarian.Modules.Map.Services.Hydrology;

internal static class UsgsWaterDataFeatureParser
{
    private const double EarthRadiusMiles = 3958.7613;

    public static StreamGageLocation? ParseStreamGageLocation(
        JsonElement feature,
        DateTimeOffset activeSince)
    {
        if (!feature.TryGetProperty("geometry", out var geometry) ||
            geometry.ValueKind == JsonValueKind.Null ||
            !geometry.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.GetArrayLength() < 2 ||
            !feature.TryGetProperty("properties", out var properties))
            return null;

        var computation = GetString(properties, "computation_identifier");
        if (!string.Equals(computation, "Instantaneous", StringComparison.OrdinalIgnoreCase))
            return null;
        if (DateTimeOffset.TryParse(
                GetString(properties, "end"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesEnd) && seriesEnd < activeSince)
            return null;

        var locationId = GetString(properties, "monitoring_location_id");
        if (locationId is null) return null;

        return new StreamGageLocation(
            locationId,
            GetString(properties, "monitoring_location_number") ?? locationId.Replace("USGS-", ""),
            GetString(properties, "monitoring_location_name") ?? locationId,
            coordinates[1].GetDouble(),
            coordinates[0].GetDouble());
    }

    public static UsgsSeriesMetadata? ParseSeriesMetadata(
        JsonElement feature,
        IReadOnlyList<StreamGageSearchOrigin> origins,
        double distanceMiles,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (!feature.TryGetProperty("geometry", out var geometry) ||
            geometry.ValueKind == JsonValueKind.Null ||
            !geometry.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.GetArrayLength() < 2 ||
            !feature.TryGetProperty("properties", out var properties))
            return null;

        if (DateTimeOffset.TryParse(
                GetString(properties, "begin"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesBegin) && seriesBegin > endDate)
            return null;
        if (DateTimeOffset.TryParse(
                GetString(properties, "end"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesEnd) && seriesEnd < startDate)
            return null;

        var longitude = coordinates[0].GetDouble();
        var latitude = coordinates[1].GetDouble();
        var nearest = origins
            .Select(origin => new OriginDistance(
                origin,
                GetDistanceMiles(
                    origin.Latitude,
                    origin.Longitude,
                    latitude,
                    longitude)))
            .MinBy(candidate => candidate.DistanceMiles);
        if (nearest is null || nearest.DistanceMiles > distanceMiles) return null;

        var seriesId = GetString(feature, "id");
        var locationId = GetString(properties, "monitoring_location_id");
        var parameterCode = GetString(properties, "parameter_code");
        if (seriesId is null || locationId is null || parameterCode is null) return null;

        var computation = GetString(properties, "computation_identifier");
        if (!string.Equals(computation, "Instantaneous", StringComparison.OrdinalIgnoreCase))
            return null;

        return new UsgsSeriesMetadata(
            seriesId,
            locationId,
            GetString(properties, "monitoring_location_number") ?? locationId.Replace("USGS-", ""),
            GetString(properties, "monitoring_location_name") ?? locationId,
            latitude,
            longitude,
            nearest.DistanceMiles,
            nearest.Origin,
            parameterCode,
            GetString(properties, "unit_of_measure") ?? "",
            GetString(properties, "primary"),
            GetNullableDouble(properties, "drainage_area"),
            GetNullableDouble(properties, "contributing_drainage_area"));
    }

    public static UsgsSiteSeriesMetadata? ParseSiteSeriesMetadata(
        JsonElement feature,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (!feature.TryGetProperty("properties", out var properties)) return null;

        if (DateTimeOffset.TryParse(
                GetString(properties, "begin"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesBegin) && seriesBegin > endDate)
            return null;
        if (DateTimeOffset.TryParse(
                GetString(properties, "end"),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var seriesEnd) && seriesEnd < startDate)
            return null;

        var seriesId = GetString(feature, "id");
        var parameterCode = GetString(properties, "parameter_code");
        if (seriesId is null || parameterCode is null) return null;

        var computation = GetString(properties, "computation_identifier");
        if (!string.Equals(computation, "Instantaneous", StringComparison.OrdinalIgnoreCase))
            return null;

        return new UsgsSiteSeriesMetadata(
            seriesId,
            parameterCode,
            GetString(properties, "unit_of_measure") ?? "",
            GetString(properties, "primary"));
    }

    public static UsgsObservationPoint? ParseObservationPoint(JsonElement feature)
    {
        if (!feature.TryGetProperty("properties", out var properties)) return null;

        var seriesId = GetString(properties, "time_series_id");
        var value = GetString(properties, "value");
        var time = GetString(properties, "time");
        if (seriesId is null || value is null || time is null ||
            !DateTimeOffset.TryParse(
                time,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var dateTime))
            return null;

        return new UsgsObservationPoint(
            seriesId,
            new StreamGagePoint(
                value,
                dateTime,
                GetString(properties, "approval_status")));
    }

    public static UsgsPeakObservation? ParsePeakObservation(JsonElement feature)
    {
        if (!feature.TryGetProperty("properties", out var properties)) return null;

        var parameterCode = GetString(properties, "parameter_code");
        var value = GetString(properties, "value");
        if (parameterCode is not ("00060" or "00065") || value is null ||
            !double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var numericValue))
            return null;
        var waterYearText = GetString(properties, "water_year");
        if (!int.TryParse(
                waterYearText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var waterYear))
            return null;

        DateOnly? date = null;
        var dateText = GetString(properties, "time");
        if (dateText is not null && DateOnly.TryParse(
                dateText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsedDate))
            date = parsedDate;

        int? peakSince = null;
        var peakSinceText = GetString(properties, "peak_since");
        if (int.TryParse(
                peakSinceText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsedPeakSince))
            peakSince = parsedPeakSince;

        return new UsgsPeakObservation(
            parameterCode,
            value,
            numericValue,
            GetString(properties, "unit_of_measure") ?? "",
            date,
            waterYear,
            GetStringValues(properties, "qualifier"),
            peakSince);
    }

    public static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
            return null;

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
    }

    private static IReadOnlyList<string> GetStringValues(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
            return [];
        if (property.ValueKind == JsonValueKind.Array)
            return property.EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.ToString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();

        var value = property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : property.ToString();
        return string.IsNullOrWhiteSpace(value) ? [] : [value];
    }

    private static double? GetNullableDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
            return null;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number))
            return number;

        return double.TryParse(
            property.ToString(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out number)
            ? number
            : null;
    }

    private static double GetDistanceMiles(
        double lat1,
        double lon1,
        double lat2,
        double lon2)
    {
        static double ToRadians(double degrees) => degrees * Math.PI / 180d;

        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dLat / 2d), 2d) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Pow(Math.Sin(dLon / 2d), 2d);
        return EarthRadiusMiles * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }

    private sealed record OriginDistance(
        StreamGageSearchOrigin Origin,
        double DistanceMiles);
}

internal sealed record UsgsObservationPoint(
    string SeriesId,
    StreamGagePoint Point);
internal sealed record UsgsSiteSeriesMetadata(
    string SeriesId,
    string ParameterCode,
    string Unit,
    string? Primary);

internal sealed record UsgsSeriesMetadata(
    string SeriesId,
    string MonitoringLocationId,
    string SiteCode,
    string SiteName,
    double Latitude,
    double Longitude,
    double DistanceMiles,
    StreamGageSearchOrigin NearestOrigin,
    string ParameterCode,
    string Unit,
    string? Primary,
    double? DrainageAreaSquareMiles,
    double? ContributingDrainageAreaSquareMiles);

internal sealed record UsgsPeakObservation(
    string ParameterCode,
    string Value,
    double NumericValue,
    string Unit,
    DateOnly? Date,
    int WaterYear,
    IReadOnlyList<string> Qualifiers,
    int? PeakSince);
