namespace Planarian.Modules.Map.Models;

public sealed record StreamGagePoint(
    string Value,
    DateTimeOffset DateTime,
    string? ApprovalStatus);

public sealed record StreamGageParameter(
    string ParameterCode,
    string VariableName,
    string Unit,
    IReadOnlyList<StreamGagePoint> Points);

public sealed record NearbyStreamGage(
    string Id,
    string SiteCode,
    string SiteName,
    double Latitude,
    double Longitude,
    double DistanceMiles,
    string? NearestOriginId,
    string? NearestOriginName,
    double? DrainageAreaSquareMiles,
    double? ContributingDrainageAreaSquareMiles,
    IReadOnlyList<StreamGageParameter> Parameters);

public sealed record StreamGageAnnualPeak(
    string ParameterCode,
    string VariableName,
    string Unit,
    string Value,
    DateOnly? Date,
    int WaterYear,
    IReadOnlyList<string> Qualifiers);

public sealed record StreamGageHistoricalPeak(
    string ParameterCode,
    string VariableName,
    string Unit,
    string Value,
    DateOnly? Date,
    int WaterYear,
    IReadOnlyList<string> Qualifiers,
    int FirstWaterYear,
    int LastWaterYear,
    int AnnualPeakCount);

public sealed record StreamGagePeakSummary(
    string SiteCode,
    StreamGageHistoricalPeak? Streamflow,
    StreamGageHistoricalPeak? GageHeight,
    IReadOnlyList<StreamGageAnnualPeak> StreamflowHistory,
    IReadOnlyList<StreamGageAnnualPeak> GageHeightHistory);
