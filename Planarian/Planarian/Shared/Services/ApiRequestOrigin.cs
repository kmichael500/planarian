namespace Planarian.Shared.Services;

public interface IApiRequestOrigin
{
    string GetOrigin();
}

/// <summary>
/// Gets the public API origin for the current request. Host filtering must be configured with
/// AllowedHosts by the deployment so request hosts are accepted only from known API domains.
/// </summary>
public class ApiRequestOrigin : IApiRequestOrigin
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly Planarian.Library.Options.ServerOptions _serverOptions;

    public ApiRequestOrigin(IHttpContextAccessor httpContextAccessor, Planarian.Library.Options.ServerOptions serverOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _serverOptions = serverOptions;
    }

    public string GetOrigin()
    {
        var request = _httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException("An API request origin is available only during an HTTP request.");

        var hostname = ServerConfigurationValidator.NormalizeHostname(request.Host.Host, "API request host");
        if (!_serverOptions.ClientOriginMappings.ContainsKey(hostname))
        {
            throw new InvalidOperationException($"API request host '{hostname}' is not configured for public origin generation.");
        }
        return $"{request.Scheme}://{request.Host}";
    }
}
