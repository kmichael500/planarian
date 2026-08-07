using Planarian.Library.Options;

namespace Planarian.Shared.Services;

public interface IClientRequestOrigin
{
    string GetOrigin();
}

public class ClientRequestOrigin : IClientRequestOrigin
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ServerOptions _serverOptions;

    public ClientRequestOrigin(IHttpContextAccessor httpContextAccessor, ServerOptions serverOptions)
    {
        _httpContextAccessor = httpContextAccessor;
        _serverOptions = serverOptions;
    }

    public string GetOrigin()
    {
        var request = _httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException("A client request origin is available only during an HTTP request.");
        var apiHostname = ServerConfigurationValidator.NormalizeHostname(request.Host.Host, "API request host");

        if (!_serverOptions.ClientOriginMappings.TryGetValue(apiHostname, out var clientOrigin))
        {
            throw new InvalidOperationException(
                $"No configured client origin exists for API host '{apiHostname}'. Configure Server:ClientOriginMappings for this API host.");
        }

        return clientOrigin;
    }
}
