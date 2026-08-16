namespace Planarian.Modules.Users.Models;

public sealed record EmailConfirmationResult(
    string EmailAddress,
    string UserId,
    bool SessionVersionChanged);
