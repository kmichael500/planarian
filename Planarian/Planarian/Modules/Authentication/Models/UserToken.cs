using Planarian.Model.Shared;

namespace Planarian.Modules.Authentication.Models;

public class UserToken
{
    public UserToken(string fullName, string id, string? currentAccountId, int sessionVersion = 0)
    {
        FullName = fullName;
        Id = id;
        CurrentAccountId = currentAccountId;
        SessionVersion = sessionVersion;
    }

    public string FullName { get; init; }
    public string Id { get; init; }
    public string? CurrentAccountId { get; set; }
    public int SessionVersion { get; init; }
}