using Planarian.Modules.Map.Models;

namespace Planarian.Modules.Map.Services.Hydrology;

internal static class UsgsWaterDataProjector
{
    public static NearbyStreamGage BuildGage(
        IReadOnlyList<UsgsSeriesMetadata> series,
        IReadOnlyDictionary<string, List<StreamGagePoint>> pointsBySeries,
        bool summaryOnly)
    {
        var first = series[0];
        var parameters = series
            .OrderBy(item => item.ParameterCode)
            .Select(item =>
            {
                IReadOnlyList<StreamGagePoint> points = pointsBySeries.TryGetValue(
                    item.SeriesId,
                    out var seriesPoints)
                    ? seriesPoints
                    : [];
                return new StreamGageParameter(
                    item.ParameterCode,
                    GetParameterName(item.ParameterCode),
                    item.Unit,
                    summaryOnly ? GetSummaryPoints(points) : points);
            })
            .ToList();

        return new NearbyStreamGage(
            first.MonitoringLocationId,
            first.SiteCode,
            first.SiteName,
            first.Latitude,
            first.Longitude,
            first.DistanceMiles,
            first.NearestOrigin.Id,
            first.NearestOrigin.Name,
            first.DrainageAreaSquareMiles,
            first.ContributingDrainageAreaSquareMiles,
            parameters);
    }

    public static IReadOnlyList<StreamGageParameter> BuildParameters(
        IReadOnlyList<UsgsSiteSeriesMetadata> series,
        IReadOnlyDictionary<string, List<StreamGagePoint>> pointsBySeries) =>
        series
            .OrderBy(item => item.ParameterCode)
            .Select(item => new StreamGageParameter(
                item.ParameterCode,
                GetParameterName(item.ParameterCode),
                item.Unit,
                pointsBySeries.TryGetValue(item.SeriesId, out var points) ? points : []))
            .ToList();

    public static StreamGagePeakSummary BuildPeakSummary(
        string siteCode,
        IReadOnlyList<UsgsPeakObservation> peaks) =>
        new(
            siteCode,
            BuildHistoricalPeak(peaks, "00060", "Streamflow"),
            BuildHistoricalPeak(peaks, "00065", "Gage height"),
            BuildAnnualPeakHistory(peaks, "00060", "Streamflow"),
            BuildAnnualPeakHistory(peaks, "00065", "Gage height"));

    private static string GetParameterName(string parameterCode) =>
        parameterCode == "00060" ? "Streamflow" : "Gage height";

    private static IReadOnlyList<StreamGagePoint> GetSummaryPoints(
        IReadOnlyList<StreamGagePoint> points)
    {
        if (points.Count <= 1) return points;

        var latest = points[^1];
        var comparisonTime = latest.DateTime.AddHours(-6);
        var comparison = points
            .Take(points.Count - 1)
            .MinBy(point => Math.Abs((point.DateTime - comparisonTime).TotalMinutes));

        if (comparison is null ||
            Math.Abs((comparison.DateTime - comparisonTime).TotalMinutes) > 90)
            return [latest];

        return [comparison, latest];
    }

    private static IReadOnlyList<StreamGageAnnualPeak> BuildAnnualPeakHistory(
        IReadOnlyList<UsgsPeakObservation> peaks,
        string parameterCode,
        string variableName) =>
        peaks
            .Where(peak => peak.ParameterCode == parameterCode && peak.PeakSince is null)
            .GroupBy(peak => peak.WaterYear)
            .Select(group => group.MaxBy(peak => peak.NumericValue)!)
            .OrderBy(peak => peak.WaterYear)
            .Select(peak => new StreamGageAnnualPeak(
                peak.ParameterCode,
                variableName,
                peak.Unit,
                peak.Value,
                peak.Date,
                peak.WaterYear,
                peak.Qualifiers))
            .ToList();

    private static StreamGageHistoricalPeak? BuildHistoricalPeak(
        IReadOnlyList<UsgsPeakObservation> peaks,
        string parameterCode,
        string variableName)
    {
        var matching = peaks
            .Where(peak => peak.ParameterCode == parameterCode && peak.PeakSince is null)
            .ToList();
        if (matching.Count == 0) return null;

        var highest = matching.MaxBy(peak => peak.NumericValue)!;
        return new StreamGageHistoricalPeak(
            parameterCode,
            variableName,
            highest.Unit,
            highest.Value,
            highest.Date,
            highest.WaterYear,
            highest.Qualifiers,
            matching.Min(peak => peak.WaterYear),
            matching.Max(peak => peak.WaterYear),
            matching.Select(peak => peak.WaterYear).Distinct().Count());
    }
}
