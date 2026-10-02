using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Planarian.Shared.Routing;

namespace Planarian.Shared.Email.Models;

public static partial class MessageLogSubstitutionSerializer
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
            var originalPath = absoluteUri.AbsolutePath;
            var builder = new UriBuilder(absoluteUri)
            {
                Path = SanitizePath(originalPath),
                Query = SanitizeQuery(originalPath, absoluteUri.Query),
                Fragment = string.Empty
            };
            return builder.Uri.AbsoluteUri;
        }

        var fragmentIndex = buttonUrl.IndexOf('#');
        var withoutFragment = fragmentIndex >= 0 ? buttonUrl[..fragmentIndex] : buttonUrl;
        var queryIndex = withoutFragment.IndexOf('?');
        var path = queryIndex >= 0 ? withoutFragment[..queryIndex] : withoutFragment;
        var query = queryIndex >= 0 ? withoutFragment[queryIndex..] : string.Empty;
        return SanitizePath(path) + SanitizeQuery(path, query);
    }

    private static string SanitizeQuery(string path, string query)
    {
        if (string.IsNullOrEmpty(query) || !IsCredentialQueryRoute(path)) return query;
        return CodeQueryParameterRegex().Replace(query, match =>
            $"{match.Groups[1].Value}{match.Groups[2].Value}={RedactedPathSegment}");
    }

    private static bool IsCredentialQueryRoute(string path)
    {
        return string.Equals(path, ClientRoutes.EmailConfirmation.Path, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, ClientRoutes.PasswordReset.Path, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizePath(string path)
    {
        if (!path.StartsWith(ClientRoutes.Invitation.Prefix, StringComparison.OrdinalIgnoreCase)) return path;
        if (path.Length <= ClientRoutes.Invitation.Prefix.Length) return path;

        var credentialEnd = path.IndexOf('/', ClientRoutes.Invitation.Prefix.Length);
        return credentialEnd < 0
            ? ClientRoutes.Invitation.Prefix + RedactedPathSegment
            : ClientRoutes.Invitation.Prefix + RedactedPathSegment + path[credentialEnd..];
    }

    [GeneratedRegex(@"(^|[?&])(code)=([^&#]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CodeQueryParameterRegex();
}
