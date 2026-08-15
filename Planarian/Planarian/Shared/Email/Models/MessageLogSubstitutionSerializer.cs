using Newtonsoft.Json;
using Planarian.Shared.Routing;

namespace Planarian.Shared.Email.Models;

public static class MessageLogSubstitutionSerializer
{
    public const string RedactedPathSegment = "[redacted]";

    public static string Serialize(IReadOnlyDictionary<string, object> substitutions)
    {
        var loggedSubstitutions = new Dictionary<string, object>(substitutions, StringComparer.Ordinal);
        if (loggedSubstitutions.TryGetValue("buttonUrl", out var buttonUrl) && buttonUrl is string url)
        {
            loggedSubstitutions["buttonUrl"] = SanitizeButtonUrl(url);
        }

        return JsonConvert.SerializeObject(loggedSubstitutions);
    }

    public static string SanitizeButtonUrl(string buttonUrl)
    {
        if (string.IsNullOrWhiteSpace(buttonUrl)) return buttonUrl;

        if (Uri.TryCreate(buttonUrl, UriKind.Absolute, out var absoluteUri) &&
            (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            var builder = new UriBuilder(absoluteUri)
            {
                Query = string.Empty,
                Fragment = string.Empty
            };
            builder.Path = SanitizePath(builder.Path);
            return builder.Uri.AbsoluteUri;
        }

        var queryOrFragmentIndex = buttonUrl.IndexOfAny(['?', '#']);
        var path = queryOrFragmentIndex >= 0 ? buttonUrl[..queryOrFragmentIndex] : buttonUrl;
        return SanitizePath(path);
    }

    private static string SanitizePath(string path)
    {
        if (!path.StartsWith(UserInvitationRoutes.Client.Prefix, StringComparison.OrdinalIgnoreCase)) return path;
        if (path.Length <= UserInvitationRoutes.Client.Prefix.Length) return path;
        return UserInvitationRoutes.Client.Prefix + RedactedPathSegment;
    }
}
