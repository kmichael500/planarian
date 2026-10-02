using Planarian.Library.Exceptions;
using Planarian.Modules.Map.Models;

namespace Planarian.Modules.Map.Services.Hydrology;

internal static class HydrologyRequestValidator
{
    private const int MaximumOriginIdLength = 128;
    private const int MaximumOriginNameLength = 256;
    private static readonly TimeSpan MaximumObservationRange = TimeSpan.FromDays(90);

    public static void ValidateNearbyGages(StreamGageRequest request)
    {
        if (request.Origins is not { Count: > 0 and <= 100 } ||
            request.Origins.Any(origin =>
                string.IsNullOrWhiteSpace(origin.Id) ||
                origin.Id.Length > MaximumOriginIdLength ||
                string.IsNullOrWhiteSpace(origin.Name) ||
                origin.Name.Length > MaximumOriginNameLength ||
                !double.IsFinite(origin.Latitude) ||
                !double.IsFinite(origin.Longitude) ||
                origin.Latitude is < -90 or > 90 ||
                origin.Longitude is < -180 or > 180) ||
            !double.IsFinite(request.DistanceMiles) ||
            request.DistanceMiles is <= 0 or > 50)
        {
            throw ApiExceptionDictionary.BadRequest(
                "Stream gage search parameters are outside the supported range.");
        }
    }

    public static void ValidateObservations(
        string siteCode,
        DateTimeOffset startDate,
        DateTimeOffset endDate)
    {
        if (!IsValidSiteCode(siteCode) ||
            endDate < startDate ||
            endDate - startDate > MaximumObservationRange)
        {
            throw ApiExceptionDictionary.BadRequest(
                "USGS stream gage observation parameters are invalid or exceed the 90-day range limit.");
        }
    }

    public static void ValidateSiteCode(string siteCode)
    {
        if (!IsValidSiteCode(siteCode))
            throw ApiExceptionDictionary.BadRequest("USGS stream gage site code is invalid.");
    }

    public static void ValidateStreamGageBounds(double north, double south, double east, double west)
    {
        if (!IsValidBounds(north, south, east, west))
            throw ApiExceptionDictionary.BadRequest("Stream gage bounds are outside the supported range.");
    }

    public static void ValidateHydrologyBounds(double north, double south, double east, double west)
    {
        if (!IsValidBounds(north, south, east, west))
            throw ApiExceptionDictionary.BadRequest("Hydrology bounds are outside the supported range.");
    }

    private static bool IsValidSiteCode(string siteCode) =>
        siteCode.Length is >= 8 and <= 15 && siteCode.All(char.IsDigit);

    private static bool IsValidBounds(double north, double south, double east, double west) =>
        north is >= -90 and <= 90 && south is >= -90 and <= 90 &&
        east is >= -180 and <= 180 && west is >= -180 and <= 180 &&
        north > south && east > west && north - south <= 10 && east - west <= 10;
}
