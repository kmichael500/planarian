using System.Web;

namespace Planarian.Shared.Helpers;

public static class UrlHelper
{
    public static string Build(string serverBaseUrl, string path, string? accountId)
    {
        var builder = new UriBuilder(new Uri(new Uri(serverBaseUrl), path));
        if (string.IsNullOrWhiteSpace(accountId))
            return builder.Uri.ToString();

        var query = HttpUtility.ParseQueryString(builder.Query);
        query["account_id"] = accountId;
        builder.Query = query.ToString() ?? string.Empty;
        return builder.Uri.ToString();
    }
}
