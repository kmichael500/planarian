using System.Globalization;

namespace Planarian.Modules.Map.Services.Hydrology;

internal static class UsgsWaterDataRequestBuilder
{
    private const int PageSize = 50000;

    public static string BuildPeakRequestUri(string siteCode)
    {
        var query = new Dictionary<string, string>
        {
            ["monitoring_location_id"] = $"USGS-{siteCode}",
            ["parameter_code"] = "00060,00065",
            ["properties"] = "parameter_code,value,water_year,time,peak_since,unit_of_measure,qualifier",
            ["limit"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };

        return "peaks/items?" + ToQueryString(query);
    }

    public static string BuildMetadataRequestUri(UsgsSearchBounds bounds)
    {
        var query = new Dictionary<string, string>
        {
            ["bbox"] = FormattableString.Invariant(
                $"{bounds.West},{bounds.South},{bounds.East},{bounds.North}"),
            ["agency_code"] = "USGS",
            ["site_type_code"] = "ST",
            ["parameter_code"] = "00060,00065",
            ["data_type"] = "Continuous values",
            ["properties"] = "monitoring_location_id,monitoring_location_number,monitoring_location_name,parameter_code,computation_identifier,unit_of_measure,primary,drainage_area,contributing_drainage_area,begin,end",
            ["limit"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };

        return "combined-metadata/items?" + ToQueryString(query);
    }

    public static string BuildSiteMetadataRequestUri(string siteCode)
    {
        var query = new Dictionary<string, string>
        {
            ["monitoring_location_id"] = $"USGS-{siteCode}",
            ["agency_code"] = "USGS",
            ["site_type_code"] = "ST",
            ["parameter_code"] = "00060,00065",
            ["data_type"] = "Continuous values",
            ["properties"] = "monitoring_location_id,monitoring_location_number,monitoring_location_name,parameter_code,computation_identifier,unit_of_measure,primary,drainage_area,contributing_drainage_area,begin,end",
            ["limit"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };

        return "combined-metadata/items?" + ToQueryString(query);
    }

    public static string BuildObservationRequestUri(
        IReadOnlyList<string> seriesIds,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        var query = new Dictionary<string, string>
        {
            ["time_series_id"] = string.Join(",", seriesIds),
            ["datetime"] = $"{startDate.UtcDateTime:O}/{endDate.UtcDateTime:O}",
            ["properties"] = "time_series_id,value,time,approval_status",
            ["limit"] = PageSize.ToString(CultureInfo.InvariantCulture),
            ["f"] = "json"
        };

        return "continuous/items?" + ToQueryString(query);
    }

    private static string ToQueryString(IReadOnlyDictionary<string, string> query) =>
        string.Join("&", query.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
}

internal sealed record UsgsSearchBounds(double South, double North, double West, double East);