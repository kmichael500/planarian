using Planarian.Shared.Helpers;
using Planarian.Shared.Routing;

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
        return BuildPath(UserPasswordResetRoutes.Client.Get(resetCode));
    }

    public string BuildEmailConfirmationUrl(string confirmationCode)
    {
        return BuildPath(UserEmailConfirmationRoutes.Client.Get(confirmationCode));
    }

    public string BuildInvitationUrl(string invitationCode)
    {
        return BuildPath(UserInvitationRoutes.Client.Get(invitationCode));
    }

    private string BuildPath(string path)
    {
        return UrlHelper.Build(GetOrigin(), path, null);
    }
}
