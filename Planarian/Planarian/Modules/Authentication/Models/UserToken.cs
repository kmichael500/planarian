namespace Planarian.Modules.Authentication.Models;

public class UserToken
{
    public UserToken(string fullName, string id, string? accountId)
    {
        FullName = fullName;
        Id = id;
        AccountId = accountId;
    }

    public string FullName { get; init; }
    public string Id { get; init; }
    public string? AccountId { get; set; }
}