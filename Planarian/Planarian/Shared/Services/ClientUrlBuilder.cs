using Planarian.Shared.Helpers;

namespace Planarian.Shared.Services;

/// <summary>
/// Builds frontend URLs for the client origin configured for the current API request.
/// </summary>
public class ClientUrlBuilder
{
    private readonly IClientRequestOrigin _clientRequestOrigin;

    public ClientUrlBuilder(IClientRequestOrigin clientRequestOrigin)
    {
        _clientRequestOrigin = clientRequestOrigin;
    }

    public string GetOrigin()
    {
        return _clientRequestOrigin.GetOrigin();
    }

    public string BuildCaveUrl(string caveId)
    {
        return BuildPath($"/caves/{Uri.EscapeDataString(caveId)}");
    }

    public string BuildPasswordResetUrl(string resetCode)
    {
        return BuildPathWithCode("/reset-password", resetCode);
    }

    public string BuildEmailConfirmationUrl(string confirmationCode)
    {
        return BuildPathWithCode("/confirm-email", confirmationCode);
    }

    public string BuildInvitationUrl(string invitationCode)
    {
        return BuildPath($"/user/invitations/{Uri.EscapeDataString(invitationCode)}");
    }

    private string BuildPathWithCode(string path, string code)
    {
        return BuildPath($"{path}?code={Uri.EscapeDataString(code)}");
    }

    private string BuildPath(string path)
    {
        return UrlHelper.Build(GetOrigin(), path, null);
    }
}
