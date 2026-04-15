using Microsoft.AspNetCore.Http;
using System.Net;
using Planarian.Model.Shared;

namespace Planarian.Shared.Services;

public static class RequestThrottleKeyHelper
{
    public static string CreateKey(RequestThrottleKeyType key, IEnumerable<string?> keyParts)
    {
        return $"{key}:{CreateKeyValue(keyParts)}";
    }

    public static string GetRequestPathKey(HttpContext? httpContext)
    {
        if (httpContext?.GetEndpoint() is RouteEndpoint routeEndpoint &&
            !string.IsNullOrWhiteSpace(routeEndpoint.RoutePattern.RawText))
        {
            return routeEndpoint.RoutePattern.RawText;
        }

        return httpContext?.Request.Path.ToString() ?? string.Empty;
    }

    public static string GetClientIpAddress(HttpContext? httpContext)
    {
        // This is safe for the current direct-hosted deployment. If the app is later placed
        // behind Cloudflare or another reverse proxy, forwarded-header middleware must be
        // configured so RemoteIpAddress resolves to the actual client IP instead of the proxy.
        var ipAddress = NormalizeIpAddress(httpContext?.Connection.RemoteIpAddress);
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            throw new InvalidOperationException("Client IP address is not available for the current request.");
        }

        return ipAddress;
    }

    public static string CreateKeyValue(IEnumerable<string?> keyParts)
    {
        return string.Join("::", keyParts.Select(part => part?.Trim().ToLowerInvariant() ?? string.Empty));
    }

    private static string? NormalizeIpAddress(IPAddress? ipAddress)
    {
        if (ipAddress == null)
        {
            return null;
        }

        return (ipAddress.IsIPv4MappedToIPv6 ? ipAddress.MapToIPv4() : ipAddress).ToString();
    }
}
