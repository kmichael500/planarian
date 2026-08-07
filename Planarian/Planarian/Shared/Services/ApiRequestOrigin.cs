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

    public ApiRequestOrigin(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetOrigin()
    {
        var request = _httpContextAccessor.HttpContext?.Request
            ?? throw new InvalidOperationException("An API request origin is available only during an HTTP request.");

        return $"{request.Scheme}://{request.Host}";
    }
}
