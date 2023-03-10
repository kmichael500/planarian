namespace Planarian.Modules.Authentication.Models;

public class UserToken
{
    public UserToken(string FullName, string Id)
    {
        this.FullName = FullName;
        this.Id = Id;
    }

    public string FullName { get; init; }
    public string Id { get; init; }
}