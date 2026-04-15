namespace Planarian.Modules.Authentication.Models;

public class ApiTokenLoginResultVm
{
    public ApiTokenLoginResultVm(string accessToken)
    {
        AccessToken = accessToken;
    }

    public string AccessToken { get; set; }
}
