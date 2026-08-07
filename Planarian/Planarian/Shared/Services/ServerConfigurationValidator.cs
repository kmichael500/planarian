using Planarian.Library.Options;

namespace Planarian.Shared.Services;

public sealed record ServerDeploymentConfiguration(IReadOnlyCollection<string> AllowedCorsOrigins);

public static class ServerConfigurationValidator
{
    public static ServerDeploymentConfiguration Validate(ServerOptions serverOptions, string? allowedHosts,
        bool isAzureAppService)
    {
        var clientOriginMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in serverOptions.ClientOriginMappings)
        {
            var apiHostname = NormalizeHostname(mapping.Key, "ClientOriginMappings API-host key");
            if (!clientOriginMappings.TryAdd(apiHostname, NormalizeOrigin(mapping.Value,
                    $"ClientOriginMappings value for '{apiHostname}'")))
            {
                throw new InvalidOperationException($"Duplicate ClientOriginMappings API-host key '{apiHostname}'.");
            }
        }
        serverOptions.ClientOriginMappings = clientOriginMappings;

        var corsOrigins = serverOptions.AllowedCorsOrigins
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(origin => NormalizeOrigin(origin, "Server:AllowedCorsOrigins entry"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (!isAzureAppService)
        {
            return new ServerDeploymentConfiguration(corsOrigins);
        }

        var configuredAllowedHosts = ParseAllowedHosts(allowedHosts);
        if (configuredAllowedHosts.Count == 0 || configuredAllowedHosts.Any(host => host == "*" || host.StartsWith("*.", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Azure App Service requires AllowedHosts with at least one explicit hostname and no wildcards.");
        }

        if (clientOriginMappings.Count == 0)
        {
            throw new InvalidOperationException("Azure App Service requires at least one Server:ClientOriginMappings entry.");
        }

        foreach (var mapping in clientOriginMappings)
        {
            if (!configuredAllowedHosts.Contains(mapping.Key))
            {
                throw new InvalidOperationException($"ClientOriginMappings API host '{mapping.Key}' is not allowed by AllowedHosts.");
            }

            if (!corsOrigins.Contains(mapping.Value, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Client origin '{mapping.Value}' for API host '{mapping.Key}' is not present in Server:AllowedCorsOrigins.");
            }
        }

        return new ServerDeploymentConfiguration(corsOrigins);
    }

    public static string NormalizeHostname(string value, string description)
    {
        var hostname = value.Trim().TrimEnd('.').ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(hostname) || hostname.Contains("://", StringComparison.Ordinal) ||
            hostname.IndexOfAny(['/', '?', '#']) >= 0 || Uri.CheckHostName(hostname) == UriHostNameType.Unknown)
        {
            throw new InvalidOperationException($"{description} '{value}' must be a non-empty hostname without a scheme or path.");
        }

        return hostname;
    }

    private static HashSet<string> ParseAllowedHosts(string? allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(allowedHosts)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawHost in allowedHosts.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawHost == "*" || rawHost.StartsWith("*.", StringComparison.Ordinal))
            {
                hosts.Add(rawHost);
                continue;
            }

            hosts.Add(NormalizeHostname(rawHost, "AllowedHosts entry"));
        }

        return hosts;
    }

    private static string NormalizeOrigin(string value, string description)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            uri.Host.Contains('*') || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"{description} must be an absolute HTTP/HTTPS origin without a path, query, fragment, or wildcard.");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}
